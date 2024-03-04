using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
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
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        Assert(
            holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
                g.VerifierId == input.GuardianToAdd.VerificationInfo.Id) == null,
            "The verifier already exists");
        Assert(
            holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
                g.Type == input.GuardianToAdd.Type && g.IdentifierHash == input.GuardianToAdd.IdentifierHash) == null,
            "The account already exists");

        var methodName = nameof(OperationType.AddGuardian).ToLower();
        //Check the verifier signature and data of the guardian to be added.
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName);

        if (!CheckVerifierSignatureAndData(input.GuardianToAdd, methodName, input.CaHash))
        {
            return new Empty();
        }

        //Whether the approved guardians count is satisfied.
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count,
            guardianApprovedCount, holderJudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }
        //var loginGuardians = GetLoginGuardians(holderInfo.GuardianList);

        var guardianAdded = new Guardian
        {
            IdentifierHash = input.GuardianToAdd!.IdentifierHash,
            Salt = GetSaltFromVerificationDoc(input.GuardianToAdd.VerificationInfo.VerificationDoc),
            Type = input.GuardianToAdd.Type,
            VerifierId = input.GuardianToAdd.VerificationInfo.Id,
            IsLoginGuardian = false
        };
        State.HolderInfoMap[input.CaHash].GuardianList?.Guardians.Add(guardianAdded);

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        Context.Fire(new GuardianAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            GuardianAdded_ = guardianAdded
        });
        return new Empty();
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

        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.RemoveGuardian).ToLower());

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1),
            guardianApprovedCount, holderInfo.JudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        State.HolderInfoMap[input.CaHash].GuardianList?.Guardians.Remove(toRemoveGuardian);

        if (State.LoginGuardianMap[toRemoveGuardian.IdentifierHash][toRemoveGuardian.VerifierId] != null)
        {
            State.LoginGuardianMap[toRemoveGuardian.IdentifierHash].Remove(toRemoveGuardian.VerifierId);
        }

        if (State.GuardianMap[toRemoveGuardian.IdentifierHash] != null)
        {
            State.GuardianMap.Remove(toRemoveGuardian.IdentifierHash);
        }


        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        Context.Fire(new GuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            GuardianRemoved_ = toRemoveGuardian
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
        Assert(input.CaHash != null && input.GuardianToUpdatePre != null
                                    && input.GuardianToUpdateNew != null && input.GuardiansApproved.Count != 0,
            "Invalid input.");
        Assert(input.GuardianToUpdatePre?.Type == input.GuardianToUpdateNew?.Type &&
               input.GuardianToUpdatePre?.IdentifierHash == input.GuardianToUpdateNew?.IdentifierHash,
            "Inconsistent guardian.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
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

        if (existPreGuardian == null || toUpdateGuardian != null)
        {
            return new Empty();
        }

        Assert(
            holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
                g.VerifierId == input.GuardianToUpdateNew.VerificationInfo.Id) == null,
            "The verifier already exists");
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

        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.UpdateGuardian).ToLower());

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1),
            guardianApprovedCount, holderInfo.JudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        existPreGuardian.VerifierId = input.GuardianToUpdateNew?.VerificationInfo.Id;

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
            GuardianUpdatedNew = existPreGuardian
        });

        return new Empty();
    }
}