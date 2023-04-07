using System.Linq;
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
        Assert(CheckHashInput(input!.LoginGuardianIdentifierHash), "invalid input login guardian");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(input.ManagerInfo!.ExtraData != null && !string.IsNullOrEmpty(input.ManagerInfo.ExtraData),
            "invalid input extraData");
        Assert(input.ManagerInfo.Address != null, "invalid input address");
        var loginGuardianIdentifierHash = input.LoginGuardianIdentifierHash;
        var caHash = State.GuardianMap[loginGuardianIdentifierHash];

        Assert(caHash != null, "CA Holder does not exist.");

        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null, $"Not found holderInfo by caHash: {caHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {caHash}");
        var guardians = holderInfo.GuardianList!.Guardians;

        Assert(input.GuardiansApproved.Count > 0, "invalid input Guardians Approved");

        var guardianApprovedAmount = 0;
        var guardianApprovedList = input.GuardiansApproved
            .DistinctBy(g => $"{g.Type}{g.IdentifierHash}{g.VerificationInfo.Id}")
            .ToList();
        foreach (var guardian in guardianApprovedList)
        {
            //Whether the guardian exists in the holder info.
            if (!IsGuardianExist(caHash, guardian)) continue;
            //Check the verifier signature and data of the guardian to be approved.
            var isApproved = CheckVerifierSignatureAndData(guardian);
            if (isApproved)
            {
                guardianApprovedAmount++;
            }
        }

        IsJudgementStrategySatisfied(guardians.Count, guardianApprovedAmount,
            holderInfo.JudgementStrategy);

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo exists");

        State.HolderInfoMap[caHash].ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(caHash, input.ManagerInfo);

        SetContractDelegator(input.ManagerInfo);

        Context.Fire(new ManagerInfoSocialRecovered()
        {
            CaHash = caHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(caHash),
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        return new Empty();
    }

    public override Empty AddManagerInfo(AddManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        //Assert(Context.Sender.Equals(input.ManagerInfo.Address), "No permission to add");

        var holderInfo = State.HolderInfoMap[input.CaHash];
        Assert(holderInfo != null, $"Not found holderInfo by caHash: {input.CaHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {input.CaHash}");

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo address exists");

        holderInfo.ManagerInfos.Add(input.ManagerInfo);
        SetDelegator(input.CaHash, input.ManagerInfo);

        SetContractDelegator(input.ManagerInfo);

        Context.Fire(new ManagerInfoAdded
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
            Manager = input.ManagerInfo.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        return new Empty();
    }

    public override Empty RemoveManagerInfo(RemoveManagerInfoInput input)
    {
        Assert(input != null, "invalid input");
        CheckManagerInfoInput(input!.CaHash, input.ManagerInfo);
        //Assert(Context.Sender.Equals(input.Manager.Address), "No permission to remove");

        var holderInfo = State.HolderInfoMap[input.CaHash];
        Assert(holderInfo != null, $"Not found holderInfo by caHash: {input.CaHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {input.CaHash}");

        // Manager does not exist
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        if (managerInfo == null)
        {
            return new Empty();
        }

        holderInfo.ManagerInfos.Remove(managerInfo);
        RemoveDelegator(input.CaHash, managerInfo);

        Context.Fire(new ManagerInfoRemoved
        {
            CaHash = input.CaHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
            Manager = managerInfo.Address,
            ExtraData = managerInfo.ExtraData
        });

        return new Empty();
    }

    private void CheckManagerInfoInput(Hash hash, ManagerInfo managerInfo)
    {
        Assert(hash != null, "invalid input CaHash");
        CheckManagerInfoPermission(hash, Context.Sender);
        Assert(managerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrEmpty(managerInfo!.ExtraData) && managerInfo.Address != null, "invalid input managerInfo");
    }

    public override Empty ManagerForwardCall(ManagerForwardCallInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        Assert(input.ContractAddress != null && !string.IsNullOrEmpty(input.MethodName),
            "Invalid input.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Context.SendVirtualInline(input.CaHash, input.ContractAddress, input.MethodName, input.Args);
        return new Empty();
    }

    public override Empty ManagerTransfer(ManagerTransferInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.To != null && !string.IsNullOrEmpty(input.Symbol), "Invalid input.");
        Context.SendVirtualInline(input.CaHash, State.TokenContract.Value,
            nameof(State.TokenContract.Transfer),
            new TransferInput
            {
                To = input.To,
                Amount = input.Amount,
                Symbol = input.Symbol,
                Memo = input.Memo
            }.ToByteString());
        return new Empty();
    }

    public override Empty ManagerTransferFrom(ManagerTransferFromInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.From != null && input.To != null && !string.IsNullOrEmpty(input.Symbol),
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
            }.ToByteString());
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