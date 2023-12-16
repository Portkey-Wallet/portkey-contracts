using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty RegisterProjectDelegatee(RegisterProjectDelegateeInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.ProjectName), "Invalid project name.");
        Assert(input.Salts.Count > 0, "Input salts is empty.");
        Assert(input.Salts.Count <= CAContractConstants.DelegateeListMaxCount, "Exceed salts max count " + CAContractConstants.DelegateeListMaxCount);
        var projectHash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.ProjectName), Context.TransactionId, Context.PreviousBlockHash);
        Assert(State.ProjectDelegateInfo[projectHash] == null, "Project Hash existed.");
        var projectDelegateInfo = new ProjectDelegateInfo
        {
            ProjectController = Context.Sender
        };
        var distinctSalt = input.Salts.DistinctBy(s => s).ToList();
        foreach (var salt in distinctSalt)
        {
            projectDelegateInfo.DelegateeHashList.Add(HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(salt), projectHash));
        }

        State.ProjectDelegateInfo[projectHash] = projectDelegateInfo;
        return new Empty();
    }

    public override Empty AddProjectDelegateeList(AddProjectDelegateeListInput input)
    {
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        Assert(input.Salts.Count > 0, "Input salts is empty.");
        Assert(input.Salts.Count <= CAContractConstants.DelegateeListMaxCount, "Exceed salts max count " + CAContractConstants.DelegateeListMaxCount);
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null, "ProjectDelegateInfo not existed.");
        Assert(Context.Sender == projectDelegateInfo.ProjectController, "No permission.");
        var distinctSalt = input.Salts.DistinctBy(s => s).ToList();
        var addDelegateeHashList = new RepeatedField<Hash>();
        foreach (var salt in distinctSalt)
        {
            addDelegateeHashList.Add(HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(salt), input.ProjectHash));
        }
        projectDelegateInfo.DelegateeHashList.AddRange(addDelegateeHashList);
        return new Empty();
    }

    public override Empty RemoveProjectDelegateeList(RemoveProjectDelegateeListInput input)
    {
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        Assert(input.DelegateeHashList.Count > 0, "Invalid delegatee hash list.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null, "ProjectDelegateInfo not existed.");
        Assert(Context.Sender == projectDelegateInfo.ProjectController, "No permission.");
        var removeHashList = input.DelegateeHashList.Intersect(projectDelegateInfo.DelegateeHashList).ToList();
        if (removeHashList.Count == 0)
        {
            return new Empty();
        }

        foreach (var hash in removeHashList)
        {
            projectDelegateInfo.DelegateeHashList.Remove(hash);
        }
        return new Empty();
    }

    public override Empty SetProjectDelegateController(SetProjectDelegateControllerInput input)
    {
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        Assert(!input.ProjectController.Value.IsNullOrEmpty(), "Invalid project controller address.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null, "ProjectDelegateInfo not existed.");
        Assert(Context.Sender == projectDelegateInfo.ProjectController, "No permission.");
        projectDelegateInfo.ProjectController = input.ProjectController;
        return new Empty();
    }

    public override Empty SetProjectDelegateSigner(SetProjectDelegateSignerInput input)
    {
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        Assert(!input.Signer.Value.IsNullOrEmpty(), "Invalid signer address.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null, "ProjectDelegateInfo not existed.");
        Assert(Context.Sender == projectDelegateInfo.ProjectController, "No permission.");
        projectDelegateInfo.Signer = input.Signer;
        return new Empty();
    }

    public override Empty WithdrawProjectDelegateeToken(WithdrawProjectDelegateeTokenInput input)
    {
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        Assert(IsValidHash(input.DelegateeHash), "Invalid delegatee hash.");
        Assert(input.Amount > 0, "Invalid amount.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null, "ProjectDelegateInfo not existed.");
        Assert(Context.Sender == projectDelegateInfo.ProjectController, "No permission.");
        Assert(projectDelegateInfo.DelegateeHashList.FirstOrDefault(d => d == input.DelegateeHash) != null, "Delegatee hash not existed.");
        Context.SendVirtualInline(input.DelegateeHash, State.TokenContract.Value, nameof(State.TokenContract.Transfer),
            new TransferInput()
            {
                To = Context.Sender,
                Amount = input.Amount,
                Symbol = CAContractConstants.ELFTokenSymbol,
                Memo = "Withdraw Project Delegatee Token"
            }.ToByteString());
        return new Empty();
    }

    public override ProjectDelegateInfo GetProjectDelegatee(Hash input)
    {
        return State.ProjectDelegateInfo[input];
    }

    public override Empty SetCaProjectDelegateHash(Hash input)
    {
        Assert(IsValidHash(input), "Invalid input.");
        Assert(State.ServerControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        State.CaProjectDelegateHash.Value = input;
        return new Empty();
    }
}