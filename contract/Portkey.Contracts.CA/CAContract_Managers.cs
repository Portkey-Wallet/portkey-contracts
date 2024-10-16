using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    // For SocialRecovery
    public override Empty SocialRecovery(SocialRecoveryInput input)
    {
        Assert(input != null, "invalid input");
        Assert(IsValidHash(input!.LoginGuardianIdentifierHash), "invalid input login guardian");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(input.ManagerInfo!.ExtraData), "invalid input extraData");
        Assert(input.ManagerInfo.Address != null, "invalid input address");
        var loginGuardianIdentifierHash = input.LoginGuardianIdentifierHash;
        var caHash = State.GuardianMap[loginGuardianIdentifierHash];

        Assert(caHash != null, "CA Holder does not exist.");

        var holderInfo = GetHolderInfoByCaHash(caHash);
        
        if (NeedToCheckCreateChain(input.GuardiansApproved))
        {
            AssertCreateChain(holderInfo);
        }
        
        var guardians = holderInfo.GuardianList!.Guardians;

        Assert(input.GuardiansApproved.Count > 0, "invalid input Guardians Approved");
        
        var operationDetails = input.ManagerInfo.Address.ToBase58();
        var guardianApprovedCount = GetGuardianApprovedCount(caHash, input.GuardiansApproved,
            nameof(OperationType.SocialRecovery).ToLower(), operationDetails);

        var strategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(guardians.Count, guardianApprovedCount,
            strategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo exists");
        UpdateManagerTransactionStatistics(caHash, input.ManagerInfo.Address);
        var caAddress = Context.ConvertVirtualAddressToContractAddress(caHash);
        ClearNonTransactionManager(caHash, caAddress);
        Assert(holderInfo.ManagerInfos.Count < GetManagerMaxCount(),
            "The amount of ManagerInfos out of limit");

        State.HolderInfoMap[caHash].ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(caHash, input.ManagerInfo);

        Context.Fire(new ManagerInfoSocialRecovered()
        {
            CaHash = caHash,
            CaAddress = caAddress,
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });
        FireInvitedLogEvent(caHash, nameof(SocialRecovery), input.ReferralCode, input.ProjectCode);
        return new Empty();
    }

    public override Empty AddManagerInfo(AddManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        //Assert(Context.Sender.Equals(input.ManagerInfo.Address), "No permission to add");

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo address exists");
        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        ClearNonTransactionManager(input.CaHash, caAddress);
        Assert(holderInfo.ManagerInfos.Count < GetManagerMaxCount(),
            "The amount of ManagerInfos out of limit");

        holderInfo.ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(input.CaHash, input.ManagerInfo);

        Context.Fire(new ManagerInfoAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        return new Empty();
    }

    // For manager remove itself
    public override Empty RemoveManagerInfo(RemoveManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        Assert(IsValidHash(input!.CaHash), "invalid input caHash");
        CheckManagerInfoPermission(input!.CaHash, Context.Sender);

        return RemoveManager(input.CaHash, Context.Sender);
    }

    // For manager remove other
    public override Empty RemoveOtherManagerInfo(RemoveOtherManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        Assert(!Context.Sender.Equals(input.ManagerInfo.Address), "One should not remove itself");
        Assert(input.GuardiansApproved != null && input.GuardiansApproved.Count > 0, "invalid input guardiansApproved");

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);

        var operateDetails = input.ManagerInfo.Address.ToBase58();
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.RemoveOtherManagerInfo).ToLower(), operateDetails);

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count,
            guardianApprovedCount, holderInfo.JudgementStrategy);
        return !isJudgementStrategySatisfied ? new Empty() : RemoveManager(input.CaHash, input.ManagerInfo.Address);
    }

    private Empty RemoveManager(Hash caHash, Address address)
    {
        var holderInfo = GetHolderInfoByCaHash(caHash);
        AssertCreateChain(holderInfo);

        // Manager does not exist
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, address);
        if (managerInfo == null)
        {
            return new Empty();
        }

        var caAddress = Context.ConvertVirtualAddressToContractAddress(caHash);

        holderInfo.ManagerInfos.Remove(managerInfo);
        RemoveDelegator(caHash, managerInfo);

        Context.Fire(new ManagerInfoRemoved
        {
            CaHash = caHash,
            CaAddress = caAddress,
            Manager = managerInfo.Address,
            ExtraData = managerInfo.ExtraData
        });

        return new Empty();
    }

    public override Empty UpdateManagerInfos(UpdateManagerInfosInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfosInput(input!.CaHash, input.ManagerInfos);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var managerInfosToUpdate = input.ManagerInfos.Distinct().ToList();

        var managerInfoList = holderInfo.ManagerInfos;

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        foreach (var manager in managerInfosToUpdate)
        {
            var managerToUpdate = managerInfoList.FirstOrDefault(m => m.Address == manager.Address);
            if (managerToUpdate == null || managerToUpdate.ExtraData == manager.ExtraData)
            {
                continue;
            }

            managerToUpdate.ExtraData = manager.ExtraData;

            Context.Fire(new ManagerInfoUpdated
            {
                CaHash = input.CaHash,
                CaAddress = caAddress,
                Manager = managerToUpdate.Address,
                ExtraData = managerToUpdate.ExtraData
            });
        }

        return new Empty();
    }

    private void CheckManagerInfoInput(Hash hash, ManagerInfo managerInfo)
    {
        Assert(hash != null, "invalid input CaHash");
        CheckManagerInfoPermission(hash, Context.Sender);
        Assert(managerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(managerInfo!.ExtraData) && managerInfo.Address != null,
            "invalid input managerInfo");
    }

    private void CheckManagerInfosInput(Hash hash, RepeatedField<ManagerInfo> managerInfos)
    {
        Assert(hash != null, "invalid input CaHash");
        CheckManagerInfoPermission(hash, Context.Sender);
        Assert(managerInfos != null && managerInfos.Count > 0, "invalid input managerInfo");

        foreach (var managerInfo in managerInfos!)
        {
            Assert(!string.IsNullOrWhiteSpace(managerInfo!.ExtraData) && managerInfo.Address != null,
                "invalid input managerInfo");
        }
    }

    public override Empty ManagerForwardCall(ManagerForwardCallInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        Assert(input.ContractAddress != null && !string.IsNullOrWhiteSpace(input.MethodName),
            "Invalid input.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(!State.ForbiddenForwardCallContractMethod[input.ContractAddress][input.MethodName.ToLower()],
            $"Does not have permission for {input.MethodName}.");
        if (input.MethodName == nameof(State.TokenContract.Transfer) &&
            input.ContractAddress == State.TokenContract.Value)
        {
            var transferInput = TransferInput.Parser.ParseFrom(input.Args);
            UpdateDailyTransferredAmount(input.CaHash, input.GuardiansApproved, transferInput.Symbol,
                transferInput.Amount, transferInput.To);
        }

        Context.SendVirtualInline(input.CaHash, input.ContractAddress, input.MethodName, input.Args, true);
        return new Empty();
    }

    public override Empty ManagerTransfer(ManagerTransferInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.To != null && !string.IsNullOrWhiteSpace(input.Symbol), "Invalid input.");
        UpdateDailyTransferredAmount(input.CaHash, input.GuardiansApproved, input.Symbol, input.Amount, input.To);
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value, nameof(State.TokenContract.Transfer),
            new TransferInput
            {
                To = input.To,
                Amount = input.Amount,
                Symbol = input.Symbol,
                Memo = input.Memo
            }.ToByteString(), true);
        return new Empty();
    }

    public override Empty ManagerTransferFrom(ManagerTransferFromInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.From != null && input.To != null && !string.IsNullOrWhiteSpace(input.Symbol),
            "Invalid input.");
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.TransferFrom),
            new TransferFromInput
            {
                From = input.From,
                To = input.To,
                Amount = input.Amount,
                Symbol = input.Symbol,
                Memo = input.Memo
            }.ToByteString(), true);
        return new Empty();
    }

    public override Empty ManagerApprove(ManagerApproveInput input)
    {
        Assert(input != null, "invalid input");
        Assert(input.CaHash != null, "CA hash is null.");
        Assert(input.Symbol != null, "symbol is null.");
        Assert(input.Spender != null && !input.Spender.Value.IsNullOrEmpty(), "Invalid input address.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        if (input.GuardiansApproved.Count > 0)
        {
            var operateDetails = $"{input.Spender.ToBase58()}_{input.Symbol}_{input.Amount}"; 
            GuardianApprovedCheck(input.CaHash, input.GuardiansApproved, OperationType.Approve,
                nameof(OperationType.Approve).ToLower(), operateDetails);
        }
        else
        {
            Assert(State.ManagerApproveSpenderWhitelistMap[input.Spender],
                "invalid input Guardians Approved");
        }
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.Approve),
            new ApproveInput
            {
                Spender = input.Spender,
                Amount = input.Amount,
                Symbol = input.Symbol,
            }.ToByteString(), true);
        Context.Fire(new ManagerApproved
        {
            CaHash = input.CaHash,
            Spender = input.Spender,
            Amount = input.Amount,
            Symbol = input.Symbol,
        });
        return new Empty();
    }

    public override Empty AddManagerApproveSpenderWhitelist(AddManagerApproveSpenderWhitelistInput input)
    {
        Assert(input != null && input.SpenderList.Count > 0, "Invalid input");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        foreach (var spender in input.SpenderList)
        {
            Assert(!spender.Value.IsNullOrEmpty(), "Invalid input");
            State.ManagerApproveSpenderWhitelistMap[spender] = true;
        }
        return new Empty();
    }

    public override Empty RemoveManagerApproveSpenderWhitelist(RemoveManagerApproveSpenderWhitelistInput input)
    {
        Assert(input != null && input.SpenderList.Count > 0, "Invalid input");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        foreach (var spender in input.SpenderList)
        {
            Assert(!spender.Value.IsNullOrEmpty(), "Invalid input");
            State.ManagerApproveSpenderWhitelistMap[spender] = false;
        }
        return new Empty();
    }

    public override BoolValue CheckInManagerApproveSpenderWhitelist(Address input)
    {
        return new BoolValue
        {
            Value = State.ManagerApproveSpenderWhitelistMap[input]
        };
    }


    public override Empty SetForbiddenForwardCallContractMethod(SetForbiddenForwardCallContractMethodInput input)
    {
        Assert(input != null && input.Address != null, "Invalid input");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrWhiteSpace(input.MethodName), "MethodName cannot be empty");
        State.ForbiddenForwardCallContractMethod[input.Address][input.MethodName.ToLower()] = input.Forbidden;
        Context.Fire(new ForbiddenForwardCallContractMethodChanged
        {
            MethodName = input.MethodName,
            Address = input.Address,
            Forbidden = input.Forbidden
        });
        return new Empty();
    }

    private void CheckManagerInfoPermission(Hash caHash, Address address)
    {
        UpdateManagerTransactionStatistics(caHash, address);
        Assert(State.HolderInfoMap[caHash] != null, $"CA holder is null.CA hash:{caHash}");
        Assert(State.HolderInfoMap[caHash].ManagerInfos.Any(m => m.Address == address), "No permission.");
    }

    private void ClearNonTransactionManager(Hash caHash, Address caAddress)
    {
        var managerInfos = State.HolderInfoMap[caHash].ManagerInfos;
        if (managerInfos == null)
        {
            return;
        }

        var existedManagerCount = managerInfos.Count;
        if (existedManagerCount + 2 < GetManagerMaxCount())
        {
            return;
        }
        var managerStatisticsInfoList = State.ManagerTransactionStatistics[caHash];
        //if there was no transaction data, remove the earliest manager
        if (managerStatisticsInfoList?.ManagerStatisticsInfos == null)
        {
            managerInfos.RemoveAt(0);
            return;
        }

        var canRemoveManagerMaxCount = GetCanRemoveManagerMaxCount();
        var transactionManagerAddresses = managerStatisticsInfoList.ManagerStatisticsInfos.Select(msi => msi.Address).ToList();
        var nonTransactionManagers = managerInfos.Where(mg => !transactionManagerAddresses.Contains(mg.Address)).ToList();
        //if non transaction managers number is lower than 10, remove all of them
        if(nonTransactionManagers.Count < canRemoveManagerMaxCount)
        {
            var nonTransactionManagerCount = nonTransactionManagers.Count;
            foreach (var nonTransactionManager in nonTransactionManagers)
            {
                //part1: remove non-transaction managers
                DoRemoveManager(caHash, caAddress, managerInfos, nonTransactionManager);
            }

            var removingTransactionCount = canRemoveManagerMaxCount - nonTransactionManagerCount;
            var sortedByTransactions = managerStatisticsInfoList.ManagerStatisticsInfos
                .OrderBy(msi => msi.TransactionFrequency).ToList();
            var managerStatisticsInfos = sortedByTransactions.GetRange(0, removingTransactionCount);
            if (managerStatisticsInfos.Count == 0)
            {
                return;
            }

            var leastManagerAddresses = managerStatisticsInfos.Select(msi => msi.Address).ToList();
            if (leastManagerAddresses.Count == 0)
            {
                return;
            }

            var removingManagerInfos = managerInfos.Where(mg => leastManagerAddresses.Contains(mg.Address)).ToList();
            foreach (var removingManagerInfo in removingManagerInfos)
            {
                //part2: remove managers that have transaction record and less transaction frequency
                DoRemoveManager(caHash, caAddress, managerInfos, removingManagerInfo);
            }
        }
        //if non transaction managers number is larger than 10, remove the first 10 managers that had no transactions
        else
        {
            for (var i = 0; i < canRemoveManagerMaxCount; i++)
            {
                //nonTransactionManagers are also sorted by transaction time
                DoRemoveManager(caHash, caAddress, managerInfos, nonTransactionManagers[i]);
            }
        }
    }

    private void DoRemoveManager(Hash caHash, Address caAddress, RepeatedField<ManagerInfo> managerInfos, ManagerInfo removingManager)
    {
        managerInfos.Remove(removingManager);
        Context.Fire(new ManagerInfoRemoved
        {
            CaHash = caHash,
            CaAddress = caAddress,
            Manager = removingManager.Address,
            ExtraData = removingManager.ExtraData
        });
    }

    private void UpdateManagerTransactionStatistics(Hash caHash, Address manager)
    {
        if (caHash == null || manager == null)
        {
            return;
        }
        if (State.ManagerTransactionStatistics[caHash] == null)
        {
            State.ManagerTransactionStatistics[caHash] = new ManagerStatisticsInfoList()
            {
                ManagerStatisticsInfos =
                {
                    MakeNewManagerStatisticsInfo(manager)
                }
            };
            return;
        }
        
        var managerStatisticsInfos = State.ManagerTransactionStatistics[caHash];
        var existedManager = managerStatisticsInfos.ManagerStatisticsInfos
            .FirstOrDefault(mg => mg.Address.Equals(manager));
        if (existedManager == null)
        {
            managerStatisticsInfos.ManagerStatisticsInfos.Add(MakeNewManagerStatisticsInfo(manager));
        }
        else
        {
            existedManager.TransactionFrequency++;
            existedManager.LatestTransactionTimestamp = Context.CurrentBlockTime;
        }
    }

    private ManagerStatisticsInfo MakeNewManagerStatisticsInfo(Address manager)
    {
        return new ManagerStatisticsInfo
        {
            Address = manager,
            LatestTransactionTimestamp = Context.CurrentBlockTime,
            TransactionFrequency = 1
        };
    }

    private ManagerInfo FindManagerInfo(RepeatedField<ManagerInfo> managerInfos, Address address)
    {
        return managerInfos.FirstOrDefault(s => s.Address == address);
    }
    
    /// <summary>
    /// When all 'VerificationDoc.Length >= 8', there is no need to verify the 'CreateChain'.
    /// </summary>
    /// <param name="guardianApproved"></param>
    /// <returns></returns>
    private bool NeedToCheckCreateChain(RepeatedField<GuardianInfo> guardianApproved)
    {
        if (guardianApproved.Count == 0)
        {
            return true;
        }

        foreach (var guardianInfo in guardianApproved)
        {
            if (CanZkLoginExecute(guardianInfo))
            {
                continue;
            }
            if (guardianInfo?.VerificationInfo == null ||
                string.IsNullOrWhiteSpace(guardianInfo.VerificationInfo.VerificationDoc) ||
                GetVerificationDocLength(guardianInfo.VerificationInfo.VerificationDoc) < 8)
            {
                return true;
            }
        }

        return false;
    }

    private int GetManagerMaxCount()
    {
        return State.ManagerMaxCount?.Value is not > CAContractConstants.ManagerMaxCount 
            ? CAContractConstants.ManagerMaxCount : State.ManagerMaxCount.Value;
    }
    
    private int GetCanRemoveManagerMaxCount()
    {
        return State.CanRemoveManagerMaxCount?.Value ?? CAContractConstants.CanRemoveManagerMaxCount;
    }

    public override Int32Value GetCanRemoveManagerMaxCount(Empty input)
    {
        return new Int32Value()
        {
            Value = State.CanRemoveManagerMaxCount.Value
        };
    }

    public override Empty SetCanRemoveManagerMaxCount(CanRemoveManagerMaxCountInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No SetCanRemoveManagerMaxCount permission.");
        Assert(input != null, "Invalid input when SetCanRemoveManagerMaxCount.");
        Assert(input?.CanRemoveMaxCount is > 0 and < 30, "Invalid can-remove-max-count scope when SetCanRemoveManagerMaxCount.");
        State.CanRemoveManagerMaxCount.Value = input.CanRemoveMaxCount;
        return new Empty();
    }
    
    public override Int32Value GetManagerMaxCount(Empty input)
    {
        return new Int32Value()
        {
            Value = State.ManagerMaxCount.Value
        };
    }

    public override Empty SetManagerMaxCount(ManagerMaxCountInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No SetManagerMaxCount permission.");
        Assert(input != null, "Invalid input when SetManagerMaxCount.");
        Assert(input?.MaxCount is > 0 and < 100, "Invalid max count scope when SetManagerMaxCount.");
        State.ManagerMaxCount.Value = input.MaxCount;
        return new Empty();
    }
    
    public override ManagerStatisticsInfoList GetManagerTransactionStatistics(ManagerStatisticsInput input)
    {
        Assert(input != null, "Invalid input when GetManagerTransactionStatistics.");
        Assert(input?.CaHash != null, "Invalid caHash when GetManagerTransactionStatistics.");
        var managerStatisticsInfoList = State.ManagerTransactionStatistics[input?.CaHash];
        Assert(managerStatisticsInfoList is { ManagerStatisticsInfos.Count: > 0 }, "There's no manager statistics");
        return managerStatisticsInfoList;
    }
}