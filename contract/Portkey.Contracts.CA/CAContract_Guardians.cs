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

        //Whether the guardian to be added has already in the holder info.
        //Filter: guardian.type && guardian.IdentifierHash && VerifierId
        var toAddGuardian = holderInfo.GuardianList.Guardians.FirstOrDefault(g =>
            g.Type == input.GuardianToAdd.Type && g.IdentifierHash == input.GuardianToAdd.IdentifierHash &&
            g.VerifierId == input.GuardianToAdd.VerificationInfo.Id);

        if (toAddGuardian != null)
        {
            return new Empty();
        }

        //Check the verifier signature and data of the guardian to be added.
        Assert(CheckVerifierSignatureAndData(input.GuardianToAdd), "Guardian to add verification failed.");

        var guardianApprovedAmount = 0;
        var guardianApprovedList = input.GuardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardian in guardianApprovedList)
        {
            //Whether the guardian exists in the holder info.
            if (!IsGuardianExist(input.CaHash, guardian)) continue;
            //Check the verifier signature and data of the guardian to be approved.
            var isApproved = CheckVerifierSignatureAndData(guardian);
            if (isApproved)
            {
                guardianApprovedAmount++;
            }
        }

        //Whether the approved guardians count is satisfied.
        IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count, guardianApprovedAmount,
            holderInfo.JudgementStrategy);

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


        Context.Fire(new GuardianAdded
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
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

        var guardianApprovedAmount = 0;
        var guardianApprovedList = input.GuardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardian in guardianApprovedList)
        {
            Assert(
                !(guardian.Type == toRemoveGuardian.Type &&
                  guardian.IdentifierHash == toRemoveGuardian.IdentifierHash &&
                  guardian.VerificationInfo.Id == toRemoveGuardian.VerifierId),
                "Guardian approved list contains to removed guardian.");
            //Whether the guardian exists in the holder info.
            if (!IsGuardianExist(input.CaHash, guardian)) continue;
            //Check the verifier signature and data of the guardian to be approved.
            var isApproved = CheckVerifierSignatureAndData(guardian);
            if (isApproved)
            {
                guardianApprovedAmount++;
            }
        }

        //Whether the approved guardians count is satisfied.
        IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1), guardianApprovedAmount,
            holderInfo.JudgementStrategy);

        State.HolderInfoMap[input.CaHash].GuardianList?.Guardians.Remove(toRemoveGuardian);

        if (State.LoginGuardianMap[toRemoveGuardian.IdentifierHash][toRemoveGuardian.VerifierId] != null)
        {
            State.LoginGuardianMap[toRemoveGuardian.IdentifierHash].Remove(toRemoveGuardian.VerifierId);
        }

        Context.Fire(new GuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
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

        var preGuardian = existPreGuardian.Clone();

        //Check verifier id is exist.
        Assert(State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v =>
            v.Id == input.GuardianToUpdateNew.VerificationInfo.Id) != null, "Verifier is not exist.");

        var guardianApprovedAmount = 0;
        var guardianApprovedList = input.GuardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardian in guardianApprovedList)
        {
            Assert(
                !(guardian.Type == existPreGuardian.Type &&
                  guardian.IdentifierHash == existPreGuardian.IdentifierHash &&
                  guardian.VerificationInfo.Id == existPreGuardian.VerifierId),
                "Guardian approved list contains to updated guardian.");
            //Whether the guardian exists in the holder info.
            if (!IsGuardianExist(input.CaHash, guardian)) continue;
            //Check the verifier signature and data of the guardian to be approved.
            var isApproved = CheckVerifierSignatureAndData(guardian);
            if (isApproved)
            {
                guardianApprovedAmount++;
            }
        }

        //Whether the approved guardians count is satisfied.
        IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count.Sub(1), guardianApprovedAmount,
            holderInfo.JudgementStrategy);

        existPreGuardian.VerifierId = input.GuardianToUpdateNew?.VerificationInfo.Id;

        if (State.LoginGuardianMap[preGuardian.IdentifierHash][preGuardian.VerifierId] != null)
        {
            State.LoginGuardianMap[preGuardian.IdentifierHash].Remove(preGuardian.VerifierId);
            State.LoginGuardianMap[existPreGuardian.IdentifierHash][existPreGuardian.VerifierId] = input.CaHash;
        }

        Context.Fire(new GuardianUpdated
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
            GuardianUpdatedPre = preGuardian,
            GuardianUpdatedNew = existPreGuardian
        });

        return new Empty();
    }
}