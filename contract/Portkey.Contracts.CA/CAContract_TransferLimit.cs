using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty SetTransferLimit(SetTransferLimitInput input)
    {
        Assert(input != null, "invalid input");
        Assert(!string.IsNullOrEmpty(input.Symbol), "Invalid symbol.");
        Assert(State.HolderInfoMap[input.CaHash] != null, $"CA holder is null.CA hash:{input.CaHash}");
        Assert(State.HolderInfoMap[input.CaHash].ManagerInfos.Any(m => m.Address == Context.Sender), "No permission.");
        TransferGuardianApprovedCheck(input.CaHash, input.GuardiansApproved, OperationType.ModifyTransferLimit,
            nameof(OperationType.ModifyTransferLimit).ToLower());
        if (input.SingleLimit <= 0 || input.DailyLimit <= 0)
            Assert(input.SingleLimit == -1 && input.DailyLimit == -1, "Invalid transfer limit");
        Assert(GetTokenInfo(input.Symbol) != null && GetTokenInfo(input.Symbol).Symbol == input.Symbol,
            $"Not exist symbol {input.Symbol}");
        State.TransferLimit[input.CaHash][input.Symbol] = new TransferLimit()
        {
            DayLimit = input.DailyLimit,
            SingleLimit = input.SingleLimit
        };
        State.DailyTransferredAmount[input.CaHash][input.Symbol] = new TransferredAmount()
        {
            UpdateTime = GetCurrentBlockTimeString(Context.CurrentBlockTime),
            DailyTransfered = 0
        };
        Context.Fire(new TransferLimitChanged()
        {
            CaHash = input.CaHash,
            Symbol = input.Symbol,
            SingleLimit = input.SingleLimit,
            DailyLimit = input.DailyLimit
        });
        return new Empty();
    }

    public override GetTransferLimitOutput GetTransferLimit(GetTransferLimitInput input)
    {
        Assert(IsValidHash(input!.CaHash), "invalid input caHash.");
        Assert(!string.IsNullOrEmpty(input.Symbol), "Invalid symbol.");

        // When the user does not set transferLimit, use the default value of symbol
        var transferLimit = GetAccountTransferLimit(input.CaHash, input.Symbol);
        return new GetTransferLimitOutput
        {
            SingleLimit = transferLimit.SingleLimit,
            DailyLimit = transferLimit.DayLimit,
            DailyTransferredAmount = State.DailyTransferredAmount[input.CaHash] != null &&
                                     State.DailyTransferredAmount[input.CaHash][input.Symbol] != null && !IsOverDay(
                                         State.DailyTransferredAmount[input.CaHash][input.Symbol].UpdateTime,
                                         GetCurrentBlockTimeString(Context.CurrentBlockTime))
                ? State.DailyTransferredAmount[input.CaHash][input.Symbol].DailyTransfered
                : 0
        };
    }

    public override Empty SetDefaultTokenTransferLimit(SetDefaultTokenTransferLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
        Assert(GetTokenInfo(input.Symbol) != null && GetTokenInfo(input.Symbol).Symbol == input.Symbol,
            $"Not exist symbol {input.Symbol}");
        Assert(input.DefaultLimit > 0, "DefaultLimit cannot be less than 0.");
        State.DefaultTokenTransferLimit[input.Symbol] = input.DefaultLimit;
        return new Empty();
    }

    public override GetDefaultTokenTransferLimitOutput GetDefaultTokenTransferLimit(
        GetDefaultTokenTransferLimitInput input)
    {
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
        return new GetDefaultTokenTransferLimitOutput()
        {
            Symbol = input.Symbol,
            DefaultLimit = State.DefaultTokenTransferLimit[input.Symbol] > 0
                ? State.DefaultTokenTransferLimit[input.Symbol]
                : State.TokenDefaultTransferLimit.Value > 0
                    ? State.TokenDefaultTransferLimit.Value
                    : CAContractConstants.TokenDefaultTransferLimitAmount
        };
    }

    private void TransferGuardianApprovedCheck(Hash caHash, RepeatedField<GuardianInfo> guardiansApproved,
        OperationType operationType, string methodName)
    {
        Assert(IsValidHash(caHash), "invalid input CaHash");
        Assert(guardiansApproved.Count > 0, "invalid input Guardians Approved");
        var holderInfo = GetHolderInfoByCaHash(caHash);
        var guardians = holderInfo.GuardianList!.Guardians;
        var guardianApprovedAmount = 0;
        var guardianApprovedList = guardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}").ToList();
        foreach (var guardian in guardianApprovedList)
        {
            if (!IsGuardianExist(caHash, guardian)) continue;
            var isApproved = CheckVerifierSignatureAndDataCompatible(guardian, methodName);
            if (!isApproved) continue;
            guardianApprovedAmount++;
        }

        var holderJudgementStrategy = State.OperationStrategy[caHash][operationType] ?? holderInfo.JudgementStrategy;
        Assert(IsJudgementStrategySatisfied(guardians.Count, guardianApprovedAmount, holderJudgementStrategy),
            "JudgementStrategy validate failed");
    }

    private void UpdateDailyTransferredLimit(Hash caHash, string symbol, long amount)
    {
        Assert(amount > 0, "Invalid amount.");
        // When transferlimit is -1, no limit calculation is performed.
        if (State.TransferLimit[caHash]?[symbol]?.DayLimit == -1) return;

        var transferLimit = GetAccountTransferLimit(caHash, symbol);
        var transferredAmount = State.DailyTransferredAmount[caHash][symbol] ??
                                new TransferredAmount() { DailyTransfered = 0 };
        Assert(amount <= transferLimit.SingleLimit,
            $"The transfer amount {amount} has exceeded the single transfer limit {transferLimit.SingleLimit}");
        var blockTime = GetCurrentBlockTimeString(Context.CurrentBlockTime);
        var transferred = IsOverDay(transferredAmount.UpdateTime, blockTime)
            ? 0
            : transferredAmount.DailyTransfered;
        Assert(amount <= transferLimit.DayLimit - transferred,
            $"The transfer amount {amount} has exceeded the daily transfer balance.");
        State.DailyTransferredAmount[caHash][symbol] = new TransferredAmount
        {
            DailyTransfered = transferred + amount,
            UpdateTime = IsOverDay(transferredAmount.UpdateTime, blockTime)
                ? blockTime
                : transferredAmount.UpdateTime
        };
    }

    private TransferLimit GetAccountTransferLimit(Hash caHash, string symbol)
    {
        // If the currency does not exist, the TokenDefaultTransferLimit is used
        var defaultTokenTransferLimit = State.DefaultTokenTransferLimit[symbol] > 0
            ? State.DefaultTokenTransferLimit[symbol]
            // If TokenDefaultTransferLimit is not configured, the default transfer limit will be used
            : State.TokenDefaultTransferLimit.Value > 0
                ? State.TokenDefaultTransferLimit.Value
                : CAContractConstants.TokenDefaultTransferLimitAmount;
        if (State.TransferLimit[caHash] == null || State.TransferLimit[caHash][symbol] == null)
        {
            State.TransferLimit[caHash][symbol] = new TransferLimit()
            {
                SingleLimit = defaultTokenTransferLimit,
                DayLimit = defaultTokenTransferLimit
            };
        }

        return State.TransferLimit[caHash][symbol];
    }
}