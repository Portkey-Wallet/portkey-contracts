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
        var operateDetails = $"{input.Symbol}_{input.SingleLimit}_{input.DailyLimit}";
        GuardianApprovedCheck(input.CaHash, input.GuardiansApproved, OperationType.ModifyTransferLimit,
            nameof(OperationType.ModifyTransferLimit).ToLower(), operateDetails);
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
        var transferLimit = State.TransferLimit[input.CaHash][input.Symbol] != null
            ? State.TransferLimit[input.CaHash][input.Symbol]
            : GetDefaultTransferLimit(input.Symbol);

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
        Assert(input.TransferLimit.SingleLimit > 0, "SingleLimit cannot be less than 0.");
        Assert(input.TransferLimit.DayLimit > 0, "DayLimit cannot be less than 0.");
        State.TokenDefaultTransferLimit[input.Symbol] = new TransferLimit
        {
            SingleLimit = input.TransferLimit.SingleLimit,
            DayLimit = input.TransferLimit.DayLimit
        };

        Context.Fire(new DefaultTokenTransferLimitChanged
        {
            Symbol = input.Symbol,
            TransferLimit = new TransferLimit
            {
                SingleLimit = input.TransferLimit.SingleLimit,
                DayLimit = input.TransferLimit.DayLimit
            }
        });
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
            TransferLimit = GetDefaultTransferLimit(input.Symbol)
        };
    }

    private void GuardianApprovedCheck(Hash caHash, RepeatedField<GuardianInfo> guardiansApproved,
        OperationType operationType, string methodName, string operateDetails = null)
    {
        Assert(IsValidHash(caHash), "invalid input CaHash");
        Assert(guardiansApproved.Count > 0, "invalid input Guardians Approved");
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(State.HolderInfoMap[caHash] != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo.GuardianList?.Guardians?.Count > 0, "Processing on the chain...");
        var guardianApprovedCount = GetGuardianApprovedCount(caHash, guardiansApproved, methodName, operateDetails);

        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        var onSyncChain = holderInfo.CreateChainId != 0 && holderInfo.CreateChainId != Context.ChainId;
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
                holderJudgementStrategy),
            onSyncChain ? "Processing on the chain..." : "JudgementStrategy validate failed");
    }

    private void UpdateDailyTransferredAmount(Hash caHash, RepeatedField<GuardianInfo> guardiansApproved, string symbol,
        long amount, Address to)
    {
        Assert(amount > 0, "Invalid amount.");
        var transferredAmount = State.DailyTransferredAmountMap[caHash][symbol] ??
                                new TransferredAmount { DailyTransfered = 0 };
        var overDayFlag = IsOverDay(transferredAmount.UpdateTime, Context.CurrentBlockTime);
        var transferred = overDayFlag
            ? 0
            : transferredAmount.DailyTransfered;
        if (guardiansApproved.Count > 0)
        {
            var operateDetails = $"{to.ToBase58()}_{symbol}_{amount}";
            GuardianApprovedCheck(caHash, guardiansApproved, OperationType.GuardianApproveTransfer,
                nameof(OperationType.GuardianApproveTransfer).ToLower(), operateDetails);
        }
        else
        {
            Assert(IsTransferSecurity(caHash), "Low transfer security level.");
            if (State.TransferLimit[caHash]?[symbol]?.DayLimit == -1) return;
            var transferLimit = State.TransferLimit[caHash][symbol] != null
                ? State.TransferLimit[caHash][symbol]
                : GetDefaultTransferLimit(symbol);
            Assert(amount <= transferLimit.SingleLimit,
                $"The transfer amount {amount} has exceeded the single transfer limit {transferLimit.SingleLimit}");
            Assert(amount <= transferLimit.DayLimit - transferred,
                $"The transfer amount {amount} has exceeded the daily transfer balance.");
        }

        State.DailyTransferredAmountMap[caHash][symbol] = new TransferredAmount
        {
            DailyTransfered = transferred + amount,
            UpdateTime = overDayFlag
                ? Context.CurrentBlockTime
                : transferredAmount.UpdateTime
        };
    }

    private TransferLimit GetDefaultTransferLimit(string symbol)
    {
        return new TransferLimit
        {
            SingleLimit = State.TokenDefaultTransferLimit[symbol] != null
                ? State.TokenDefaultTransferLimit[symbol].SingleLimit
                : State.TokenInitialTransferLimit.Value > 0
                    ? State.TokenInitialTransferLimit.Value
                    : CAContractConstants.TokenDefaultTransferLimitAmount,
            DayLimit = State.TokenDefaultTransferLimit[symbol] != null
                ? State.TokenDefaultTransferLimit[symbol].DayLimit
                : State.TokenInitialTransferLimit.Value > 0
                    ? State.TokenInitialTransferLimit.Value
                    : CAContractConstants.TokenDefaultTransferLimitAmount
        };
    }

    public override Empty SetTransferSecurityThreshold(SetTransferSecurityThresholdInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.TransferSecurityThreshold != null, "Security threshold cannot be null.");
        Assert(input.TransferSecurityThreshold.BalanceThreshold > 0, "Token threshold cannot be less than 0.");
        Assert(input.TransferSecurityThreshold.GuardianThreshold > 0, "Guardian threshold cannot be less than 0.");
        Assert(input.TransferSecurityThreshold.Symbol != null, "Symbol cannot be null.");

        if (State.TransferSecurityThresholdList?.Value != null)
        {
            foreach (var securityThreshold in State.TransferSecurityThresholdList.Value.TransferSecurityThresholds)
            {
                if (securityThreshold.Symbol == input.TransferSecurityThreshold.Symbol)
                {
                    // If the value is the same as before, it will be returned directly.
                    if (securityThreshold.GuardianThreshold == input.TransferSecurityThreshold.GuardianThreshold &&
                        securityThreshold.BalanceThreshold == input.TransferSecurityThreshold.BalanceThreshold)
                        return new Empty();

                    // Otherwise, elements will be removed first and added last.
                    State.TransferSecurityThresholdList.Value.TransferSecurityThresholds.Remove(securityThreshold);
                    break;
                }
            }
        }
        else
        {
            State.TransferSecurityThresholdList.Value = new TransferSecurityThresholdList();
        }

        State.TransferSecurityThresholdList.Value.TransferSecurityThresholds.Add(new TransferSecurityThreshold
        {
            Symbol = input.TransferSecurityThreshold.Symbol,
            GuardianThreshold = input.TransferSecurityThreshold.GuardianThreshold,
            BalanceThreshold = input.TransferSecurityThreshold.BalanceThreshold
        });

        Context.Fire(new TransferSecurityThresholdChanged
        {
            Symbol = input.TransferSecurityThreshold.Symbol,
            GuardianThreshold = input.TransferSecurityThreshold.GuardianThreshold,
            BalanceThreshold = input.TransferSecurityThreshold.BalanceThreshold
        });
        return new Empty();
    }

    public override GetTransferSecurityCheckResultOutput GetTransferSecurityCheckResult(
        GetTransferSecurityCheckResultInput input)
    {
        Assert(State.HolderInfoMap[input.CaHash] != null, $"CA holder is null.CA hash:{input.CaHash}");
        return new GetTransferSecurityCheckResultOutput
        {
            IsSecurity = IsTransferSecurity(input.CaHash)
        };
    }
    
    public override Empty SetTokenInitialTransferLimit(
        SetTokenInitialTransferLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.TokenInitialTransferLimit >= 0, "Token initial transfer limit cannot be less than 0.");
        State.TokenInitialTransferLimit.Value = input.TokenInitialTransferLimit;
        return new Empty();
    }

    private bool IsTransferSecurity(Hash caHash)
    {
        var holderInfo = State.HolderInfoMap[caHash];
        var guardianAmount = holderInfo.GuardianList?.Guardians?.Count;
        if (State.TransferSecurityThresholdList?.Value != null)
        {
            foreach (var securityThreshold in State.TransferSecurityThresholdList.Value.TransferSecurityThresholds)
            {
                if (guardianAmount > securityThreshold.GuardianThreshold) continue;
                var balance = GetTokenBalance(securityThreshold.Symbol,
                    Context.ConvertVirtualAddressToContractAddress(caHash));
                if (balance.Balance >= securityThreshold.BalanceThreshold) return false;
            }
        }

        return true;
    }
    
    
    
    
}