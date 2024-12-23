using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    // Add a guardian, if already added, return 
    public override Empty AddGuardian(AddGuardianInput input)
    {
        Assert(input.CaHash != null && input.GuardianToAdd != null && input.GuardiansApproved.Count != 0,
            "Invalid input.");
        Assert(input.GuardianToAdd.VerificationInfo != null, "input.GuardianToAdd verification is null");
        Assert(input.GuardianToAdd.VerificationInfo.Id != null && !Hash.Empty.Equals(input.GuardianToAdd.VerificationInfo.Id), "input.GuardianToAdd verification id is invalid");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertWhenVerifierIdInValid(holderInfo.GuardianList.Guardians, input.GuardianToAdd);
        if (holderInfo.GuardianList.Guardians.Any(g =>
                g.Type == input.GuardianToAdd.Type && g.IdentifierHash == input.GuardianToAdd.IdentifierHash))
        {
            return new Empty();
        }
        foreach (var guardianInfo in input.GuardiansApproved)
        {
            CheckZkParams(guardianInfo);
        }
        var guardianToAddSupportZk = CheckZkParams(input.GuardianToAdd);
        var methodName = nameof(OperationType.AddGuardian).ToLower();
        //for the guardian supporting zk, the front end inputs zk params, portkey contract verifies with zk
        if (guardianToAddSupportZk)
        {
            Assert(CheckZkLoginVerifierAndData(input.GuardianToAdd, input.CaHash), "input.GuardianToAdd supports zkLogin, but verified failed ");
        }
        else //otherwise portkey contract uses the original verifier
        {
            Assert(CheckVerifierSignatureAndData(input.GuardianToAdd, methodName, input.CaHash), "CheckVerifierSignatureAndData error ");
        }
        //Check the verifier signature and data of the guardian to be added.
        var operateDetails = $"{input.GuardianToAdd.IdentifierHash.ToHex()}_{(int)input.GuardianToAdd.Type}_{input.GuardianToAdd.VerificationInfo.Id.ToHex()}";
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName, operateDetails);

        //Whether the approved guardians count is satisfied.
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count,
            guardianApprovedCount, holderJudgementStrategy, input.CaHash);
        Assert(isJudgementStrategySatisfied, "The number of approved guardians does not meet the requirements ");
        
        //var loginGuardians = GetLoginGuardians(holderInfo.GuardianList);
        //for new users of new version,portkey contract should generate a random verifier for the guardian supporting zk
        //for old users of new version,portkey contract uses zk as the default verifier,replacing the original verifier
        //for users of old version,portkey contract uses the original verifier,the front end won't input zk parameters
        Guardian guardianAdded;
        if (guardianToAddSupportZk)
        {
            guardianAdded = new Guardian
            {
                IdentifierHash = input.GuardianToAdd!.IdentifierHash,
                Salt = input.GuardianToAdd.ZkLoginInfo.Salt,
                Type = input.GuardianToAdd.Type,
                VerifierId = input.GuardianToAdd.VerificationInfo.Id,
                IsLoginGuardian = false,
                ZkLoginInfo = input.GuardianToAdd.ZkLoginInfo,
                ManuallySupportForZk = true,
                PoseidonIdentifierHash = input.GuardianToAdd.ZkLoginInfo.PoseidonIdentifierHash
            };
        }
        else
        {
            guardianAdded = new Guardian
            {
                IdentifierHash = input.GuardianToAdd!.IdentifierHash,
                Salt = GetSaltFromVerificationDoc(input.GuardianToAdd.VerificationInfo.VerificationDoc),
                Type = input.GuardianToAdd.Type,
                VerifierId = input.GuardianToAdd.VerificationInfo.Id,
                IsLoginGuardian = false
            };
        }
        State.HolderInfoMap[input.CaHash].GuardianList?.Guardians.Add(guardianAdded);

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        Context.Fire(new GuardianAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            GuardianAdded_ = guardianAdded,
            Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
        });
        return new Empty();
    }

    private void AssertWhenVerifierIdInValid(RepeatedField<Guardian> guardians, GuardianInfo newGuardianInfo)
    {
        if (CanZkLoginExecute(newGuardianInfo))
        {
            return;
        }

        var notSupportZkGuardians = guardians.Where(g => !CanGuardianZkLoginExecute(g)).ToList();
        if (notSupportZkGuardians.Count == 0)
        {
            return;
        }
        Assert(!notSupportZkGuardians
            .Any(gd => gd.VerifierId.Equals(newGuardianInfo.VerificationInfo.Id)), "The verifier already exists");
    }

    // Remove a Guardian, if already removed, return 
    public override Empty RemoveGuardian(RemoveGuardianInput input)
    {
        Assert(input.CaHash != null && input.GuardianToRemove != null && input.GuardiansApproved.Count != 0,
            "Invalid input.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        //Select satisfied guardian to remove.
        //Filter: guardian.type && guardian.&& && VerifierId
        //for guardian supporting zk,portkey contract could also use verifierId as the condition,cause a random verifier would be generated
        var toRemoveGuardian = holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
            g.Type == input.GuardianToRemove.Type &&
            g.IdentifierHash == input.GuardianToRemove.IdentifierHash &&
            g.VerifierId == input.GuardianToRemove.VerificationInfo.Id);

        if (toRemoveGuardian == null)
        {
            return new Empty();
        }

        //   Get all loginGuardian.
        var loginGuardians = GetLoginGuardians(holderInfo.GuardianList);
        //   If the guardian to be removed is a loginGuardian, ...
        if (loginGuardians.Contains(toRemoveGuardian))
        {
            var loginGuardianCount = loginGuardians.Count(g => g.IdentifierHash == toRemoveGuardian.IdentifierHash);
            //   and it is the only one, refuse. If you really wanna to remove it, unset it first.
            Assert(loginGuardianCount > 1,
                $"Cannot remove a Guardian for login, to remove it, unset it first. {input.GuardianToRemove?.IdentifierHash} is a guardian for login.");
        }
        
        foreach (var guardian in input.GuardiansApproved)
        {
            Assert(
                !(guardian.Type == toRemoveGuardian.Type &&
                  guardian.IdentifierHash == toRemoveGuardian.IdentifierHash &&
                  guardian.VerificationInfo.Id == toRemoveGuardian.VerifierId),
                "Guardian approved list contains to removed guardian.");
        }

        var operateDetails = $"{input.GuardianToRemove.IdentifierHash.ToHex()}_{(int)input.GuardianToRemove.Type}_{input.GuardianToRemove.VerificationInfo.Id.ToHex()}";
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.RemoveGuardian).ToLower(), operateDetails);

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1),
            guardianApprovedCount, holderInfo.JudgementStrategy, input.CaHash);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        State.HolderInfoMap[input.CaHash].GuardianList?.Guardians.Remove(toRemoveGuardian);

        if (State.LoginGuardianMap[toRemoveGuardian.IdentifierHash][toRemoveGuardian.VerifierId] != null)
        {
            State.LoginGuardianMap[toRemoveGuardian.IdentifierHash].Remove(toRemoveGuardian.VerifierId);
        }

        if (toRemoveGuardian.IsLoginGuardian && State.GuardianMap[toRemoveGuardian.IdentifierHash] != null)
        {
            State.GuardianMap.Remove(toRemoveGuardian.IdentifierHash);
        }


        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        Context.Fire(new GuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            GuardianRemoved_ = toRemoveGuardian,
            Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
        });

        return new Empty();
    }

    private RepeatedField<Guardian> GetLoginGuardians(GuardianList guardianList)
    {
        var loginGuardians = new RepeatedField<Guardian>();
        loginGuardians.AddRange(guardianList.Guardians.Where(g => g.IsLoginGuardian));

        return loginGuardians;
    }

    public override Empty UpdateGuardian(UpdateGuardianInput input)
    {
        //for guardian supporting zk,users mustn't update verifier
        //for guardian not supporting zk,users can update verifier except zk
        Assert(input != null && input.CaHash != null && input.GuardianToUpdatePre != null
                                         && input.GuardianToUpdateNew != null && input.GuardiansApproved.Count != 0,
            "Invalid input.");
        Assert(input.GuardianToUpdatePre?.Type == input.GuardianToUpdateNew?.Type &&
               input.GuardianToUpdatePre?.IdentifierHash == input.GuardianToUpdateNew?.IdentifierHash,
            "Inconsistent guardian.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        if (!IsZkLoginSupported(input.GuardianToUpdatePre.Type))
            Assert(!input.GuardianToUpdatePre.UpdateSupportZk, "Invalid updateSupportZk when the pre guardian doesn't support zk");
        if (IsZkLoginSupported(input.GuardianToUpdateNew.Type))
            Assert(input.GuardianToUpdateNew.UpdateSupportZk, "Invalid updateSupportZk when the new guardian supports zk");
        else
            Assert(!input.GuardianToUpdateNew.UpdateSupportZk, "Invalid updateSupportZk when the new guardian doesn't support zk");
        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        //Whether the guardian to be updated in the holder info.
        //Filter: guardian.type && guardian.IdentifierHash && VerifierId
        var existPreGuardian = holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
            g.Type == input.GuardianToUpdatePre.Type &&
            g.IdentifierHash == input.GuardianToUpdatePre.IdentifierHash &&
            g.VerifierId == input.GuardianToUpdatePre.VerificationInfo.Id);

        var toUpdateGuardian = holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
            g.Type == input.GuardianToUpdateNew.Type &&
            g.IdentifierHash == input.GuardianToUpdateNew.IdentifierHash &&
            g.VerifierId == input.GuardianToUpdateNew.VerificationInfo.Id);

        //verifier changed to zk
        if (IsZkLoginSupported(input.GuardianToUpdateNew.Type) && !input.GuardianToUpdatePre.UpdateSupportZk && input.GuardianToUpdateNew.UpdateSupportZk)
        {
            Assert(existPreGuardian.VerifierId == toUpdateGuardian.VerifierId, "verifierId can't be changed when new guardian supports zk");
        }
        else
        {
            Assert(existPreGuardian != null, "existPreGuardian not exists");
            Assert(toUpdateGuardian == null, "toUpdateGuardian exists");
            AssertWhenVerifierIdInValid(holderInfo.GuardianList.Guardians, input.GuardianToUpdateNew);
        }
        
        var preGuardian = existPreGuardian.Clone();

        //Check verifier id is exist.
        Assert(State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v =>
            v.Id == input.GuardianToUpdateNew.VerificationInfo.Id) != null, "Verifier is not exist.");

        foreach (var guardian in input.GuardiansApproved)
        {
            Assert(
                !(guardian.Type == existPreGuardian.Type &&
                  guardian.IdentifierHash == existPreGuardian.IdentifierHash &&
                  guardian.VerificationInfo.Id == existPreGuardian.VerifierId),
                "Guardian approved list contains to updated guardian.");
        }

        var operateDetails = $"{input.GuardianToUpdatePre.IdentifierHash.ToHex()}_{(int)input.GuardianToUpdatePre.Type}_" +
                             $"{input.GuardianToUpdatePre.VerificationInfo.Id.ToHex()}_{input.GuardianToUpdateNew.VerificationInfo.Id.ToHex()}";
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.UpdateGuardian).ToLower(), operateDetails);

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1),
            guardianApprovedCount, holderInfo.JudgementStrategy, input.CaHash);
        Assert(isJudgementStrategySatisfied, "guardian approved is" + guardianApprovedCount + ", not satisfied strategy");
        UpdateManuallySupportForZk(input.CaHash, input.GuardiansApproved, holderInfo);
        
        existPreGuardian.VerifierId = input.GuardianToUpdateNew?.VerificationInfo.Id;
        //when the user changed the verifier to zk,the front end would show zk verifier totally, not show zk+original verifier
        existPreGuardian.ManuallySupportForZk = IsZkLoginSupported(input.GuardianToUpdateNew.Type)
            && !input.GuardianToUpdatePre.UpdateSupportZk && input.GuardianToUpdateNew.UpdateSupportZk;

        if (State.LoginGuardianMap[preGuardian.IdentifierHash][preGuardian.VerifierId] != null)
        {
            State.LoginGuardianMap[preGuardian.IdentifierHash].Remove(preGuardian.VerifierId);
            State.LoginGuardianMap[existPreGuardian.IdentifierHash][existPreGuardian.VerifierId] = input.CaHash;
        }

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        Context.Fire(new GuardianUpdated
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            GuardianUpdatedPre = preGuardian,
            GuardianUpdatedNew = existPreGuardian,
            Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
        });

        return new Empty();
    }
}