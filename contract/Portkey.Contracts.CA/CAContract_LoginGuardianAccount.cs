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
        Assert(input.GuardianToSetLogin != null, "GuardianToSetLogin should not be null");
        Assert(input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty.");
        Assert(IsValidHash(input.GuardianToSetLogin.IdentifierHash), "Guardian IdentifierHash should not be null");

        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var isOccupied = CheckLoginGuardianIsNotOccupied(input.GuardianToSetLogin, input.CaHash);

        Assert(isOccupied != LoginGuardianStatus.IsOccupiedByOthers,
            $"The login guardian --{input.GuardianToSetLogin!.IdentifierHash}-- is occupied by others!");

        // for idempotent
        if (isOccupied == LoginGuardianStatus.IsYours)
        {
            return new Empty();
        }

        Assert(isOccupied == LoginGuardianStatus.IsNotOccupied,
            "Internal error, how can it be?");

        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == input.GuardianToSetLogin.VerificationInfo.Id && t.IdentifierHash == input.GuardianToSetLogin.IdentifierHash &&
            t.Type == input.GuardianToSetLogin.Type);

        if (guardian == null)
        {
            return new Empty();
        }

        var methodName = nameof(OperationType.SetLoginAccount).ToLower();
        input.GuardiansApproved.Add(input.GuardianToSetLogin);
        
        var operateDetails = $"{input.GuardianToSetLogin.IdentifierHash.ToHex()}_{(int)input.GuardianToSetLogin.Type}_" +
                             $"{input.GuardianToSetLogin.VerificationInfo.Id.ToHex()}";
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName, operateDetails);
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
            holderJudgementStrategy, input.CaHash), "JudgementStrategy validate failed");
        UpdateManuallySupportForZk(input.CaHash, input.GuardiansApproved, holderInfo);
        guardian.IsLoginGuardian = true;

        State.LoginGuardianMap[input.GuardianToSetLogin.IdentifierHash][input.GuardianToSetLogin.VerificationInfo.Id] = input.CaHash;

        State.GuardianMap[input.GuardianToSetLogin.IdentifierHash] = input.CaHash;
        
        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        
        Context.Fire(new LoginGuardianAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            LoginGuardian = guardian,
            Manager = Context.Sender,
            Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
        });

        return new Empty();
    }

    private int GetGuardianApprovedCount(Hash caHash, RepeatedField<GuardianInfo> guardianApproved, string methodName,
        string operationDetails = null)
    {
        var guardianApprovedList = guardianApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();

        return guardianApprovedList.Count(guardianInfo => IsApprovedGuardian(caHash, methodName, operationDetails, guardianInfo));
    }

    private bool IsApprovedGuardian(Hash caHash, string methodName, string operationDetails, GuardianInfo approvedGuardian)
    {
        if (!IsGuardianExist(caHash, approvedGuardian))
            return false;
        return CanZkLoginExecute(approvedGuardian) 
            ? CheckZkLoginVerifierAndData(approvedGuardian, caHash) 
            : CheckVerifierSignatureAndData(approvedGuardian, methodName, caHash, operationDetails);
    }

    // Unset a Guardian for login, if already unset, return ture
    public override Empty UnsetGuardianForLogin(UnsetGuardianForLoginInput input)
    {
        Assert(input != null, "Invalid input");
        Assert(input!.CaHash != null, "CaHash can not be null");
        // Guardian should be valid, not null, and be with non-null Value
        Assert(input.GuardianToUnsetLogin != null, "GuardianToUnsetLogin should not be null");
        Assert(input.GuardiansApproved.Count > 0, "GuardiansApproved should not be empty.");
        Assert(IsValidHash(input.GuardianToUnsetLogin.IdentifierHash), "Guardian IdentifierHash should not be null");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        // if CAHolder only have one LoginGuardian,not Allow Unset;
        Assert(holderInfo.GuardianList!.Guardians.Count(g => g.IsLoginGuardian) > 1,
            "only one LoginGuardian,can not be Unset");
        
        var guardian = holderInfo.GuardianList!.Guardians.FirstOrDefault(t =>
            t.VerifierId == input.GuardianToUnsetLogin.VerificationInfo.Id && t.IdentifierHash == input.GuardianToUnsetLogin.IdentifierHash &&
            t.Type == input.GuardianToUnsetLogin.Type);

        if (guardian == null || !guardian.IsLoginGuardian)
        {
            return new Empty();
        }

        var methodName = nameof(OperationType.UnSetLoginAccount).ToLower();
        input.GuardiansApproved.Add(input.GuardianToUnsetLogin);
        var operateDetails = $"{input.GuardianToUnsetLogin.IdentifierHash.ToHex()}_{(int)input.GuardianToUnsetLogin.Type}_" +
                             $"{input.GuardianToUnsetLogin.VerificationInfo.Id.ToHex()}";
        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved, methodName, operateDetails);
        var holderJudgementStrategy = holderInfo.JudgementStrategy ?? Strategy.DefaultStrategy();
        Assert(IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedCount,
            holderJudgementStrategy, input.CaHash), "JudgementStrategy validate failed");
        UpdateManuallySupportForZk(input.CaHash, input.GuardiansApproved, holderInfo);
        guardian.IsLoginGuardian = false;

        State.LoginGuardianMap[input.GuardianToUnsetLogin.IdentifierHash].Remove(input.GuardianToUnsetLogin.VerificationInfo.Id);

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);
        Context.Fire(new LoginGuardianRemoved
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            LoginGuardian = guardian,
            Manager = Context.Sender,
            Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
        });

        

        // not found, or removed and be registered by others later, quit to be idempotent
        if (holderInfo.GuardianList.Guardians.Where(g =>
                g.IdentifierHash == input.GuardianToUnsetLogin.IdentifierHash).All(g => !g.IsLoginGuardian))
        {
            State.GuardianMap.Remove(input.GuardianToUnsetLogin.IdentifierHash);
            Context.Fire(new LoginGuardianUnbound
            {
                CaHash = input.CaHash,
                CaAddress = caAddress,
                LoginGuardianIdentifierHash = input.GuardianToUnsetLogin.IdentifierHash,
                Manager = Context.Sender,
                Platform = GetPlatformFromCurrentSender(input.CaHash, holderInfo)
            });
        }
        
        return new Empty();
    }

    private LoginGuardianStatus CheckLoginGuardianIsNotOccupied(GuardianInfo guardian, Hash caHash)
    {
        var result = State.LoginGuardianMap[guardian.IdentifierHash][guardian.VerificationInfo.Id];
        if (result == null)
        {
            return LoginGuardianStatus.IsNotOccupied;
        }

        return result == caHash
            ? LoginGuardianStatus.IsYours
            : LoginGuardianStatus.IsOccupiedByOthers;
    }
}