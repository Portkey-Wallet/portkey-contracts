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
        var checkGuardiansApproved = State.LoginGuardianCheckGuardianApprovedEnabled.Value || input.GuardianToSetLogin != null;
        if (checkGuardiansApproved)
        {
            Assert(input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty.");
        }
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

        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var isOccupied = CheckLoginGuardianIsNotOccupied(loginGuardian, input.CaHash);

        Assert(isOccupied != LoginGuardianStatus.IsOccupiedByOthers,
            $"The login guardian --{loginGuardian!.IdentifierHash}-- is occupied by others!");

        // for idempotent
        if (isOccupied == LoginGuardianStatus.IsYours)
        {
            return new Empty();
        }

        Assert(isOccupied == LoginGuardianStatus.IsOccupiedByOthers,
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
            var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName);
            var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
            Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
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

    private int GetGuardianApprovedCount(Hash cahHash, RepeatedField<GuardianInfo> guardianApproved, string methodName)
    {
        var guardianApprovedCount = 0;
        var guardianApprovedList = guardianApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardianInfo in guardianApprovedList)
        {
            if (!IsGuardianExist(cahHash, guardianInfo)) continue;
            var isApproved = CheckVerifierSignatureAndDataCompatible(guardianInfo, methodName, cahHash);
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
        var checkGuardiansApproved = State.LoginGuardianCheckGuardianApprovedEnabled.Value || input.GuardianToUnsetLogin != null;
        if (checkGuardiansApproved)
        {
            Assert(input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty.");
        }
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
            var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName);
            var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
            Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
                holderJudgementStrategy), "JudgementStrategy validate failed");
        }

        guardian.IsLoginGuardian = false;

        State.LoginGuardianMap[loginGuardian.IdentifierHash].Remove(loginGuardian.VerifierId);

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

    private LoginGuardianStatus CheckLoginGuardianIsNotOccupied(Guardian guardian, Hash caHash)
    {
        var result = State.LoginGuardianMap[guardian.IdentifierHash][guardian.VerifierId];
        if (result == null)
        {
            return LoginGuardianStatus.IsNotOccupied;
        }

        return result == caHash
            ? LoginGuardianStatus.IsYours
            : LoginGuardianStatus.IsOccupiedByOthers;
    }

    public override Empty SetLoginGuardianCheckGuardianApprovedEnabled(SetLoginGuardianCheckGuardianApprovedEnabledInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        State.LoginGuardianCheckGuardianApprovedEnabled.Value = input.LoginGuardianCheckGuardianApprovedEnabled;
        return new Empty();
    }

    public override GetLoginGuardianCheckGuardianApprovedEnabledOutput GetLoginGuardianCheckGuardianApprovedEnabled(Empty input)
    {
        return new GetLoginGuardianCheckGuardianApprovedEnabledOutput
        {
            LoginGuardianCheckGuardianApprovedEnabled = State.LoginGuardianCheckGuardianApprovedEnabled.Value
        };
    }
}