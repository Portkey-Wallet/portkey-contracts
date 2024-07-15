using System.Collections.Generic;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using AetherLink.Contracts.Consumer;
using AetherLink.Contracts.Oracle;
using Google.Protobuf;
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
        Assert(input.ManagerInfo?.Address != null, "invalid input managerInfo address");
        var guardianIdentifierHash = input.GuardianApproved.IdentifierHash;
        var holderId = State.GuardianMap[guardianIdentifierHash];

        // if CAHolder exists and there is no pre cross chain synchronization mark
        if (holderId != null && State.PreCrossChainSyncHolderInfoMarks[guardianIdentifierHash] != holderId)
            return new Empty();

        // Delete the useless data generated by accelerated registration.
        if (holderId != null)
        {
            State.HolderInfoMap.Remove(holderId);
        }

        holderId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);
        Guardian guardian;
        Address caAddress;
        if (IsZkLoginSupported(input.GuardianApproved.Type))
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainIdForZkLogin(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    holderId, Context.ChainId, out guardian, out caAddress))
            {
                return new Empty();
            }
        }
        else
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainId(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    holderId, Context.ChainId, out guardian, out caAddress))
            {
                return new Empty();
            }
        }

        if (!SetProjectDelegateInfo(holderId, input.DelegateInfo))
        {
            SetProjectDelegator(caAddress);
        }

        State.PreCrossChainSyncHolderInfoMarks.Remove(guardianIdentifierHash);

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

    // private bool IsZkLoginSupported(GuardianType type)
    // {
    //     return GuardianType.OfGoogle.Equals(type) || GuardianType.OfApple.Equals(type) ||
    //            GuardianType.OfFacebook.Equals(type);
    // }

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

    /// <summary>
    /// The Create method can only be executed on the outside of the 'CreateChain'.
    /// This method aims to pre-create a HolderInfo for cross-chain operations.
    /// The difference from 'CreateCAHolder' is that 'CAHash' is passed as an interface parameter and it requires verifying the operation details in the signature
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public override Empty ReportPreCrossChainSyncHolderInfo(ReportPreCrossChainSyncHolderInfoInput input)
    {
        Assert(State.CreatorControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        Assert(input != null, "Invalid input.");
        Assert(input!.GuardianApproved != null && IsValidHash(input.GuardianApproved.IdentifierHash),
            "invalid input guardian");
        Assert(
            input.GuardianApproved!.VerificationInfo != null, "invalid verification");
        Assert(GetVerificationDocLength(input.GuardianApproved!.VerificationInfo!.VerificationDoc) >= 8,
            "Not supported");
        Assert(input.ManagerInfo != null, "invalid input managerInfo");
        Assert(input.ManagerInfo?.Address != null, "invalid input managerInfo address");
        Assert(input.CreateChainId != 0 && input.CreateChainId != Context.ChainId, "Invalid input CreateChainId");
        Assert(IsValidHash(input.CaHash), "Invalid input CaHash");

        var guardianIdentifierHash = input.GuardianApproved.IdentifierHash;
        var holderId = State.GuardianMap[guardianIdentifierHash];
        var holderInfo = State.HolderInfoMap[input.CaHash];

        // if CAHolder exists
        if (holderId != null || holderInfo != null) return new Empty();
        Guardian guardian;
        Address caAddress;
        if (IsZkLoginSupported(input.GuardianApproved.Type))
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainIdForZkLogin(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    holderId, Context.ChainId, out guardian, out caAddress))
            {
                return new Empty();
            }
        }
        else
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainId(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    input.CaHash, input.CreateChainId, out guardian, out caAddress))
            {
                return new Empty();
            }
        }

        SetProjectDelegator(caAddress);

        State.PreCrossChainSyncHolderInfoMarks[guardianIdentifierHash] = input.CaHash;

        Context.Fire(new PreCrossChainSyncHolderInfoCreated
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
    
    private bool CreateCaHolderInfoWithCaHashAndCreateChainIdForZkLogin(ManagerInfo managerInfo, GuardianInfo guardianApproved,
        StrategyNode judgementStrategy, Hash holderId, int chainId, out Guardian guardian, out Address caAddress)
    {
        //Check zk proof
        if (!CheckZkLoginVerifierAndData(guardianApproved))
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

        guardian = new Guardian
        {
            IdentifierHash = guardianApproved.IdentifierHash,
            //the original verifier hasn't been verified
            Salt = string.Empty,
            Type = guardianApproved.Type,
            //get a verifier from verifier server list verifierId
            VerifierId = GetOneVerifierFromServers(),
            IsLoginGuardian = true,
            ZkOidcInfo = guardianApproved.ZkOidcInfo,
            ManuallySupportForZk = true //when the new user registered,portkey contract used zklogin as verifier
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
            IsLoginGuardian = true,
            ManuallySupportForZk = false
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

    public override Empty AddJwtIssuer(JwtIssuerAndEndpointInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No AddJwtIssuer permission.");
        Assert(input != null, "Invalid input when AddJwtIssuer.");
        Assert(IsValidGuardianType(input.Type), "Invalid guardian input when adding jwt issuer.");
        Assert(input.Issuer != null, "Invalid Issuer input when adding jwt issuer.");
        Assert(input.Oauth2Endpoint != null, "Invalid Oauth2Endpoint input when adding jwt issuer.");
        Assert(!input.Issuer.Equals(State.OidcProviderAdminData[input.Type].Issuer), "the guardian type's issuer exists");
        State.OidcProviderAdminData[input.Type] = new ZkBasicAdminData()
        {
            Issuer = input.Issuer,
            Oauth2Endpoint = input.Oauth2Endpoint
        };
        return new Empty();
    }

    public override Empty AddKidPublicKey(KidPublicKeyInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No AddKidPublicKey permission.");
        Assert(input != null, "Invalid input when AddJwtIssuer.");
        Assert(IsValidGuardianType(input.Type), "Invalid guardian input when adding kid public key.");
        Assert(input.Kid != null, "Invalid kid input when adding kid public key.");
        Assert(input.PublicKey != null, "Invalid PublicKey input when adding kid public key.");
        State.IssuerPublicKeysByKid[input.Type][input.Kid] = input.PublicKey;
        return new Empty();
    }

    public override Empty AddOrUpdateVerifyingKey(VerifyingKey input)
    {
        Assert(Context.Sender == State.Admin.Value, "no addVerifyingKey permission.");
        Assert(input != null, "Invalid verifying key input.");
        Assert(!string.IsNullOrEmpty(input.CircuitId), "circuitId is required.");
        Assert(!string.IsNullOrEmpty(input.VerifyingKey_), "verifying key is required.");
        State.CircuitVerifyingKeys[input.CircuitId] = input;
        return new Empty();
    }

    public override BoolValue IsValidIssuer(JwtIssuerAndEndpointInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No IsValidIssuer permission.");
        Assert(input != null, "Invalid JwtIssuerInput in IsValidIssuer method.");
        Assert(input.Issuer != null, "Invalid Issuer in IsValidIssuer method.");
        Assert(IsValidGuardianType(input.Type), "Invalid Type in IsValidIssuer method.");
        Assert(State.OidcProviderAdminData[input.Type] != null, "guardian type doesn't exist.");
        Assert(State.OidcProviderAdminData[input.Type].Issuer != null, "verifying key doesn't exist.");
        return new BoolValue
        {
            Value = State.OidcProviderAdminData[input.Type].Issuer == input.Issuer
        };
    }

    public override VerifyingKey GetVerifyingKey(StringValue input)
    {
        Assert(Context.Sender == State.Admin.Value, "No GetVerifyingKey permission.");
        Assert(input != null, "Invalid circuit id.");
        Assert(State.CircuitVerifyingKeys[input.Value] != null, "circuitId not exist");
        return State.CircuitVerifyingKeys[input.Value];
    }

    public override Empty SetOracleAddress(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, "No SetOracleAddress permission.");
        Assert(input != null, "Invalid address input");
        Assert(State.OracleAddress.Value != null, "OracleAddress exists");
        State.OracleAddress.Value = input;
        return new Empty();
    }

    public override Empty StartOracleDataFeedsTask(StartOracleDataFeedsTaskRequest input)
    {
        Assert(Context.Sender == State.Admin.Value, "No StartOracleRequest permission.");
        Assert(input != null, "Invalid StartOracleDataFeedsTaskRequest input.");
        Assert(IsValidGuardianType(input.Type), "Invalid input type.");
        Assert(input.SubscriptionId > 0, "Invalid input subscription id.");
        Assert(State.OidcProviderAdminData[input.Type] != null, "StartOracleDataFeedsTaskRequest OidcProviderAdminData doesn't exist");
        Assert(!(State.OidcProviderAdminData[input.Type].SubscriptionId.Equals(input.SubscriptionId)
            && State.OidcProviderAdminData[input.Type].SpecificData != null
            && State.OidcProviderAdminData[input.Type].SpecificData.DataFeedsJobSpec != null), "the SubscriptionId has started before");

        var specificData = new OracleDataFeedsSpecificData()
        {
            Cron = "0 */2 * * * ?",
            DataFeedsJobSpec = new OracleDataFeedsJobSpec()
            {
                Type = "PlainDataFeeds",
                Url = State.OidcProviderAdminData[input.Type].Oauth2Endpoint
                //"https://www.googleapis.com/oauth2/v3/certs"
            }
        };
        State.OracleContract.SendRequest.Send(new SendRequestInput
        {
            SubscriptionId = input.SubscriptionId,
            RequestTypeIndex = 1,
            SpecificData = specificData.ToByteString()
        });
        State.OidcProviderAdminData[input.Type].SubscriptionId = input.SubscriptionId;
        State.OidcProviderAdminData[input.Type].RequestTypeIndex = 1;
        State.OidcProviderAdminData[input.Type].SpecificData = specificData;
        return new Empty();
    }

    public override Empty HandleOracleFulfillment(HandleOracleFulfillmentInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(State.OracleContract.Value == State.OracleAddress.Value, "Invalid OracleContract address.");
        Assert(input.RequestTypeIndex > 0, "Invalid request type index.");
        Assert(!input.Response.IsNullOrEmpty() || !input.Err.IsNullOrEmpty(), "Invalid input response or err.");
        //check the input.Response input.Response.ToBase64() or input.Response.ToHex() or input.Response.ToPlainBase58()
        GoogleResponse response = GoogleResponse.Parser.ParseFrom(input.Response.ToByteArray());
        Assert(response != null, "Invalid HandleOracleFulfillmentInput response input.");
        //todo aetherlink can't differentiate apple/google/facebook
        foreach (var googleKeyDto in response.Keys)
        {
            State.IssuerPublicKeysByKid[GuardianType.OfGoogle][googleKeyDto.Kid] = googleKeyDto.N;
        }

        return new Empty();
    }
}