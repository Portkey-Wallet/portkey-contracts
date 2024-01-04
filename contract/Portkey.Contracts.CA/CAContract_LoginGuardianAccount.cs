using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    // Set a Guardian for login, if already set, return ture
    public override Empty SetGuardianForLogin(SetGuardianForLoginInput input)
    {
        Assert(input != null, "input should not be null");
        Assert(input!.CaHash != null, "CaHash should not be null");
        // Guardian should be valid, not null, and be with non-null Value
        Assert(input.GuardianToSetLogin != null && input.GuardiansApproved.Count > 0, "Invalid input.");
        var loginGuardian = input.GuardianToSetLogin;
        Assert(IsValidHash(loginGuardian.IdentifierHash), "Guardian IdentifierHash should not be null");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var isOccupied = CheckLoginGuardianIsNotOccupied(loginGuardian, input.CaHash);

        Assert(isOccupied != CAContractConstants.LoginGuardianIsOccupiedByOthers,
            $"The login guardian --{input.GuardianToSetLogin!.IdentifierHash}-- is occupied by others!");

        // for idempotent
        if (isOccupied == CAContractConstants.LoginGuardianIsYours)
        {
            return new Empty();
        }

        Assert(isOccupied == CAContractConstants.LoginGuardianIsNotOccupied,
            "Internal error, how can it be?");

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == loginGuardian.VerificationInfo.Id && t.IdentifierHash == loginGuardian.IdentifierHash &&
            t.Type == loginGuardian.Type);

        if (guardian == null)
        {
            return new Empty();
        }

        input.GuardiansApproved.Add(input.GuardianToSetLogin);
        var guardianApprovedAmount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.SetLoginAccount).ToLower());
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
            holderJudgementStrategy), "JudgementStrategy validate failed");

        guardian.IsLoginGuardian = true;

        State.LoginGuardianMap[loginGuardian.IdentifierHash][loginGuardian.VerificationInfo.Id] = input.CaHash;

        State.GuardianMap[loginGuardian.IdentifierHash] = input.CaHash;

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        UpgradeProjectDelegatee(caAddress, holderInfo.ManagerInfos);

        Context.Fire(new LoginGuardianAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            LoginGuardian = guardian,
            Manager = Context.Sender
        });

        return new Empty();
    }

    private int GetGuardianApprovedCount(Hash cahHash, RepeatedField<GuardianInfo> guardianApproved, string methodName)
    {
        var guardianApprovedCount = 0;
        var guardianApprovedList = guardianApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardianInfo in guardianApprovedList)
        {
            if (!IsGuardianExist(cahHash, guardianInfo)) continue;
            var isApproved = CheckVerifierSignatureAndData(guardianInfo, methodName, cahHash);
            if (!isApproved) continue;
            guardianApprovedCount++;
        }

        return guardianApprovedCount;
    }

    // Unset a Guardian for login, if already unset, return ture
    public override Empty UnsetGuardianForLogin(UnsetGuardianForLoginInput input)
    {
        Assert(input != null, "Invalid input");
        Assert(input!.CaHash != null, "CaHash can not be null");
        // Guardian should be valid, not null, and be with non-null Value
        Assert(input.GuardianToUnsetLogin != null && input.GuardiansApproved.Count > 0, "Invalid input.");
        var loginGuardian = input.GuardianToUnsetLogin;
        Assert(IsValidHash(loginGuardian.IdentifierHash), "Guardian IdentifierHash should not be null");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        // if CAHolder only have one LoginGuardian,not Allow Unset;
        Assert(holderInfo.GuardianList!.Guardians.Count(g => g.IsLoginGuardian) > 1,
            "only one LoginGuardian,can not be Unset");

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == loginGuardian.VerificationInfo.Id && t.IdentifierHash == loginGuardian.IdentifierHash &&
            t.Type == loginGuardian.Type);

        if (guardian == null || !guardian.IsLoginGuardian)
        {
            return new Empty();
        }


        input.GuardiansApproved.Add(input.GuardianToUnsetLogin);
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.UnSetLoginAccount).ToLower());
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
            holderJudgementStrategy), "JudgementStrategy validate failed");

        guardian.IsLoginGuardian = false;

        State.LoginGuardianMap[loginGuardian.IdentifierHash].Remove(loginGuardian.VerificationInfo.Id);
        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        Context.Fire(new LoginGuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            LoginGuardian = guardian,
            Manager = Context.Sender
        });


        // not found, or removed and be registered by others later, quit to be idempotent
        if (holderInfo.GuardianList.Guardians.Where(g =>
                g.IdentifierHash == loginGuardian.IdentifierHash).All(g => !g.IsLoginGuardian))
        {
            State.GuardianMap.Remove(loginGuardian.IdentifierHash);
            Context.Fire(new LoginGuardianUnbound
            {
                CaHash = input.CaHash,
                CaAddress = caAddress,
                LoginGuardianIdentifierHash = loginGuardian.IdentifierHash,
                Manager = Context.Sender
            });
        }

        UpgradeProjectDelegatee(caAddress, holderInfo.ManagerInfos);

        return new Empty();
    }

    private int CheckLoginGuardianIsNotOccupied(GuardianInfo guardian, Hash caHash)
    {
        var result = State.LoginGuardianMap[guardian.IdentifierHash][guardian.VerificationInfo.Id];
        if (result == null)
        {
            return CAContractConstants.LoginGuardianIsNotOccupied;
        }

        return result == caHash
            ? CAContractConstants.LoginGuardianIsYours
            : CAContractConstants.LoginGuardianIsOccupiedByOthers;
    }
}