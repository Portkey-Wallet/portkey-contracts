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
        Assert(IsValidHash(input!.LoginGuardianIdentifierHash), "invalid input login guardian");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(!string.IsNullOrWhiteSpace(input.ManagerInfo!.ExtraData), "invalid input extraData");
        Assert(input.ManagerInfo.Address != null, "invalid input address");
        var loginGuardianIdentifierHash = input.LoginGuardianIdentifierHash;
        var caHash = State.GuardianMap[loginGuardianIdentifierHash];

        Assert(caHash != null, "CA Holder does not exist.");

        var holderInfo = GetHolderInfoByCaHash(caHash);
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
        Assert(holderInfo.ManagerInfos.Count < CAContractConstants.ManagerMaxCount,
            "The amount of ManagerInfos out of limit");

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

        var holderInfo = GetHolderInfoByCaHash(input.CaHash);

        // ManagerInfo exists
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, input.ManagerInfo.Address);
        Assert(managerInfo == null, $"ManagerInfo address exists");
        Assert(holderInfo.ManagerInfos.Count < CAContractConstants.ManagerMaxCount,
            "The amount of ManagerInfos out of limit");

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

        var guardianApprovedAmount = 0;
        var guardianApprovedList = input.GuardiansApproved!
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
        IsJudgementStrategySatisfied(holderInfo.GuardianList!.Guardians.Count, guardianApprovedAmount,
            holderInfo.JudgementStrategy);
        
        return RemoveManager(input.CaHash, input.ManagerInfo.Address);
    }

    private Empty RemoveManager(Hash caHash, Address address)
    {
        var holderInfo = GetHolderInfoByCaHash(caHash);

        // Manager does not exist
        var managerInfo = FindManagerInfo(holderInfo.ManagerInfos, address);
        if (managerInfo == null)
        {
            return new Empty();
        }

        holderInfo.ManagerInfos.Remove(managerInfo);
        RemoveDelegator(caHash, managerInfo);
        RemoveContractDelegator(managerInfo);

        Context.Fire(new ManagerInfoRemoved
        {
            CaHash = caHash,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(caHash),
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

        var managerInfosToUpdate = input.ManagerInfos.Distinct().ToList();

        var managerInfoList = holderInfo.ManagerInfos;

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
                CaAddress = Context.ConvertVirtualAddressToContractAddress(input.CaHash),
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
        Context.SendVirtualInline(input.CaHash, input.ContractAddress, input.MethodName, input.Args);
        return new Empty();
    }

    public override Empty ManagerTransfer(ManagerTransferInput input)
    {
        Assert(input.CaHash != null, "CA hash is null.");
        CheckManagerInfoPermission(input.CaHash, Context.Sender);
        Assert(input.To != null && !string.IsNullOrWhiteSpace(input.Symbol), "Invalid input.");
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