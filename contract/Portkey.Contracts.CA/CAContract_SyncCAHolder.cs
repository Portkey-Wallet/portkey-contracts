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

        ValidateNotLoginGuardian(input.CaHash, holderInfo, input.NotLoginGuardians);

        ValidateManager(holderInfo, input.ManagerInfos);
        AssertCreateChain(holderInfo);
        Assert(holderInfo.CreateChainId == input.CreateChainId, "Invalid input createChainId.");
        Assert(input.GuardianList?.Guardians?.Count > 0, "Input guardianList is empty.");
        Assert(holderInfo.GuardianList.Guardians.Count == input.GuardianList.Guardians.Count,
            "The amount of input.GuardianList not equals to HolderInfo's GuardianList");
        ValidateGuardianList(holderInfo.GuardianList, input.GuardianList);
        return new Empty();
    }

    private void ValidateNotLoginGuardian(Hash caHash, HolderInfo holderInfo, RepeatedField<Hash> notLoginGuardians)
    {
        var notLoginGuardianList = new RepeatedField<Hash>();
        notLoginGuardianList.AddRange(holderInfo.GuardianList.Guardians.Where(g => !g.IsLoginGuardian)
            .Select(g => g.IdentifierHash));
        var notLoginGuardianIdentifierHashList = notLoginGuardians.ToList();
        Assert(notLoginGuardianList.Count == notLoginGuardianIdentifierHashList.Count,
            "The amount of input.NotLoginGuardians not equals to HolderInfo's NotLoginGuardians");
        var loginGuardians = new RepeatedField<Hash>();
        loginGuardians.AddRange(holderInfo.GuardianList.Guardians.Where(g => g.IsLoginGuardian)
            .Select(g => g.IdentifierHash));
        foreach (var guardian in notLoginGuardianIdentifierHashList)
        {
            Assert(!loginGuardians.Contains(guardian) &&
                   State.GuardianMap[guardian] == caHash,
                $"NotLoginGuardian:{guardian} is in HolderInfo's LoginGuardians");
        }

        var list = notLoginGuardianIdentifierHashList.Except(notLoginGuardianList).ToList();
        Assert(list.Count == 0, $"NotLoginGuardian:{list} is not in HolderInfo's NotLoginGuardians");
    }

    private void ValidateGuardianList(GuardianList desGuardianList, GuardianList srcGuardianList)
    {
        foreach (var guardianInfo in desGuardianList.Guardians)
        {
            var searchGuardian = srcGuardianList.Guardians.FirstOrDefault(
                g => g.IdentifierHash == guardianInfo.IdentifierHash && g.Type == guardianInfo.Type &&
                     g.VerifierId == guardianInfo.VerifierId
            );
            Assert(searchGuardian != null, $"Guardian:{guardianInfo.VerifierId} is not in CAHolder GuardianList");
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

        foreach (var loginGuardian in loginGuardians)
        {
            Assert(loginGuardianIdentifierHashList.Contains(loginGuardian)
                   && State.GuardianMap[loginGuardian] == caHash,
                $"LoginGuardian:{loginGuardian} is not in HolderInfo's LoginGuardians");
        }
        var list = loginGuardians.Except(loginGuardianIdentifierHashList).ToList();
        Assert(list.Count == 0, $"LoginGuardian:{list} is not in HolderInfo's LoginGuardians");
        
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

    public override Empty SyncHolderInfos(SyncHolderInfosInput input)
    {
        foreach (var verificationTransactionInfo in input.VerificationTransactionInfos)
        {
            SyncHolderInfo(verificationTransactionInfo);
        }

        return new Empty();
    }

    public override Empty SyncHolderInfo(SyncHolderInfoInput input)
    {
        SyncHolderInfo(input.VerificationTransactionInfo);
        return new Empty();
    }

    private void SyncHolderInfo(VerificationTransactionInfo verificationTransactionInfo)
    {
        var originalTransaction = Transaction.Parser.ParseFrom(verificationTransactionInfo.TransactionBytes);
        AssertCrossChainTransaction(originalTransaction,
            nameof(ValidateCAHolderInfoWithManagerInfosExists));

        var originalTransactionId = originalTransaction.GetHash();

        TransactionVerify(originalTransactionId, verificationTransactionInfo.ParentChainHeight,
            verificationTransactionInfo.FromChainId, verificationTransactionInfo.MerklePath);
        var transactionInput =
            ValidateCAHolderInfoWithManagerInfosExistsInput.Parser.ParseFrom(originalTransaction.Params);
        Assert(!State.SyncHolderInfoTransaction[originalTransactionId], "Already synced.");
        Assert(State.SyncHolderInfoTransactionHeightMap[transactionInput.CaHash] < verificationTransactionInfo.ParentChainHeight,
            "Already synced.");
        var holderId = transactionInput.CaHash;
        var holderInfo = State.HolderInfoMap[holderId] ?? new HolderInfo { CreatorAddress = Context.Sender };
        holderInfo.CreateChainId = transactionInput.CreateChainId;
        
        var caAddress = Context.ConvertVirtualAddressToContractAddress(holderId);
        if (holderInfo.GuardianList == null)
        {
            holderInfo.GuardianList = new GuardianList
            {
                Guardians = { }
            };
            SetProjectDelegator(caAddress);
        }

        var managerInfosToAdd = ManagerInfosExcept(transactionInput.ManagerInfos, holderInfo.ManagerInfos);
        var managerInfosToRemove = ManagerInfosExcept(holderInfo.ManagerInfos, transactionInput.ManagerInfos);

        foreach (var managerInfo in managerInfosToRemove)
        {
            holderInfo.ManagerInfos.Remove(managerInfo);
        }

        RemoveDelegators(holderId, managerInfosToRemove);
        holderInfo.ManagerInfos.AddRange(managerInfosToAdd);
        SetDelegators(holderId, managerInfosToAdd);

        var loginGuardiansAdded = SyncLoginGuardianAdded(transactionInput.CaHash, transactionInput.LoginGuardians);
        var loginGuardiansUnbound =
            SyncLoginGuardianUnbound(transactionInput.CaHash, transactionInput.NotLoginGuardians);

        var guardiansAdded = new RepeatedField<Guardian>();
        var guardiansRemoved = new RepeatedField<Guardian>();

        guardiansAdded =
            GuardiansExcept(transactionInput.GuardianList.Guardians, holderInfo.GuardianList.Guardians);
        guardiansRemoved =
            GuardiansExcept(holderInfo.GuardianList.Guardians, transactionInput.GuardianList.Guardians);
        foreach (var guardian in guardiansAdded)
        {
            holderInfo.GuardianList.Guardians.Add(guardian);
        }

        var loginGuardians = GetLoginGuardians(holderInfo.GuardianList);
        foreach (var guardian in guardiansRemoved)
        {
            if (loginGuardians.Contains(guardian))
            {
                var loginGuardianCount = loginGuardians.Count(g => g.IdentifierHash == guardian.IdentifierHash);
                //   and it is the only one, refuse. If you really wanna to remove it, unset it first.
                Assert(loginGuardianCount > 1,
                    $"Cannot remove a Guardian for login, to remove it, unset it first. {guardian.IdentifierHash} is a guardian for login.");
            }

            holderInfo.GuardianList.Guardians.Remove(guardian);
        }

        State.HolderInfoMap[holderId] = holderInfo;
        State.SyncHolderInfoTransaction[originalTransactionId] = true;
        State.SyncHolderInfoTransactionHeightMap[transactionInput.CaHash] = verificationTransactionInfo.ParentChainHeight;

        var guardians = holderInfo.GuardianList.Guardians;
        foreach (var guardian in guardians)
        {
            State.PreCrossChainSyncHolderInfoMarks.Remove(guardian.IdentifierHash);
        }

        Context.Fire(new CAHolderSynced
        {
            Creator = Context.Sender,
            CaHash = holderId,
            CaAddress = caAddress,
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
            CreateChainId = transactionInput.CreateChainId
        });
    }


    private void AssertCrossChainTransaction(Transaction originalTransaction,
        string validMethodName)
    {
        Assert(originalTransaction.MethodName == validMethodName && originalTransaction.To == Context.Self, "Invalid transaction.");
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

    private RepeatedField<Guardian> GuardiansExcept(RepeatedField<Guardian> src,
        RepeatedField<Guardian> destination)
    {
        RepeatedField<Guardian> resultSet = new RepeatedField<Guardian>();

        foreach (var srcGuardian in src)
        {
            bool theSame = false;
            foreach (var desGuardian in destination)
            {
                if (srcGuardian.IdentifierHash == desGuardian.IdentifierHash &&
                    srcGuardian.VerifierId == desGuardian.VerifierId && srcGuardian.Type == desGuardian.Type)
                {
                    theSame = true;
                    break;
                }
            }

            if (!theSame)
            {
                resultSet.Add(srcGuardian);
            }
        }

        return resultSet;
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