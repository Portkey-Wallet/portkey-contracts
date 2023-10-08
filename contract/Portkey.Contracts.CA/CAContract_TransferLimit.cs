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
        GuardianApprovedCheck(input.CaHash, input.GuardiansApproved, OperationType.ModifyTransferLimit,
            nameof(OperationType.ModifyTransferLimit).ToLower());
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        if (input.SingleLimit <= 0 || input.DailyLimit <= 0)
            Assert(input.SingleLimit == -1 && input.DailyLimit == -1, "Invalid transfer limit");
        Assert(GetTokenInfo(input.Symbol) != null, $"Not exist symbol {input.Symbol}");
        State.TransferLimit[input.CaHash][input.Symbol] = new TransferLimit()
        {
            DayLimit = input.DailyLimit,
            SingleLimit = input.SingleLimit
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
            DailyTransferredAmount =
                State.DailyTransferredAmountMap[input.CaHash]?[input.Symbol] != null && !IsOverDay(
                    State.DailyTransferredAmountMap[input.CaHash][input.Symbol].UpdateTime, Context.CurrentBlockTime)
                    ? State.DailyTransferredAmountMap[input.CaHash][input.Symbol].DailyTransfered
                    : 0
        };
    }

    public override Empty SetDefaultTokenTransferLimit(SetDefaultTokenTransferLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Symbol), "Invalid symbol.");
        Assert(GetTokenInfo(input.Symbol) != null, $"Not exist symbol {input.Symbol}");
        Assert(input.DefaultLimit > 0, "DefaultLimit cannot be less than 0.");
        State.TokenDefaultTransferLimit[input.Symbol] = input.DefaultLimit;
        return new Empty();
    }

    public override GetDefaultTokenTransferLimitOutput GetDefaultTokenTransferLimit(
        GetDefaultTokenTransferLimitInput input)
    {
        Assert(!string.IsNullOrEmpty(input.Symbol), "Invalid symbol.");
        Assert(GetTokenInfo(input.Symbol) != null, $"Not exist symbol {input.Symbol}");
        return new GetDefaultTokenTransferLimitOutput
        {
            Symbol = input.Symbol,
            DefaultLimit = State.TokenDefaultTransferLimit[input.Symbol] > 0
                ? State.TokenDefaultTransferLimit[input.Symbol]
                : State.TokenInitialTransferLimit.Value > 0
                    ? State.TokenInitialTransferLimit.Value
                    : CAContractConstants.TokenDefaultTransferLimitAmount
        };
    }

    private void GuardianApprovedCheck(Hash caHash, RepeatedField<GuardianInfo> guardiansApproved,
        OperationType operationType, string methodName)
    {
        Assert(IsValidHash(caHash), "invalid input CaHash");
        Assert(guardiansApproved.Count > 0, "invalid input Guardians Approved");
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(State.HolderInfoMap[caHash] != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo.GuardianList?.Guardians?.Count > 0,
            "Processing one the chain...");
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

        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
                holderJudgementStrategy),
            "JudgementStrategy validate failed");
    }

    private void UpdateDailyTransferredAmount(Hash caHash, string symbol, long amount)
    {
        Assert(amount > 0, "Invalid amount.");
        // When transferlimit is -1, no limit calculation is performed.
        if (State.TransferLimit[caHash]?[symbol]?.DayLimit == -1) return;

        var transferLimit = GetAccountTransferLimit(caHash, symbol);
        var transferredAmount = State.DailyTransferredAmountMap[caHash][symbol] ??
                                new TransferredAmount() { DailyTransfered = 0 };
        Assert(amount <= transferLimit.SingleLimit,
            $"The transfer amount {amount} has exceeded the single transfer limit {transferLimit.SingleLimit}");
        var transferred = IsOverDay(transferredAmount.UpdateTime, Context.CurrentBlockTime)
            ? 0
            : transferredAmount.DailyTransfered;
        Assert(amount <= transferLimit.DayLimit - transferred,
            $"The transfer amount {amount} has exceeded the daily transfer balance.");
        State.DailyTransferredAmountMap[caHash][symbol] = new TransferredAmount
        {
            DailyTransfered = transferred + amount,
            UpdateTime = IsOverDay(transferredAmount.UpdateTime, Context.CurrentBlockTime)
                ? Context.CurrentBlockTime
                : transferredAmount.UpdateTime
        };
    }

    private TransferLimit GetAccountTransferLimit(Hash caHash, string symbol)
    {
        // If the currency does not exist, the TokenDefaultTransferLimit is used
        var defaultTokenTransferLimit = State.TokenDefaultTransferLimit[symbol] > 0
            ? State.TokenDefaultTransferLimit[symbol]
            // If TokenDefaultTransferLimit is not configured, the default transfer limit will be used
            : State.TokenInitialTransferLimit.Value > 0
                ? State.TokenInitialTransferLimit.Value
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

    public override Empty SetCheckChainIdInSignatureEnabled(SetCheckChainIdInSignatureEnabledInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        if (input.CheckChainIdInSignatureEnabled != State.CheckChainIdInSignatureEnabled.Value)
        {
            State.CheckChainIdInSignatureEnabled.Value = input.CheckChainIdInSignatureEnabled;
        }

        return new Empty();
    }

    public override GetCheckChainIdInSignatureEnabledOutput GetCheckChainIdInSignatureEnabled(Empty input)
    {
        return new GetCheckChainIdInSignatureEnabledOutput
        {
            CheckChainIdInSignatureEnabled = State.CheckChainIdInSignatureEnabled.Value
        };
    }

    public override Empty SetTransferSecurityThreshold(SetTransferSecurityThresholdInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Symbol), "Invalid symbol.");
        Assert(GetTokenInfo(input.Symbol) != null, $"Not exist symbol {input.Symbol}");
        Assert(input.TransferSecurityThreshold != null, "Security threshold cannot be null.");
        Assert(input.TransferSecurityThreshold.BalanceThreshold > 0, "Token threshold cannot be less than 0.");
        Assert(input.TransferSecurityThreshold.GuardianThreshold > 0, "Guardian threshold cannot be less than 0.");

        State.TransferSecurityThreshold[input.Symbol] = new TransferSecurityThreshold
        {
            GuardianThreshold = input.TransferSecurityThreshold.GuardianThreshold,
            BalanceThreshold = input.TransferSecurityThreshold.BalanceThreshold
        };

        return new Empty();
    }

    public override GetTransferSecurityCheckResultOutput GetTransferSecurityCheckResult(
        GetTransferSecurityCheckResultInput input)
    {
        Assert(State.TransferSecurityThreshold[input.Symbol] != null,
            $"There is no threshold set for this symbol{input.Symbol}");
        Assert(State.HolderInfoMap[input.CaHash] != null, $"CA holder is null.CA hash:{input.CaHash}");
        var balance = GetTokenBalance(input.Symbol, Context.ConvertVirtualAddressToContractAddress(input.CaHash));

        return new GetTransferSecurityCheckResultOutput
        {
            Symbol = input.Symbol,
            GuardianAmount = State.TransferSecurityThreshold[input.Symbol].GuardianThreshold,
            Balance = balance.Balance,
            IsSecurity = IsTransferSecurity(input.CaHash, input.Symbol)
        };
    }

    private bool IsTransferSecurity(Hash caHash, string symbol)
    {
        // There is no security threshold set for this symbol, return true
        if (State.TransferSecurityThreshold[symbol] == null) return true;
        var holderInfo = State.HolderInfoMap[caHash];
        if (holderInfo.GuardianList?.Guardians?.Count >
            State.TransferSecurityThreshold[symbol].GuardianThreshold) return true;

        var balance = GetTokenBalance(symbol, Context.ConvertVirtualAddressToContractAddress(caHash));
        return balance.Balance < State.TransferSecurityThreshold[symbol].BalanceThreshold;
    }
}