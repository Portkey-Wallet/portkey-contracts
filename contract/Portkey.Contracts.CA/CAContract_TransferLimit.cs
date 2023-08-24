using System;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.State;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty SetTransferLimit(SetTransferLimitInput input)
    {
        Assert(input != null, "invalid input");
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
        TransferGuardianApprovedCheck(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.ModifyTransferLimit).ToLower());
        // State.TransferLimit[input.CaHash][input.Symbol] =
        UpdateAccountTransferLimit(input.CaHash, input.Symbol, input.SingleLimit, input.DailyLimit);
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
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
        Assert(State.TransferLimit[input.CaHash] != null, $"TransferLimit is null.CA hash:{input.CaHash}.");
        Assert(State.TransferLimit[input.CaHash][input.Symbol] != null,
            $"This symbol {input.Symbol} has not set transferLimit");

        return new GetTransferLimitOutput()
        {
            SingleLimit = State.TransferLimit[input.CaHash][input.Symbol].SingleLimit,
            DailyLimit = State.TransferLimit[input.CaHash][input.Symbol].DayLimit,
            DailyTransferredAmount = IsOverDay(State.DailyTransferredAmount[input.CaHash][input.Symbol].UpdateTime,
                GetCurrentBlockTimeString(Context.CurrentBlockTime))
                ? 0
                : State.DailyTransferredAmount[input.CaHash][input.Symbol].DailyTransfered
        };
    }

    public override Empty SetDefaultTokenTransferLimit(SetDefaultTokenTransferLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
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
        string methodName)
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

        Assert(IsJudgementStrategySatisfied(guardians.Count, guardianApprovedAmount, holderInfo.JudgementStrategy),
            "JudgementStrategy validate failed");
    }

    private void UpdateAccountTransferLimit(Hash caHash, string symbol, long singleLimit, long dailyLimit)
    {
        var accountTransferLimit = GetAccountTransferLimit(caHash, symbol);
        State.TransferLimit[caHash][symbol] = new TransferLimit()
        {
            DayLimit = dailyLimit > 0
                ? dailyLimit
                : accountTransferLimit.DayLimit,
            SingleLimit = singleLimit > 0
                ? singleLimit
                : accountTransferLimit.SingleLimit
        };
    }

    private void UpdateDailyTransferredLimit(Hash caHash, string symbol, long amount)
    {
        Assert(amount > 0, "Invalid amount.");
        var transferLimit = GetAccountTransferLimit(caHash, symbol);
        var transferredAmount = State.DailyTransferredAmount[caHash][symbol] ??
                                new TransferredAmount() { DailyTransfered = 0 };
        Assert(amount <= transferLimit.SingleLimit,
            $"The transfer amount {amount} has exceeded the single transfer limit {transferLimit.SingleLimit}");

        var blockTime = GetCurrentBlockTimeString(Context.CurrentBlockTime);
        var transferBalance = transferLimit.DayLimit - (IsOverDay(transferredAmount.UpdateTime, blockTime)
            ? 0
            : transferredAmount.DailyTransfered);
        Assert(amount <= transferBalance,
            $"The transfer amount {amount} has exceeded the daily transfer balance {transferBalance}.");
        State.DailyTransferredAmount[caHash][symbol] = new TransferredAmount()
        {
            DailyTransfered = transferBalance - amount,
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