using System;
using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Standards.ACS7;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty ValidateCAHolderInfoWithManagerInfosExists(
        ValidateCAHolderInfoWithManagerInfosExistsInput input)
    {
        Assert(input != null, "input is null");
        Assert(IsValidHash(input!.CaHash), "input.CaHash is null or empty");
        Assert(input.ManagerInfos != null, "input.ManagerInfos is null");

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);

        ValidateLoginGuardian(input.CaHash, holderInfo, input.LoginGuardians);

        ValidateManager(holderInfo, input.ManagerInfos);

        Assert(input.CreateChainId == Context.ChainId, "input.CreateChainId is not current ChainId");
        FillCreateChainId(holderInfo);
        Assert(input.CreateChainId == holderInfo.CreateChainId, "input.CreateChainId is not HolderInfo's CreateChainId");
        ValidateGuardianList(holderInfo.GuardianList, input.GuardianList);
        return new Empty();
    }

    private void ValidateGuardianList(GuardianList holdInfoGuardianList, GuardianList inputGuardianList)
    {
        Assert(inputGuardianList != null, "input.GuardianList is null");
        foreach (var guardianInfo in inputGuardianList.Guardians)
        {
            var searchGuardian = holdInfoGuardianList.Guardians.FirstOrDefault(
                g => g.IdentifierHash == guardianInfo.IdentifierHash && g.Type == guardianInfo.Type &&
                     g.VerifierId == guardianInfo.VerifierId
            );
            Assert(searchGuardian != null, $"Guardian:{guardianInfo.VerifierId} is not in HolderInfo's GuardianList");
        }
    }

    private void ValidateLoginGuardian(Hash caHash, HolderInfo holderInfo,
        RepeatedField<Hash> loginGuardianInput)
    {
        var loginGuardians = new RepeatedField<Hash>();
        loginGuardians.AddRange(holderInfo.GuardianList.Guardians.Where(g => g.IsLoginGuardian)
            .Select(g => g.IdentifierHash));

        // LoginGuardianInput may have duplicate IdentifierHash, so must not use Distinct()
        var loginGuardianIdentifierHashList = loginGuardianInput.ToList();

        Assert(loginGuardians.Count == loginGuardianIdentifierHashList.Count,
            "The amount of LoginGuardianInput not equals to HolderInfo's LoginGuardians");

        foreach (var loginGuardian in loginGuardianIdentifierHashList)
        {
            Assert(loginGuardians.Contains(loginGuardian)
                   && State.GuardianMap[loginGuardian] == caHash,
                $"LoginGuardian:{loginGuardian} is not in HolderInfo's LoginGuardians");
        }
    }

    private void ValidateManager(HolderInfo holderInfo, RepeatedField<ManagerInfo> managerInfoInput)
    {
        var managerInfos = managerInfoInput.Distinct().ToList();

        Assert(holderInfo!.ManagerInfos.Count == managerInfos.Count,
            "ManagerInfos set is out of time! Please GetHolderInfo again.");

        foreach (var managerInfo in managerInfos)
        {
            if (!CAHolderContainsManagerInfo(holderInfo.ManagerInfos, managerInfo))
            {
                Assert(false,
                    $"ManagerInfo(address:{managerInfo.Address},extra_data{managerInfo.ExtraData}) is not in this CAHolder.");
            }
        }
    }

    public override Empty SyncHolderInfo(SyncHolderInfoInput input)
    {
        var originalTransaction = Transaction.Parser.ParseFrom(input.VerificationTransactionInfo.TransactionBytes);
        AssertCrossChainTransaction(originalTransaction,
            nameof(ValidateCAHolderInfoWithManagerInfosExists), input.VerificationTransactionInfo.FromChainId);

        var originalTransactionId = originalTransaction.GetHash();

        TransactionVerify(originalTransactionId, input.VerificationTransactionInfo.ParentChainHeight,
            input.VerificationTransactionInfo.FromChainId, input.VerificationTransactionInfo.MerklePath);
        var transactionInput =
            ValidateCAHolderInfoWithManagerInfosExistsInput.Parser.ParseFrom(originalTransaction.Params);

        var holderId = transactionInput.CaHash;
        var holderInfo = State.HolderInfoMap[holderId] ?? new HolderInfo { CreatorAddress = Context.Sender };
        holderInfo.CreateChainId = transactionInput.CreateChainId;

        var managerInfosToAdd = ManagerInfosExcept(transactionInput.ManagerInfos, holderInfo.ManagerInfos);
        var managerInfosToRemove = ManagerInfosExcept(holderInfo.ManagerInfos, transactionInput.ManagerInfos);

        holderInfo.ManagerInfos.AddRange(managerInfosToAdd);
        SetDelegators(holderId, managerInfosToAdd);

        foreach (var managerInfo in managerInfosToAdd)
        {
            SetContractDelegator(managerInfo);
        }

        foreach (var managerInfo in managerInfosToRemove)
        {
            holderInfo.ManagerInfos.Remove(managerInfo);
            RemoveContractDelegator(managerInfo);
        }

        RemoveDelegators(holderId, managerInfosToRemove);

        var loginGuardiansAdded = SyncLoginGuardianAdded(transactionInput.CaHash, transactionInput.LoginGuardians);
        var loginGuardiansUnbound =
            SyncLoginGuardianUnbound(transactionInput.CaHash, transactionInput.NotLoginGuardians);

        var guardiansAdded = GuardiansExcept(transactionInput.GuardianList.Guardians, holderInfo.GuardianList.Guardians);
        var guardiansRemoved = GuardiansExcept(holderInfo.GuardianList.Guardians, transactionInput.GuardianList.Guardians);
        foreach (var guardian in guardiansAdded)
        {
            holderInfo.GuardianList.Guardians.Add(guardian);
        }

        foreach (var guardian in guardiansRemoved)
        {
            holderInfo.GuardianList.Guardians.Remove(guardian);
        }

        var guardiansUpdate = new RepeatedField<Guardian>();
        foreach (var guardianInput in transactionInput.GuardianList.Guardians)
        {
            var guardian = holderInfo.GuardianList.Guardians.FirstOrDefault(o => 
                o.IdentifierHash == guardianInput.IdentifierHash && o.VerifierId != guardianInput.VerifierId);
            if (guardian != null)
            {
                guardiansUpdate.Add(guardianInput);
                guardian.VerifierId = guardianInput.VerifierId;
                guardian.IsLoginGuardian = guardianInput.IsLoginGuardian;
                guardian.Type = guardianInput.Type;
                guardian.Salt = guardianInput.Salt;
            }
        }

        State.HolderInfoMap[holderId] = holderInfo;

        Context.Fire(new CAHolderSynced
        {
            Creator = Context.Sender,
            CaHash = holderId,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(holderId),
            ManagerInfosAdded = new ManagerInfoList
            {
                ManagerInfos = { managerInfosToAdd }
            },
            ManagerInfosRemoved = new ManagerInfoList
            {
                ManagerInfos = { managerInfosToRemove }
            },
            LoginGuardiansAdded = new LoginGuardianList
            {
                LoginGuardians = { loginGuardiansAdded }
            },
            LoginGuardiansUnbound = new LoginGuardianList
            {
                LoginGuardians = { loginGuardiansUnbound }
            },
            GuardiansAdded = new GuardianList
            {
                Guardians = { guardiansAdded }
            },
            GuardiansRemoved = new GuardianList
            {
                Guardians = { guardiansRemoved }
            },
            GuardiansUpdate = new GuardianList
            {
                Guardians = { guardiansUpdate }
            }
        });

        return new Empty();
    }


    private void AssertCrossChainTransaction(Transaction originalTransaction,
        string validMethodName, int fromChainId)
    {
        var validateResult = originalTransaction.MethodName == validMethodName
                             && originalTransaction.To == State.CAContractAddresses[fromChainId];
        Assert(validateResult, "Invalid transaction.");
    }


    private RepeatedField<Hash> SyncLoginGuardianAdded(Hash caHash, RepeatedField<Hash> loginGuardians)
    {
        var list = new RepeatedField<Hash>();

        if (loginGuardians != null)
        {
            foreach (var loginGuardian in loginGuardians)
            {
                if (State.GuardianMap[loginGuardian] == null ||
                    State.GuardianMap[loginGuardian] != caHash)
                {
                    State.GuardianMap.Set(loginGuardian, caHash);
                    list.Add(loginGuardian);
                }
            }
        }

        return list;
    }

    private RepeatedField<Hash> SyncLoginGuardianUnbound(Hash caHash, RepeatedField<Hash> notLoginGuardians)
    {
        var list = new RepeatedField<Hash>();

        if (notLoginGuardians != null)
        {
            foreach (var notLoginGuardian in notLoginGuardians)
            {
                if (State.GuardianMap[notLoginGuardian] == caHash)
                {
                    State.GuardianMap.Remove(notLoginGuardian);
                    list.Add(notLoginGuardian);
                }
            }
        }

        return list;
    }

    private RepeatedField<ManagerInfo> ManagerInfosExcept(RepeatedField<ManagerInfo> set1,
        RepeatedField<ManagerInfo> set2)
    {
        RepeatedField<ManagerInfo> resultSet = new RepeatedField<ManagerInfo>();

        foreach (var managerInfo1 in set1)
        {
            bool theSame = false;
            foreach (var managerInfo2 in set2)
            {
                if (managerInfo1.Address == managerInfo2.Address && managerInfo1.ExtraData == managerInfo2.ExtraData)
                {
                    theSame = true;
                    break;
                }
            }

            if (!theSame)
            {
                resultSet.Add(managerInfo1);
            }
        }

        return resultSet;
    }
    
    private RepeatedField<Guardian> GuardiansExcept(RepeatedField<Guardian> set1,
        RepeatedField<Guardian> set2)
    {
        RepeatedField<Guardian> resultSet = new RepeatedField<Guardian>();

        foreach (var guardian1 in set1)
        {
            bool theSame = false;
            foreach (var guardian2 in set2)
            {
                if (guardian1.IdentifierHash == guardian2.IdentifierHash)
                {
                    theSame = true;
                    break;
                }
            }

            if (!theSame)
            {
                resultSet.Add(guardian1);
            }
        }

        return resultSet;
    }

    private Transaction MethodNameVerify(VerificationTransactionInfo info, string methodNameExpected)
    {
        var originalTransaction = Transaction.Parser.ParseFrom(info.TransactionBytes);
        Assert(originalTransaction.MethodName == methodNameExpected, $"Invalid transaction method.");

        return originalTransaction;
    }


    private void TransactionVerify(Hash transactionId, long parentChainHeight, int chainId, MerklePath merklePath)
    {
        var verificationInput = new VerifyTransactionInput
        {
            TransactionId = transactionId,
            ParentChainHeight = parentChainHeight,
            VerifiedChainId = chainId,
            Path = merklePath
        };
        //
        var crossChainAddress = Context.GetContractAddressByName(SmartContractConstants.CrossChainContractSystemName);
        var verificationResult = Context.Call<BoolValue>(crossChainAddress,
            nameof(ACS7Container.ACS7ReferenceState.VerifyTransaction), verificationInput);
        Assert(verificationResult.Value, "transaction verification failed.");
    }


    private bool CAHolderContainsManagerInfo(RepeatedField<ManagerInfo> managerInfos, ManagerInfo targetManagerInfo)
    {
        foreach (var manager in managerInfos)
        {
            if (manager.Address == targetManagerInfo.Address
                && manager.ExtraData == targetManagerInfo.ExtraData)
            {
                return true;
            }
        }

        return false;
    }
}