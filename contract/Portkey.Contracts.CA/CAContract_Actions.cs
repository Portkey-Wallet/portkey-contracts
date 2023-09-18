using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract : CAContractContainer.CAContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        var author = State.GenesisContract.GetContractAuthor.Call(Context.Self);
        Assert(author == Context.Sender, "No permission.");
        State.Admin.Value = input.ContractAdmin ?? Context.Sender;
        State.CreatorControllers.Value = new ControllerList { Controllers = { input.ContractAdmin ?? Context.Sender } };
        State.ServerControllers.Value = new ControllerList { Controllers = { input.ContractAdmin ?? Context.Sender } };
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        // State.MethodFeeController.Value = new AuthorityInfo
        // {
        //     OwnerAddress = Context.Sender
        // };
        State.Initialized.Value = true;

        return new Empty();
    }

    /// <summary>
    ///     The Create method can only be executed in AElf MainChain.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public override Empty CreateCAHolder(CreateCAHolderInput input)
    {
        //Assert(Context.Sender == State.RegisterOrRecoveryController.Value,"No permission.");
        Assert(State.CreatorControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        Assert(input != null, "Invalid input.");
        Assert(input!.GuardianApproved != null && IsValidHash(input.GuardianApproved.IdentifierHash),
            "invalid input guardian");
        Assert(
            input.GuardianApproved!.VerificationInfo != null, "invalid verification");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        var guardianIdentifierHash = input.GuardianApproved.IdentifierHash;
        var holderId = State.GuardianMap[guardianIdentifierHash];

        // if CAHolder exists
        if (holderId != null) return new Empty();

        var holderInfo = new HolderInfo();
        holderId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);
        holderInfo.CreatorAddress = Context.Sender;
        holderInfo.CreateChainId = Context.ChainId;
        holderInfo.ManagerInfos.Add(input.ManagerInfo);
        //Check verifier signature.
        var methodName = nameof(CreateCAHolder).ToLower();
        if (!CheckVerifierSignatureAndDataCompatible(input.GuardianApproved, methodName))
        {
            return new Empty();
        }

        var salt = GetSaltFromVerificationDoc(input.GuardianApproved.VerificationInfo.VerificationDoc);
        var guardian = new Guardian
        {
            IdentifierHash = input.GuardianApproved.IdentifierHash,
            Salt = salt,
            Type = input.GuardianApproved.Type,
            VerifierId = input.GuardianApproved.VerificationInfo.Id,
            IsLoginGuardian = true
        };

        holderInfo.GuardianList = new GuardianList
        {
            Guardians = { guardian }
        };

        holderInfo.JudgementStrategy = input.JudgementStrategy ?? Strategy.DefaultStrategy();

        // Where is the code for double check approved guardians?
        // Don't forget to assign GuardianApprovedCount
        var isJudgementStrategySatisfied =
            IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count, 1, holderInfo.JudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            return new Empty();
        }

        State.HolderInfoMap[holderId] = holderInfo;
        State.GuardianMap[guardianIdentifierHash] = holderId;
        State.LoginGuardianMap[guardianIdentifierHash][input.GuardianApproved.VerificationInfo.Id] = holderId;

        SetDelegator(holderId, input.ManagerInfo);

        SetContractDelegator(input.ManagerInfo);

        // Log Event
        Context.Fire(new CAHolderCreated
        {
            Creator = Context.Sender,
            CaHash = holderId,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(holderId),
            Manager = input.ManagerInfo!.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        Context.Fire(new LoginGuardianAdded
        {
            CaHash = holderId,
            CaAddress = Context.ConvertVirtualAddressToContractAddress(holderId),
            LoginGuardian = guardian,
            Manager = input.ManagerInfo.Address,
        });

        return new Empty();
    }

    private void FillCreateChainId(HolderInfo holderInfo)
    {
        if (holderInfo.CreateChainId > 0)
            return;
        if (holderInfo.GuardianList?.Guardians?.Count > 0)
        {
            holderInfo.CreateChainId = Context.ChainId;
        }
    }

    private void AssertOnCreateChain(HolderInfo holderInfo)
    {
        if (holderInfo.CreateChainId != 0 && holderInfo.GuardianList?.Guardians?.Count > 0)
        {
            Assert(holderInfo.CreateChainId == Context.ChainId, "Not on registered chain");
        }
    }

    private bool IsJudgementStrategySatisfied(int guardianCount, int guardianApprovedCount, StrategyNode strategyNode)
    {
        var context = new StrategyContext()
        {
            Variables = new Dictionary<string, long>()
            {
                { CAContractConstants.GuardianCount, guardianCount },
                { CAContractConstants.GuardianApprovedCount, guardianApprovedCount }
            }
        };

        var judgementStrategy = StrategyFactory.Create(strategyNode);
        return (bool)judgementStrategy.Validate(context);
    }

    private void SetDelegator(Hash holderId, ManagerInfo managerInfo)
    {
        var delegations = new Dictionary<string, long>
        {
            [CAContractConstants.ELFTokenSymbol] = CAContractConstants.CADelegationAmount
        };

        Context.SendVirtualInline(holderId, State.TokenContract.Value,
            nameof(State.TokenContract.SetTransactionFeeDelegations),
            new SetTransactionFeeDelegationsInput
            {
                DelegatorAddress = managerInfo.Address,
                Delegations =
                {
                    delegations
                }
            });
    }

    private void SetDelegators(Hash holderId, RepeatedField<ManagerInfo> managerInfos)
    {
        foreach (var managerInfo in managerInfos)
        {
            SetDelegator(holderId, managerInfo);
        }
    }

    private void RemoveDelegator(Hash holderId, ManagerInfo managerInfo)
    {
        Context.SendVirtualInline(holderId, State.TokenContract.Value,
            nameof(State.TokenContract.RemoveTransactionFeeDelegator),
            new RemoveTransactionFeeDelegatorInput
            {
                DelegatorAddress = managerInfo.Address
            });
    }

    private void RemoveDelegators(Hash holderId, RepeatedField<ManagerInfo> managerInfos)
    {
        foreach (var managerInfo in managerInfos)
        {
            RemoveDelegator(holderId, managerInfo);
        }
    }

    private void SetContractDelegator(ManagerInfo managerInfo)
    {
        // Todo Temporary, need delete later
        if (State.ContractDelegationFee.Value == null)
        {
            State.ContractDelegationFee!.Value = new ContractDelegationFee
            {
                Amount = CAContractConstants.DefaultContractDelegationFee
            };
        }

        var delegations = new Dictionary<string, long>
        {
            [CAContractConstants.ELFTokenSymbol] = State.ContractDelegationFee.Value.Amount
        };

        State.TokenContract.SetTransactionFeeDelegations.Send(new SetTransactionFeeDelegationsInput
        {
            DelegatorAddress = managerInfo.Address,
            Delegations =
            {
                delegations
            }
        });
    }

    private void RemoveContractDelegator(ManagerInfo managerInfo)
    {
        State.TokenContract.RemoveTransactionFeeDelegator.Send(new RemoveTransactionFeeDelegatorInput
        {
            DelegatorAddress = managerInfo.Address,
        });
    }

    public override Empty SetContractDelegationFee(SetContractDelegationFeeInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(input != null && input.DelegationFee != null, "invalid input");
        Assert(input!.DelegationFee!.Amount >= 0, "input can not be less than 0");

        if (State.ContractDelegationFee.Value == null)
        {
            State.ContractDelegationFee!.Value = new ContractDelegationFee();
        }

        State.ContractDelegationFee.Value.Amount = input.DelegationFee.Amount;

        return new Empty();
    }

    public override GetContractDelegationFeeOutput GetContractDelegationFee(Empty input)
    {
        return new GetContractDelegationFeeOutput
        {
            DelegationFee = State.ContractDelegationFee.Value
        };
    }

    public override Empty ChangeOperationTypeInSignatureEnabled(OperationTypeInSignatureEnabledInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(State.OperationTypeInSignatureEnabled.Value != input.OperationTypeInSignatureEnabled, "invalid input");
        State.OperationTypeInSignatureEnabled.Value = input.OperationTypeInSignatureEnabled;
        return new Empty();
    }

    public override OperationTypeInSignatureEnabledOutput GetOperationTypeInSignatureEnabled(Empty input)
    {
        return new OperationTypeInSignatureEnabledOutput
        {
            OperationTypeInSignatureEnabled = State.OperationTypeInSignatureEnabled.Value
        };
    }

    public override Empty SetCAContractAddresses(SetCAContractAddressesInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        foreach (var caContractAddress in input.CaContractAddresses)
        {
            State.CAContractAddresses[caContractAddress.ChainId] = caContractAddress.Address;
        }

        return new Empty();
    }

    public override Empty SetManagerApproveForbiddenEnabled(ManagerApproveForbiddenEnabledInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(State.ManagerApproveForbiddenEnabled.Value != input.ManagerApproveForbiddenEnabled,
            $"Already set {State.ManagerApproveForbiddenEnabled.Value}, no need to repeat settings");
        State.ManagerApproveForbiddenEnabled.Value = input.ManagerApproveForbiddenEnabled;
        return new Empty();
    }
}