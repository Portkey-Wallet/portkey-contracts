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
        Assert(input.Guardian != null, "Guardian should not be null");
        Assert(IsValidHash(input.Guardian!.IdentifierHash), "Guardian IdentifierHash should not be null");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        var loginGuardian = input.Guardian;

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
            t.VerifierId == input.Guardian.VerifierId && t.IdentifierHash == input.Guardian.IdentifierHash &&
            t.Type == input.Guardian.Type);

        if (guardian == null)
        {
            return new Empty();
        }

        if (State.SetUnsetLoginGuardianCheckGuardianApprovedEnabled.Value || (input.GuardianToSetLogin != null && input.GuardiansApproved.Count > 0) )
        {
            var guardianToSetLogin = input.GuardianToSetLogin;
            Assert(loginGuardian.IdentifierHash == guardianToSetLogin.IdentifierHash && loginGuardian.VerifierId == guardianToSetLogin.VerificationInfo.Id &&
                   loginGuardian.Type == guardianToSetLogin.Type, "Input guardian not equal guardianToSetLogin.");
            var methodName = nameof(OperationType.SetLoginAccount).ToLower();
            //Check the verifier signature and data of the guardian to be added.
            if (!CheckVerifierSignatureAndDataCompatible(input.GuardianToSetLogin, methodName, input.CaHash))
            {
                return new Empty();
            }

            var guardianApprovedAmount = GetGuardianApprovedAmount(input.CaHash, input.GuardiansApproved, methodName);
            var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
            Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
                holderJudgementStrategy), "JudgementStrategy validate failed");
        }

        guardian.IsLoginGuardian = true;

        State.LoginGuardianMap[loginGuardian.IdentifierHash][loginGuardian.VerifierId] = input.CaHash;

        State.GuardianMap[loginGuardian.IdentifierHash] = input.CaHash;
        
        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        UpgradeSecondaryDelegatee(caAddress, holderInfo.ManagerInfos);
        
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
        Assert(input.Guardian != null, "Guardian can not be null");
        Assert(IsValidHash(input.Guardian!.IdentifierHash), "Guardian IdentifierHash can not be null");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        // if CAHolder only have one LoginGuardian,not Allow Unset;
        Assert(holderInfo.GuardianList!.Guardians.Count(g => g.IsLoginGuardian) > 1,
            "only one LoginGuardian,can not be Unset");
        var loginGuardian = input.Guardian;

        // if (State.LoginGuardianMap[loginGuardian.IdentifierHash][input.Guardian.VerifierId] == null ||
        //     State.LoginGuardianMap[loginGuardian.IdentifierHash][input.Guardian.VerifierId] != input.CaHash)
        // {
        //     return new Empty();
        // }

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == input.Guardian.VerifierId && t.IdentifierHash == input.Guardian.IdentifierHash &&
            t.Type == input.Guardian.Type);

        if (guardian == null || !guardian.IsLoginGuardian)
        {
            return new Empty();
        }
        if (State.SetUnsetLoginGuardianCheckGuardianApprovedEnabled.Value || (input.GuardianToUnsetLogin != null && input.GuardiansApproved.Count > 0) )
        {
            var guardianToSetLogin = input.GuardianToUnsetLogin;
            Assert(loginGuardian.IdentifierHash == guardianToSetLogin.IdentifierHash && loginGuardian.VerifierId == guardianToSetLogin.VerificationInfo.Id &&
                   loginGuardian.Type == guardianToSetLogin.Type, "Input guardian not equal guardianToUnsetLogin.");
            var methodName = nameof(OperationType.UnSetLoginAccount).ToLower();
            //Check the verifier signature and data of the guardian to be added.
            if (!CheckVerifierSignatureAndDataCompatible(input.GuardianToUnsetLogin, methodName, input.CaHash))
            {
                return new Empty();
            }

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
        
        UpgradeSecondaryDelegatee(caAddress, holderInfo.ManagerInfos);
        
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