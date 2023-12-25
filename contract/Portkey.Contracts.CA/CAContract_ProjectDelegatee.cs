using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty WithdrawDelegationFeeToken(WithdrawDelegationFeeTokenInput input)
    {
        Assert(input.Amount > 0 && !string.IsNullOrWhiteSpace(input.Symbol), "Invalid input");
        Assert(Context.Sender == State.Admin.Value, "No permission");
        State.TokenContract.Transfer.Send(new TransferInput()
        {
            Amount = input.Amount,
            Symbol = input.Symbol,
            To = State.Admin.Value,
            Memo = "withdraw delegation fee"
        });
        return new Empty();
    }

    public override Hash RegisterProjectDelegatee(RegisterProjectDelegateeInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.ProjectName), "Invalid project name.");
        Assert(input.Salts.Count > 0, "Input salts is empty.");
        Assert(input.Salts.Count <= CAContractConstants.DelegateeListMaxCount, "Exceed salts max count " + CAContractConstants.DelegateeListMaxCount);
        Assert(input.Signer != null && !input.Signer.Value.IsNullOrEmpty(), "Invalid signer.");
        var projectHash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.ProjectName), Context.TransactionId, Context.PreviousBlockHash);
        Assert(State.ProjectDelegateInfo[projectHash] == null, "Project Hash existed.");
        var projectDelegateInfo = new ProjectDelegateInfo
        {
            ProjectController = Context.Sender,
            Signer = input.Signer
        };
        var distinctSalt = input.Salts.DistinctBy(s => s).ToList();
        foreach (var salt in distinctSalt)
        {
            projectDelegateInfo.DelegateeHashList.Add(HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(salt), projectHash));
        }
        State.ProjectDelegateInfo[projectHash] = projectDelegateInfo;
        Context.Fire(new ProjectDelegateRegistered
        {
            ProjectDelegateHash = projectHash,
            Controller = Context.Sender,
            DelegateeHashList = new DelegateeHashList
            {
                HashList = { projectDelegateInfo.DelegateeHashList }
            },
            DelegateeAddressList = new DelegateeAddressList
            {
                AddressList = { projectDelegateInfo.DelegateeHashList.Select(t => Context.ConvertVirtualAddressToContractAddress(t)) }
            }
        });
        return projectHash;
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
            var delegateeHash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(salt), input.ProjectHash);
            if (!projectDelegateInfo.DelegateeHashList.Contains(delegateeHash))
            {
                addDelegateeHashList.Add(delegateeHash);
            }
        }
        if (addDelegateeHashList.Count == 0)
        {
            return new Empty();
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
        Assert(removeHashList.Count < projectDelegateInfo.DelegateeHashList.Count, "Can not remove all delegatee hash list.");
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
        State.TokenContract.Transfer.VirtualSend(input.DelegateeHash, new TransferInput()
        {
            To = Context.Sender,
            Amount = input.Amount,
            Symbol = CAContractConstants.ELFTokenSymbol,
            Memo = "Withdraw Project Delegatee Token"
        });
        return new Empty();
    }

    public override GetProjectDelegateInfoOutput GetProjectDelegatee(Hash input)
    {
        var projectDelegateInfo = State.ProjectDelegateInfo[input];
        if (projectDelegateInfo == null)
        {
            return new GetProjectDelegateInfoOutput();
        }

        var result = new GetProjectDelegateInfoOutput
        {
            ProjectController = projectDelegateInfo.ProjectController,
            Signer = projectDelegateInfo.Signer,
            DelegateeHashList = {projectDelegateInfo.DelegateeHashList}
        };
        foreach (var delegateeHash in result.DelegateeHashList)
        {
            result.DelegateeAddressList.Add(Context.ConvertVirtualAddressToContractAddress(delegateeHash));
        }
        return result;
    }

    public override Empty SetCaProjectDelegateHash(Hash input)
    {
        Assert(IsValidHash(input), "Invalid input.");
        Assert(Context.Sender == State.Admin.Value, "No permission");
        State.CaProjectDelegateHash.Value = input;
        return new Empty();
    }

    public override Hash GetCaProjectDelegateHash(Empty input)
    {
        return State.CaProjectDelegateHash.Value;
    }

    private void SetProjectDelegatee(Hash caHash, DelegateInfo delegateInfo)
    {
        if (delegateInfo == null || delegateInfo.ChainId != Context.ChainId || State.GuardianMap[delegateInfo.IdentifierHash] != caHash ||
            delegateInfo.Timestamp.AddSeconds(delegateInfo.ExpirationTime) < Context.CurrentBlockTime ||
            delegateInfo.Delegations.Count == 0 || string.IsNullOrWhiteSpace(delegateInfo.Signature))
        {
            return;
        }
        var projectDelegateInfo = State.ProjectDelegateInfo[delegateInfo.ProjectHash];
        if (projectDelegateInfo == null || projectDelegateInfo.DelegateeHashList.Count == 0)
        {
            return;
        }

        var cloneDelegateInfo = delegateInfo.Clone();
        cloneDelegateInfo.Signature = "";
        var recoverPublicKey = Context.RecoverPublicKey(ByteStringHelper.FromHexString(delegateInfo.Signature).ToByteArray(),
            ByteStringHelper.FromHexString(HashHelper.ComputeFrom(cloneDelegateInfo).ToHex()).ToByteArray());
        var signer = Address.FromPublicKey(recoverPublicKey);
        if (signer != projectDelegateInfo.Signer)
        {
            return;
        }
        
        var selectIndex = (int)Math.Abs(Context.ConvertVirtualAddressToContractAddress(caHash).ToByteArray().ToInt64(true) %
                          projectDelegateInfo.DelegateeHashList.Count);
        var delegateInfoList = new RepeatedField<AElf.Contracts.MultiToken.DelegateInfo>();
        if (State.DelegateWhitelistTransactions.Value == null)
        {
            return;
        }

        foreach (var methodName in State.DelegateWhitelistTransactions.Value.MethodNames)
        {
            delegateInfoList.Add(new AElf.Contracts.MultiToken.DelegateInfo
            {
                Delegations = { delegateInfo.Delegations },
                MethodName = methodName,
                ContractAddress = Context.Self,
                IsUnlimitedDelegate = delegateInfo.IsUnlimitedDelegate
            });
        }
        
        Context.SendVirtualInline(projectDelegateInfo.DelegateeHashList[selectIndex], State.TokenContract.Value,
            nameof(State.TokenContract.SetTransactionFeeDelegateInfos), new SetTransactionFeeDelegateInfosInput
            {
                DelegatorAddress = Context.ConvertVirtualAddressToContractAddress(caHash),
                DelegateInfoList = { delegateInfoList }
            });
    }

    public override Empty AssignProjectDelegatee(AssignProjectDelegateeInput input)
    { 
        Assert(input != null, "Invalid input.");
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null && projectDelegateInfo.DelegateeHashList.Count > 0, "Project delegate not existed.");
        Assert(Context.Sender == projectDelegateInfo.Signer, "No permission.");
        var selectIndex = (int)input.CaAddress.ToByteArray().ToInt64(true) %
                          projectDelegateInfo.DelegateeHashList.Count;
        var delegateInfoList = new RepeatedField<AElf.Contracts.MultiToken.DelegateInfo>();
        foreach (var assignDelegateInfo in input.AssignDelegateInfos)
        {
            delegateInfoList.Add(new AElf.Contracts.MultiToken.DelegateInfo
            {
                Delegations = { assignDelegateInfo.Delegations },
                MethodName = assignDelegateInfo.MethodName,
                ContractAddress = assignDelegateInfo.ContractAddress,
                IsUnlimitedDelegate = assignDelegateInfo.IsUnlimitedDelegate
            });
        }
        Context.SendVirtualInline(projectDelegateInfo.DelegateeHashList[selectIndex], State.TokenContract.Value,
            nameof(State.TokenContract.SetTransactionFeeDelegateInfos), new SetTransactionFeeDelegateInfosInput
            {
                DelegatorAddress = input.CaAddress,
                DelegateInfoList = { delegateInfoList }
            });
        return new Empty();
    }

    public override Empty RemoveCAProjectDelegatee(RemoveCAProjectDelegateeInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsValidHash(input.ProjectHash), "Invalid project hash.");
        var projectDelegateInfo = State.ProjectDelegateInfo[input.ProjectHash];
        Assert(projectDelegateInfo != null && projectDelegateInfo.DelegateeHashList.Count > 0, "Project delegate not existed.");
        Assert(projectDelegateInfo.Signer != null && !projectDelegateInfo.Signer.Value.IsNullOrEmpty(), "Signer not set.");
        Assert(Context.Sender == projectDelegateInfo.Signer, "No permission.");
        var delegateeTransactionListMap = new Dictionary<Hash, RepeatedField<DelegateTransaction>>();
        foreach (var delegateTransaction in input.DelegateTransactionList)
        {
            var delegateesOutput = State.TokenContract.GetTransactionFeeDelegateeList.Call(new GetTransactionFeeDelegateeListInput()
            {
                DelegatorAddress = input.CaAddress,
                ContractAddress = delegateTransaction.ContractAddress,
                MethodName = delegateTransaction.MethodName
            });
            if (delegateesOutput.DelegateeAddresses.Count == 0)
            {
                continue;
            }

            foreach (var projectDelegateeHash in projectDelegateInfo.DelegateeHashList)
            {
                if (delegateesOutput.DelegateeAddresses.Contains(Context.ConvertVirtualAddressToContractAddress(projectDelegateeHash)))
                {
                    var transactionList = delegateeTransactionListMap[projectDelegateeHash] ??
                                          new RepeatedField<DelegateTransaction>();
                    transactionList.Add(delegateTransaction);
                    delegateeTransactionListMap[projectDelegateeHash] = transactionList;
                }
            }
        }

        if (delegateeTransactionListMap.Count == 0)
        {
            return new Empty();
        }

        foreach (var delegateeTransactionListPair in delegateeTransactionListMap)
        {
            
            Context.SendVirtualInline(delegateeTransactionListPair.Key, State.TokenContract.Value, nameof(State.TokenContract.RemoveTransactionFeeDelegatorInfos),
                new RemoveTransactionFeeDelegatorInfosInput()
                {
                    DelegatorAddress = input.CaAddress,
                    DelegateTransactionList = { delegateeTransactionListPair.Value.Select(t => new AElf.Contracts.MultiToken.DelegateTransaction
                    {
                        MethodName = t.MethodName,
                        ContractAddress = t.ContractAddress,
                    }) }
                });
        }
        return new Empty();
    }

    public override Empty AddTransactionWhitelist(WhitelistTransactions input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.MethodNames.Count > 0, "Invalid input.");
        var whitelistTransactions = State.DelegateWhitelistTransactions.Value ?? new WhitelistTransactions();
        var addMethodNames = input.MethodNames.Except(whitelistTransactions.MethodNames);
        if (addMethodNames.Count() == 0)
        {
            return new Empty();
        }
        // Todo
        State.DelegateWhitelistTransactions.Value.MethodNames.AddRange(addMethodNames);
        return new Empty();
    }

    public override Empty RemoveTransactionWhitelist(WhitelistTransactions input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.MethodNames.Count > 0, "Invalid input.");
        var removeMethodNames = input.MethodNames.Intersect(State.DelegateWhitelistTransactions.Value.MethodNames);
        if (removeMethodNames.Count() == 0)
        {
            return new Empty();
        }

        foreach (var methodName in removeMethodNames)
        {
            State.DelegateWhitelistTransactions.Value.MethodNames.Remove(methodName);
        }
        return new Empty();
    }

    public override WhitelistTransactions GetTransactionWhitelist(Empty input)
    {
        return State.DelegateWhitelistTransactions.Value;
    }
}