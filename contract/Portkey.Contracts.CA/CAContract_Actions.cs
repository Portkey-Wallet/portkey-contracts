using System.Collections.Generic;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract : CAContractImplContainer.CAContractImplBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        // The main chain uses the audit deployment, does not verify the Author
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        var contractInfo = State.GenesisContract.GetContractInfo.Call(Context.Self);
        Assert(contractInfo.Deployer == Context.Sender, "No permission");

        State.Admin.Value = input.ContractAdmin ?? Context.Sender;
        State.CreatorControllers.Value = new ControllerList { Controllers = { input.ContractAdmin ?? Context.Sender } };
        State.ServerControllers.Value = new ControllerList { Controllers = { input.ContractAdmin ?? Context.Sender } };
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
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
        if (holderId != null && State.AcceleratedRegistration[guardianIdentifierHash] != holderId) return new Empty();

        // Delete the useless data generated by accelerated registration.
        if (holderId != null)
        {
            State.HolderInfoMap.Remove(holderId);
        }

        holderId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);
        if (!CreateCaHolderInfoWithCaHashAndCreateChainId(input.ManagerInfo, input.GuardianApproved,
                input.JudgementStrategy,
                holderId, Context.ChainId, out var guardian, out var caAddress))
        {
            return new Empty();
        }

        if (!SetProjectDelegateInfo(holderId, input.DelegateInfo))
        {
            SetProjectDelegator(caAddress);
        }

        State.AcceleratedRegistration.Remove(guardianIdentifierHash);

        // Log Event
        Context.Fire(new CAHolderCreated
        {
            Creator = Context.Sender,
            CaHash = holderId,
            CaAddress = caAddress,
            Manager = input.ManagerInfo!.Address,
            ExtraData = input.ManagerInfo.ExtraData
        });

        Context.Fire(new LoginGuardianAdded
        {
            CaHash = holderId,
            CaAddress = caAddress,
            LoginGuardian = guardian,
            Manager = input.ManagerInfo.Address,
            IsCreateHolder = true
        });
        FireInvitedLogEvent(holderId, nameof(CreateCAHolder), input.ReferralCode, input.ProjectCode);
        return new Empty();
    }

    private void FireInvitedLogEvent(Hash caHash, string methodName, string referralCode, string projectCode)
    {
        var isValidReferralCode = IsValidInviteCode(referralCode);
        var isValidProjectCode = IsValidInviteCode(projectCode);
        if (isValidProjectCode || isValidReferralCode)
        {
            Context.Fire(new Invited
            {
                CaHash = caHash,
                ContractAddress = Context.Self,
                MethodName = methodName,
                ProjectCode = isValidProjectCode ? projectCode : "",
                ReferralCode = isValidReferralCode ? referralCode : ""
            });
        }
    }

    public override Empty CreateCAHolderOnNonCreateChain(CreateCAHolderOnNonCreateChainInput input)
    {
        Assert(State.CheckOperationDetailsInSignatureEnabled.Value, "Not supported");
        Assert(State.CreatorControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        Assert(input != null, "Invalid input.");
        Assert(input!.GuardianApproved != null && IsValidHash(input.GuardianApproved.IdentifierHash),
            "invalid input guardian");
        Assert(
            input.GuardianApproved!.VerificationInfo != null, "invalid verification");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(input.ManagerInfo?.Address != null, "invalid input managerInfo address");
        Assert(input.CreateChainId != 0 && input.CreateChainId != Context.ChainId, "Invalid input CreateChainId");
        Assert(IsValidHash(input.CaHash), "Invalid input CaHash");

        var guardianIdentifierHash = input.GuardianApproved.IdentifierHash;
        var holderId = State.GuardianMap[guardianIdentifierHash];
        var holderInfo = State.HolderInfoMap[input.CaHash];

        // if CAHolder exists
        if (holderId != null || holderInfo != null) return new Empty();

        if (!CreateCaHolderInfoWithCaHashAndCreateChainId(input.ManagerInfo, input.GuardianApproved,
                input.JudgementStrategy,
                input.CaHash, input.CreateChainId, out var guardian, out var caAddress))
        {
            return new Empty();
        }

        SetProjectDelegator(caAddress);

        State.AcceleratedRegistration[guardianIdentifierHash] = input.CaHash;

        Context.Fire(new NonCreateChainCAHolderCreated
        {
            Creator = Context.Sender,
            CaHash = input.CaHash,
            CaAddress = caAddress,
            Manager = input.ManagerInfo!.Address,
            ExtraData = input.ManagerInfo.ExtraData,
            CreateChainId = input.CreateChainId
        });

        return new Empty();
    }

    private bool CreateCaHolderInfoWithCaHashAndCreateChainId(ManagerInfo managerInfo, GuardianInfo guardianApproved,
        StrategyNode judgementStrategy, Hash holderId, int chainId, out Guardian guardian, out Address caAddress)
    {
        //Check verifier signature.
        var methodName = nameof(CreateCAHolder).ToLower();
        var operationDetail = managerInfo.Address.ToBase58();
        if (!CheckVerifierSignatureAndData(guardianApproved, methodName, chainId == Context.ChainId ? null : holderId,
                operationDetail))
        {
            guardian = null;
            caAddress = null;
            return false;
        }

        var guardianIdentifierHash = guardianApproved.IdentifierHash;
        var holderInfo = new HolderInfo();
        holderInfo.CreatorAddress = Context.Sender;
        holderInfo.CreateChainId = chainId;
        holderInfo.ManagerInfos.Add(managerInfo);

        var salt = GetSaltFromVerificationDoc(guardianApproved.VerificationInfo.VerificationDoc);
        guardian = new Guardian
        {
            IdentifierHash = guardianApproved.IdentifierHash,
            Salt = salt,
            Type = guardianApproved.Type,
            VerifierId = guardianApproved.VerificationInfo.Id,
            IsLoginGuardian = true
        };

        holderInfo.GuardianList = new GuardianList
        {
            Guardians = { guardian }
        };

        holderInfo.JudgementStrategy = judgementStrategy ?? Strategy.DefaultStrategy();

        // Where is the code for double check approved guardians?
        // Don't forget to assign GuardianApprovedCount
        var isJudgementStrategySatisfied =
            IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count, 1, holderInfo.JudgementStrategy);
        if (!isJudgementStrategySatisfied)
        {
            caAddress = null;
            return false;
        }

        State.HolderInfoMap[holderId] = holderInfo;
        State.GuardianMap[guardianIdentifierHash] = holderId;
        State.LoginGuardianMap[guardianIdentifierHash][guardianApproved.VerificationInfo.Id] = holderId;

        SetDelegator(holderId, managerInfo);

        caAddress = Context.ConvertVirtualAddressToContractAddress(holderId);

        return true;
    }

    private void AssertCreateChain(HolderInfo holderInfo)
    {
        Assert(holderInfo.CreateChainId == Context.ChainId, "Not on registered chain");
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

        State.TokenContract.SetTransactionFeeDelegations.VirtualSend(holderId,
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
        State.TokenContract.RemoveTransactionFeeDelegator.VirtualSend(holderId,
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

    public override Empty SetProjectDelegationFee(SetProjectDelegationFeeInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(input != null && input.DelegationFee != null, "Invalid input");
        Assert(input.DelegationFee.Amount >= 0, "Amount can not be less than 0");

        State.ProjectDelegationFee.Value ??= new ProjectDelegationFee();

        State.ProjectDelegationFee.Value.Amount = input.DelegationFee.Amount;

        return new Empty();
    }

    public override ProjectDelegationFee GetProjectDelegationFee(Empty input)
    {
        return State.ProjectDelegationFee.Value;
    }

    public override Empty SetCheckOperationDetailsInSignatureEnabled(
        SetCheckOperationDetailsInSignatureEnabledInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(State.CheckOperationDetailsInSignatureEnabled.Value != input.CheckOperationDetailsEnabled,
            $"It is already {input.CheckOperationDetailsEnabled}");
        State.CheckOperationDetailsInSignatureEnabled.Value = input.CheckOperationDetailsEnabled;

        return new Empty();
    }

    public override GetCheckOperationDetailsInSignatureEnabledOutput
        GetCheckOperationDetailsInSignatureEnabled(Empty input)
    {
        return new GetCheckOperationDetailsInSignatureEnabledOutput
        {
            CheckOperationDetailsEnabled = State.CheckOperationDetailsInSignatureEnabled.Value
        };
    }
}