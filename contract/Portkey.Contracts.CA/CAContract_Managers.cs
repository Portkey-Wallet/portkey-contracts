using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    // For SocialRecovery
    public override Empty SocialRecovery(SocialRecoveryInput input)
    {
        Assert(input != null, "invalid input");
        Assert(IsValidHash(input!.LoginGuardianIdentifierHash), "invalid input login guardian");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(input.ManagerInfo!.ExtraData), "invalid input extraData");
        Assert(input.ManagerInfo.Address != null, "invalid input address");
        var loginGuardianIdentifierHash = input.LoginGuardianIdentifierHash;
        var caHash = State.GuardianMap[loginGuardianIdentifierHash];

        Assert(caHash != null, "CA Holder does not exist.");

        var holderInfo = GetHolderInfoByCaHash(caHash);
        AssertCreateChain(holderInfo);
        var guardians = holderInfo.GuardianList!.Guardians;

        Assert(input.GuardiansApproved.Count > 0, "invalid input Guardians Approved");

        var guardianApprovedCount = GetGuardianApprovedCount(caHash, input.GuardiansApproved,
            nameof(OperationType.SocialRecovery).ToLower());

        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(guardians.Count, guardianApprovedCount,
            holderInfo.JudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo exists");
        Assert(holderInfo.ManagerInfos.Count < CAContractConstants.ManagerMaxCount,
            "The amount of ManagerInfos out of limit");

        var caAddress = Context.ConvertVirtualAddressToContractAddress(caHash);

        State.HolderInfoMap[caHash].ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(caHash, input.ManagerInfo);

        Context.Fire(new ManagerInfoSocialRecovered()
        {
            CaHash = caHash,
            CaAddress = caAddress,
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });
        var isValidReferralCode = IsValidInviteCode(input.ReferralCode);
        var isValidProjectCode = IsValidInviteCode(input.ProjectCode);
        if (isValidProjectCode || isValidReferralCode)
        {
            Context.Fire(new Invited
            {
                CaHash = caHash,
                ContractAddress = Context.Self,
                MethodName = nameof(SocialRecovery),
                ProjectCode = isValidProjectCode ? input.ProjectCode : "",
                ReferralCode = isValidReferralCode ? input.ReferralCode : ""
            });
        }

        return new Empty();
    }

    public override Empty AddManagerInfo(AddManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        //Assert(Context.Sender.Equals(input.ManagerInfo.Address), "No permission to add");

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);
        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo address exists");
        Assert(holderInfo.ManagerInfos.Count < CAContractConstants.ManagerMaxCount,
            "The amount of ManagerInfos out of limit");

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        holderInfo.ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(input.CaHash, input.ManagerInfo);

        Context.Fire(new ManagerInfoAdded
        {
            CaHash = input.CaHash,
            CaAddress = caAddress,
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        return new Empty();
    }

    // For manager remove itself
    public override Empty RemoveManagerInfo(RemoveManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        Assert(IsValidHash(input!.CaHash), "invalid input caHash");
        CheckManagerInfoPermission(input!.CaHash, Context.Sender);

        return RemoveManager(input.CaHash, Context.Sender);
    }

    // For manager remove other
    public override Empty RemoveOtherManagerInfo(RemoveOtherManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        Assert(!Context.Sender.Equals(input.ManagerInfo.Address), "One should not remove itself");
        Assert(input.GuardiansApproved != null && input.GuardiansApproved.Count > 0, "invalid input guardiansApproved");

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);

        var guardianApprovedCount = GetGuardianApprovedCount(input.CaHash, input.GuardiansApproved,
            nameof(OperationType.RemoveOtherManagerInfo).ToLower());

        //Whether the approved guardians count is satisfied.
        var isJudgementStrategySatisfied = IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count,
            guardianApprovedCount, holderInfo.JudgementStrategy);
        return !isJudgementStrategySatisfied ? new Empty() : RemoveManager(input.CaHash, input.ManagerInfo.Address);
    }

    private Empty RemoveManager(Hash caHash, Address address)
    {
        var holderInfo = GetHolderInfoByCaHash(caHash);
        AssertCreateChain(holderInfo);

        // Manager does not exist
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, address);
        if (managerInfo == null)
        {
            return new Empty();
        }

        var caAddress = Context.ConvertVirtualAddressToContractAddress(caHash);

        holderInfo.ManagerInfos.Remove(managerInfo);
        RemoveDelegator(caHash, managerInfo);

        Context.Fire(new ManagerInfoRemoved
        {
            CaHash = caHash,
            CaAddress = caAddress,
            Manager = managerInfo.Address,
            ExtraData = managerInfo.ExtraData
        });

        return new Empty();
    }

    public override Empty UpdateManagerInfos(UpdateManagerInfosInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfosInput(input!.CaHash, input.ManagerInfos);

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);
        AssertCreateChain(holderInfo);

        var managerInfosToUpdate = input.ManagerInfos.Distinct().ToList();

        var managerInfoList = holderInfo.ManagerInfos;

        var caAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash);

        foreach (var manager in managerInfosToUpdate)
        {
            var managerToUpdate = managerInfoList.FirstOrDefault(m => m.Address == manager.Address);
            if (managerToUpdate == null || managerToUpdate.ExtraData == manager.ExtraData)
            {
                continue;
            }

            managerToUpdate.ExtraData = manager.ExtraData;

            Context.Fire(new ManagerInfoUpdated
            {
                CaHash = input.CaHash,
                CaAddress = caAddress,
                Manager = managerToUpdate.Address,
                ExtraData = managerToUpdate.ExtraData
            });
        }

        return new Empty();
    }

    private void CheckManagerInfoInput(Hash hash, ManagerInfo managerInfo)
    {
        Assert(hash != null, "invalid input CaHash");
        CheckManagerInfoPermission(hash, Context.Sender);
        Assert(managerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(managerInfo!.ExtraData) && managerInfo.Address != null,
            "invalid input managerInfo");
    }

    private void CheckManagerInfosInput(Hash hash, RepeatedField<ManagerInfo> managerInfos)
    {
        Assert(hash != null, "invalid input CaHash");
        CheckManagerInfoPermission(hash, Context.Sender);
        Assert(managerInfos != null && managerInfos.Count > 0, "invalid input managerInfo");

        foreach (var managerInfo in managerInfos!)
        {
            Assert(!string.IsNullOrWhiteSpace(managerInfo!.ExtraData) && managerInfo.Address != null,
                "invalid input managerInfo");
        }
    }

    public override Empty ManagerForwardCall(ManagerForwardCallInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        Assert(input.ContractAddress != null && !string.IsNullOrWhiteSpace(input.MethodName),
            "Invalid input.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(!State.ForbiddenForwardCallContractMethod[input.ContractAddress][input.MethodName.ToLower()],
            $"Does not have permission for {input.MethodName}.");
        if (input.MethodName == nameof(State.TokenContract.Transfer) &&
            input.ContractAddress == State.TokenContract.Value)
        {
            var transferInput = TransferInput.Parser.ParseFrom(input.Args);
            UpdateDailyTransferredAmount(input.CaHash, input.GuardiansApproved, transferInput.Symbol,
                transferInput.Amount);
        }

        Context.SendVirtualInline(input.CaHash, input.ContractAddress, input.MethodName, input.Args, true);
        return new Empty();
    }

    public override Empty ManagerTransfer(ManagerTransferInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.To != null && !string.IsNullOrWhiteSpace(input.Symbol), "Invalid input.");
        UpdateDailyTransferredAmount(input.CaHash, input.GuardiansApproved, input.Symbol, input.Amount);
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value, nameof(State.TokenContract.Transfer),
            new TransferInput
            {
                To = input.To,
                Amount = input.Amount,
                Symbol = input.Symbol,
                Memo = input.Memo
            }.ToByteString(), true);
        return new Empty();
    }

    public override Empty ManagerTransferFrom(ManagerTransferFromInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.From != null && input.To != null && !string.IsNullOrWhiteSpace(input.Symbol),
            "Invalid input.");
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.TransferFrom),
            new TransferFromInput
            {
                From = input.From,
                To = input.To,
                Amount = input.Amount,
                Symbol = input.Symbol,
                Memo = input.Memo
            }.ToByteString(), true);
        return new Empty();
    }

    public override Empty ManagerApprove(ManagerApproveInput input)
    {
        Assert(input != null, "invalid input");
        Assert(input.CaHash != null, "CA hash is null.");
        Assert(input.Spender != null && !input.Spender.Value.IsNullOrEmpty(), "Invalid input address.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        GuardianApprovedCheck(input.CaHash, input.GuardiansApproved, OperationType.Approve,
            nameof(OperationType.Approve).ToLower());
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.Approve),
            new ApproveInput
            {
                Spender = input.Spender,
                Amount = input.Amount,
                Symbol = input.Symbol,
            }.ToByteString(), true);
        Context.Fire(new ManagerApproved
        {
            CaHash = input.CaHash,
            Spender = input.Spender,
            Amount = input.Amount,
            Symbol = input.Symbol,
        });
        return new Empty();
    }

    public override Empty SetForbiddenForwardCallContractMethod(SetForbiddenForwardCallContractMethodInput input)
    {
        Assert(input != null && input.Address != null, "Invalid input");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrWhiteSpace(input.MethodName), "MethodName cannot be empty");
        State.ForbiddenForwardCallContractMethod[input.Address][input.MethodName.ToLower()] = input.Forbidden;
        Context.Fire(new ForbiddenForwardCallContractMethodChanged
        {
            MethodName = input.MethodName,
            Address = input.Address,
            Forbidden = input.Forbidden
        });
        return new Empty();
    }

    private void CheckManagerInfoPermission(Hash caHash, Address address)
    {
        Assert(State.HolderInfoMap[caHash] != null, $"CA holder is null.CA hash:{caHash}");
        Assert(State.HolderInfoMap[caHash].ManagerInfos.Any(m => m.Address == address), "No permission.");
    }

    private ManagerInfo FindManagerInfo(RepeatedField<ManagerInfo> managerInfos, Address address)
    {
        return managerInfos.FirstOrDefault(s => s.Address == address);
    }
}