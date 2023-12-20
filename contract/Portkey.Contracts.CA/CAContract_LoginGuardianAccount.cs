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
        var checkGuardiansApproved = input.GuardianToSetLogin != null;
        var loginGuardian = checkGuardiansApproved
            ? new Guardian
            {
                IdentifierHash = input.GuardianToSetLogin.IdentifierHash,
                VerifierId = input.GuardianToSetLogin.VerificationInfo.Id,
                Type = input.GuardianToSetLogin.Type
            }
            : input.Guardian;
        Assert(loginGuardian != null, "Guardian should not be null");
        Assert(IsValidHash(loginGuardian.IdentifierHash), "Guardian IdentifierHash should not be null");
        Assert(!checkGuardiansApproved || input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty");

        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var isOccupied = CheckLoginGuardianIsNotOccupied(loginGuardian, input.CaHash);

        Assert(isOccupied != CAContractConstants.LoginGuardianIsOccupiedByOthers,
            $"The login guardian --{loginGuardian!.IdentifierHash}-- is occupied by others!");

        // for idempotent
        if (isOccupied == CAContractConstants.LoginGuardianIsYours)
        {
            return new Empty();
        }

        Assert(isOccupied == CAContractConstants.LoginGuardianIsNotOccupied,
            "Internal error, how can it be?");

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == loginGuardian.VerifierId && t.IdentifierHash == loginGuardian.IdentifierHash &&
            t.Type == loginGuardian.Type);

        if (guardian == null)
        {
            return new Empty();
        }

        if (checkGuardiansApproved)
        {
            var methodName = nameof(OperationType.SetLoginAccount).ToLower();
            input.GuardiansApproved.Add(input.GuardianToSetLogin);
            var guardianApprovedAmount = GetGuardianApprovedAmount(input.CaHash, input.GuardiansApproved, methodName);
            var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
            Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
                holderJudgementStrategy), "JudgementStrategy validate failed");
        }

        guardian.IsLoginGuardian = true;

        State.LoginGuardianMap[loginGuardian.IdentifierHash][loginGuardian.VerifierId] = input.CaHash;

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

    private int GetGuardianApprovedAmount(Hash cahHash, RepeatedField<GuardianInfo> guardianApproved, string methodName)
    {
        var guardianApprovedAmount = 0;
        var guardianApprovedList = guardianApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardianInfo in guardianApprovedList)
        {
            if (!IsGuardianExist(cahHash, guardianInfo)) continue;
            var isApproved = CheckVerifierSignatureAndDataCompatible(guardianInfo, methodName, cahHash);
            if (!isApproved) continue;
            guardianApprovedAmount++;
        }
        return guardianApprovedAmount;
    }

    // Unset a Guardian for login, if already unset, return ture
    public override Empty UnsetGuardianForLogin(UnsetGuardianForLoginInput input)
    {
        Assert(input != null, "Invalid input");
        Assert(input!.CaHash != null, "CaHash can not be null");
        // Guardian should be valid, not null, and be with non-null Value
        var checkGuardiansApproved = input.GuardianToUnsetLogin != null;
        var loginGuardian = checkGuardiansApproved
            ? new Guardian
            {
                IdentifierHash = input.GuardianToUnsetLogin.IdentifierHash,
                VerifierId = input.GuardianToUnsetLogin.VerificationInfo.Id,
                Type = input.GuardianToUnsetLogin.Type
            }
            : input.Guardian;
        Assert(loginGuardian != null, "Guardian should not be null");
        Assert(IsValidHash(loginGuardian.IdentifierHash), "Guardian IdentifierHash should not be null");
        Assert(!checkGuardiansApproved || input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        // if CAHolder only have one LoginGuardian,not Allow Unset;
        Assert(holderInfo.GuardianList!.Guardians.Count(g => g.IsLoginGuardian) > 1,
            "only one LoginGuardian,can not be Unset");

        // if (State.LoginGuardianMap[loginGuardian.IdentifierHash][input.Guardian.VerifierId] == null ||
        //     State.LoginGuardianMap[loginGuardian.IdentifierHash][input.Guardian.VerifierId] != input.CaHash)
        // {
        //     return new Empty();
        // }

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == loginGuardian.VerifierId && t.IdentifierHash == loginGuardian.IdentifierHash &&
            t.Type == loginGuardian.Type);

        if (guardian == null || !guardian.IsLoginGuardian)
        {
            return new Empty();
        }
        if (checkGuardiansApproved)
        {
            var methodName = nameof(OperationType.UnSetLoginAccount).ToLower();
            input.GuardiansApproved.Add(input.GuardianToUnsetLogin);
            var guardianApprovedAmount = GetGuardianApprovedAmount(input.CaHash, input.GuardiansApproved, methodName);
            var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
            Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
                holderJudgementStrategy), "JudgementStrategy validate failed");
        }

        guardian.IsLoginGuardian = false;

        State.LoginGuardianMap[loginGuardian.IdentifierHash].Remove(input.Guardian.VerifierId);

        Context.Fire(new LoginGuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
            LoginGuardian = guardian,
            Manager = Context.Sender
        });

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        
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

    private int CheckLoginGuardianIsNotOccupied(Guardian guardian, Hash caHash)
    {
        var result = State.LoginGuardianMap[guardian.IdentifierHash][guardian.VerifierId];
        if (result == null)
        {
            return CAContractConstants.LoginGuardianIsNotOccupied;
        }

        return result == caHash
            ? CAContractConstants.LoginGuardianIsYours
            : CAContractConstants.LoginGuardianIsOccupiedByOthers;
    }
}