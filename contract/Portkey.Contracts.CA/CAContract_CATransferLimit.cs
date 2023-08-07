using System;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty ManagerApprove(ManagerApproveInput input)
    {
        Assert(input != null, "invalid input");
        TransferGuardianApprovedCheck(input.CaHash, input.ManagerInfo, input.GuardiansApproved,
            input.JudgementStrategy);

        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.Approve),
            new ApproveInput
            {
                Spender = input.Spender,
                Amount = input.Amount,
                Symbol = input.Symbol,
            }.ToByteString());

        Context.Fire(new ManagerApproved()
        {
            CaHash = input.CaHash,
            Spender = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
            ManagerInfo = input.ManagerInfo,
            JudgementStrategy = input.JudgementStrategy,
            Symbol = input.Symbol,
            Amount = input.Amount
        });

        return new Empty();
    }

    public override Empty ManagerUnApprove(ManagerUnApproveInput input)
    {
        Assert(input != null, "invalid input");
        TransferGuardianApprovedCheck(input.CaHash, input.ManagerInfo, input.GuardiansApproved,
            input.JudgementStrategy);

        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.UnApprove),
            new UnApproveInput
            {
                Spender = input.Spender,
                Amount = Int64.MaxValue,
                Symbol = input.Symbol,
            }.ToByteString());

        Context.Fire(new ManagerUnApproved()
        {
            CaHash = input.CaHash,
            Spender = input.Spender,
            ManagerInfo = input.ManagerInfo,
            JudgementStrategy = input.JudgementStrategy,
            Symbol = input.Symbol,
        });

        return new Empty();
    }

    public override Empty SetCATransferLimit(SetCATransferLimitInput input)
    {
        Assert(input != null, "invalid input");
        TransferGuardianApprovedCheck(input.CaHash, input.ManagerInfo, input.GuardiansApproved,
            input.JudgementStrategy);

        State.CATransferLimit[input.CaHash][input.Symbol] =
            GetAccountSymbolAmount(input.CaHash, input.Symbol, input.SingleLimit, input.DailyLimit);
        State.DailyTransferredAmount[input.CaHash][input.Symbol] = new TransferredAmount()
        {
            UpdateTime = GetCurrentBlockTimeString(Context.CurrentBlockTime),
            DailyTransfered = 0
        };

        Context.Fire(new CATransferLimitChanged()
        {
            CaHash = input.CaHash,
            ManagerInfo = input.ManagerInfo,
            JudgementStrategy = input.JudgementStrategy,
            Symbol = input.Symbol,
            SingleLimit = input.SingleLimit,
            DailyLimit = input.DailyLimit
        });

        return new Empty();
    }

    public override GetCATransferLimitOutput GetCATransferLimit(GetCATransferLimitInput input)
    {
        Assert(IsValidHash(input!.CaHash), "invalid input caHash.");
        Assert(!string.IsNullOrEmpty(input.Symbol) && input.Symbol.All(IsValidSymbolChar), "Invalid symbol.");
        Assert(State.CATransferLimit[input.CaHash] != null, $"CATransferLimit is null.CA hash:{input.CaHash}.");
        Assert(State.CATransferLimit[input.CaHash][input.Symbol] != null,
            $"This symbol {input.Symbol} has not set transferLimit");

        return new GetCATransferLimitOutput()
        {
            SingleLimit = State.CATransferLimit[input.CaHash][input.Symbol].SingleLimit,
            DailyLimit = State.CATransferLimit[input.CaHash][input.Symbol].DayLimit,
            DailyTransferredAmount = IsOverDay(State.DailyTransferredAmount[input.CaHash][input.Symbol].UpdateTime,
                GetCurrentBlockTimeString(Context.CurrentBlockTime))
                ? 0
                : State.DailyTransferredAmount[input.CaHash][input.Symbol].DailyTransfered
        };
    }

    private void TransferGuardianApprovedCheck(Hash caHash, ManagerInfo managerInfo,
        RepeatedField<GuardianInfo> guardiansApproved, StrategyNode judgementStrategy)
    {
        Assert(IsValidHash(caHash), "invalid input CaHash");
        Assert(managerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(managerInfo!.ExtraData), "invalid input extraData");
        Assert(managerInfo.Address != null, "invalid input address");
        Assert(guardiansApproved.Count > 0, "invalid input Guardians Approved");

        var holderInfo = GetHolderInfoByCaHash(caHash);
        var guardians = holderInfo.GuardianList!.Guardians;
        var guardianApprovedAmount = 0;
        var guardianApprovedList = guardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}").ToList();
        var methodName = nameof(SetCATransferLimit).ToLower();
        foreach (var guardian in guardianApprovedList)
        {
            if (!IsGuardianExist(caHash, guardian)) continue;
            var isApproved = CheckVerifierSignatureAndDataCompatible(guardian, methodName);
            if (!isApproved) continue;
            guardianApprovedAmount++;
        }

        Assert(IsJudgementStrategySatisfied(guardians.Count, guardianApprovedAmount,
            judgementStrategy), "JudgementStrategy validate failed");
    }

    private TransferLimit GetAccountSymbolAmount(Hash caHash, string symbol, long singleLimit, long dailyLimit)
    {
        return new TransferLimit()
        {
            // When limit is not equal to 0, use the input value
            DayLimit = dailyLimit > 0
                ? dailyLimit
                // When the input limit equal 0, and the user set first time, use the default symbol limit value.
                : State.CATransferLimit[caHash] == null
                    ? State.DefaultTokenTransferLimit[symbol] > 0
                        ? State.DefaultTokenTransferLimit[symbol]
                        // Non-mainstream symbol use the default limit
                        : CAContractConstants.SingleTransferLimitDefaultAmount
                    : State.CATransferLimit[caHash][symbol].DayLimit,
            SingleLimit = singleLimit > 0
                ? singleLimit
                : State.CATransferLimit[caHash] == null
                    ? State.DefaultTokenTransferLimit[symbol] > 0
                        ? State.DefaultTokenTransferLimit[symbol]
                        : CAContractConstants.DailyTransferLimitDefaultAmount
                    : State.CATransferLimit[caHash][symbol].SingleLimit
        };
    }
}