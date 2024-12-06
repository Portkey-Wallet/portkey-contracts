using System.Collections.Generic;
using System.Linq;
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

        State.OrganizationAddress.Value = input.ContractAdmin ?? Context.Sender;
        State.CreatorControllers.Value = new ControllerList { Controllers = { input.ContractAdmin ?? Context.Sender } };
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
        {
            Context.Fire(new CAHolderErrorOccured
            {
                ErrorMessage = "caHash exists cahash:" + holderId + "guardianIdentifierHash:" + guardianIdentifierHash
            }
            );
            return new Empty();
        }

        // Delete the useless data generated by accelerated registration.
        if (holderId != null)
        {
            State.HolderInfoMap.Remove(holderId);
        }

        holderId = HashHelper.ConcatAndCompute(Context.TransactionId, Context.PreviousBlockHash);
        Guardian guardian;
        Address caAddress;
        input.ManagerInfo.Platform = input.Platform;
        if (CanZkLoginExecute(input.GuardianApproved))
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainIdForZkLogin(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    holderId, Context.ChainId, out guardian, out caAddress))
            {
                Context.Fire(new CAHolderErrorOccured
                    {
                        ErrorMessage = "zk validation failed caHash:" + holderId + " identifierHash:" + input.GuardianApproved.ZkLoginInfo.IdentifierHash
                        + " salt:" + input.GuardianApproved.ZkLoginInfo.Salt
                        + " nonce:" + input.GuardianApproved.ZkLoginInfo.Nonce
                        + " kid:" + input.GuardianApproved.ZkLoginInfo.Kid
                        + " issuer:" + input.GuardianApproved.ZkLoginInfo.Issuer
                        + " circuitId:" + input.GuardianApproved.ZkLoginInfo.CircuitId
                    }
                );
                return new Empty();
            }
        }
        else
        {
            if (!CreateCaHolderInfoWithCaHashAndCreateChainId(input.ManagerInfo, input.GuardianApproved,
                    input.JudgementStrategy,
                    holderId, Context.ChainId, out guardian, out caAddress))
            {
                Context.Fire(new CAHolderErrorOccured
                    {
                        ErrorMessage = "signature validation failed cahash:" + holderId + "identifierHash:" + guardianIdentifierHash
                        + " verificationId:" + input.GuardianApproved.VerificationInfo.Id
                        + " signature:" + input.GuardianApproved.VerificationInfo.Signature
                        + " verificationDoc:" + input.GuardianApproved.VerificationInfo.VerificationDoc
                    }
                );
                return new Empty();
            }
        }

        if (!SetProjectDelegateInfo(holderId, input.DelegateInfo))
        {
            SetProjectDelegator(caAddress);
        }

        State.PreCrossChainSyncHolderInfoMarks.Remove(guardianIdentifierHash);
        UpdateManagerTransactionStatistics(holderId, input.ManagerInfo!.Address);
        // Log Event
        Context.Fire(new CAHolderCreated
        {
            Creator = Context.Sender,
            CaHash = holderId,
            CaAddress = caAddress,
            Manager = input.ManagerInfo!.Address,
            ExtraData = input.ManagerInfo.ExtraData,
            Platform = (int)input.Platform
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
        if (CanZkLoginExecute(input.GuardianApproved))
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
        UpdateManagerTransactionStatistics(input.CaHash, input.ManagerInfo?.Address);
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
        if (!CheckZkLoginVerifierAndData(guardianApproved, holderId))
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
            Salt = guardianApproved.ZkLoginInfo.Salt,
            Type = guardianApproved.Type,
            //get a verifier from verifier server list verifierId
            VerifierId = guardianApproved.VerificationInfo == null
                         || Hash.Empty.Equals(guardianApproved.VerificationInfo.Id)
                ? GetRandomVerifierFromServers() : guardianApproved.VerificationInfo.Id,
            IsLoginGuardian = true,
            ZkLoginInfo = guardianApproved.ZkLoginInfo,
            ManuallySupportForZk = true, //when the new user registered,portkey contract used zklogin as verifier
            PoseidonIdentifierHash = guardianApproved.ZkLoginInfo.PoseidonIdentifierHash
        };
        holderInfo.GuardianList = new GuardianList
        {
            Guardians = { guardian }
        };

        holderInfo.JudgementStrategy = judgementStrategy ?? Strategy.DefaultStrategy();

        // Where is the code for double check approved guardians?
        // Don't forget to assign GuardianApprovedCount
        var isJudgementStrategySatisfied =
            IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count, 1, holderInfo.JudgementStrategy, caHash:null, clearReadOnlyManager:false);
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
            IsJudgementStrategySatisfied(holderInfo.GuardianList.Guardians.Count, 1, holderInfo.JudgementStrategy, caHash:null, clearReadOnlyManager:false);
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

    private bool IsJudgementStrategySatisfied(int guardianCount, int guardianApprovedCount, StrategyNode strategyNode,
        Hash caHash = null, bool clearReadOnlyManager = true)
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
        var judgementResult = (bool)judgementStrategy.Validate(context);
        //after guardian approved, read-only status manager should be cleared.
        if (!clearReadOnlyManager || caHash == null || !judgementResult)
            return judgementResult;
        if (IsManagerReadOnly(caHash, Context.Sender))
            State.CaHashToReadOnlyStatusManagers[caHash].ManagerAddresses.Remove(Context.Sender);
        return true;
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
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
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
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
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

    public override Empty AddOrUpdateJwtIssuer(JwtIssuerAndEndpointInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No AddJwtIssuer permission.");
        Assert(input != null, "Invalid input when AddJwtIssuer.");
        Assert(IsValidGuardianType(input.Type), "Invalid guardian input when adding jwt issuer.");
        Assert(input.Issuer != null, "Invalid Issuer input when adding jwt issuer.");
        Assert(input.JwksEndpoint != null, "Invalid JwksEndpoint input when adding jwt issuer.");
        State.OidcProviderData[input.Type] = new ZkBasicAdminData()
        {
            Issuer = input.Issuer,
            JwksEndpoint = input.JwksEndpoint
        };
        Context.Fire(new JwtIssuerCreated
        {
            Issuer = State.OidcProviderData[input.Type].Issuer,
            JwksEndpoint = State.OidcProviderData[input.Type].JwksEndpoint
        });
        return new Empty();
    }
    
    public override BoolValue IsValidIssuer(JwtIssuerAndEndpointInput input)
    {
        Assert(input != null, "Invalid JwtIssuerInput in IsValidIssuer method.");
        Assert(input.Issuer != null, "Invalid Issuer in IsValidIssuer method.");
        Assert(IsValidGuardianType(input.Type), "Invalid Type in IsValidIssuer method.");
        Assert(State.OidcProviderData[input.Type] != null, "guardian type doesn't exist.");
        Assert(State.OidcProviderData[input.Type].Issuer != null, "verifying key doesn't exist.");
        return new BoolValue
        {
            Value = State.OidcProviderData[input.Type].Issuer == input.Issuer
        };
    }

    public override Empty AddKidPublicKey(KidPublicKeyInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No AddKidPublicKey permission.");
        Assert(input != null, "Invalid input when AddKidPublicKey.");
        Assert(IsValidGuardianType(input.Type), "Invalid guardian input when adding kid public key.");
        Assert(input.Kid != null, "Invalid kid input when adding kid public key.");
        Assert(input.PublicKey != null, "Invalid PublicKey input when adding kid public key.");
        
        SetPublicKeyAndChunks(input.Type, input.Kid, input.PublicKey);
        return new Empty();
    }

    public override KidPublicKeyOutput GetGooglePublicKeyByKid(StringValue input)
    {
        Assert(input != null, "Invalid kid.");
        Assert(State.PublicKeysChunksByKid[GuardianType.OfGoogle][input.Value] != null, "the public key of kid not exists.");
        return new KidPublicKeyOutput
        {
            Kid = input.Value,
            PublicKey = State.PublicKeysChunksByKid[GuardianType.OfGoogle][input.Value].PublicKey,
            PublicKeyChunks = { State.PublicKeysChunksByKid[GuardianType.OfGoogle][input.Value].PublicKeyChunks }
        };
    }
    
    public override KidPublicKeyOutput GetApplePublicKeyByKid(StringValue input)
    {
        Assert(input != null, "Invalid kid.");
        Assert(State.PublicKeysChunksByKid[GuardianType.OfApple][input.Value] != null, "the public key of kid not exists.");
        return new KidPublicKeyOutput
        {
            Kid = input.Value,
            PublicKey = State.PublicKeysChunksByKid[GuardianType.OfApple][input.Value].PublicKey,
            PublicKeyChunks = { State.PublicKeysChunksByKid[GuardianType.OfApple][input.Value].PublicKeyChunks }
        };
    }

    public override Empty AddOrUpdateVerifyingKey(VerifyingKey input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No addVerifyingKey permission.");
        Assert(input != null, "Invalid verifying key input.");
        Assert(!string.IsNullOrEmpty(input.CircuitId), "circuitId is required.");
        Assert(!string.IsNullOrEmpty(input.VerifyingKey_), "verifying key is required.");
        State.CircuitVerifyingKeys[input.CircuitId] = input;
        return new Empty();
    }

    public override VerifyingKey GetVerifyingKey(StringValue input)
    {
        Assert(input != null, "Invalid circuit id.");
        Assert(State.CircuitVerifyingKeys[input.Value] != null, "circuitId not exist");
        return State.CircuitVerifyingKeys[input.Value];
    }

    public override Empty SetOracleAddress(Address input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No SetOracleAddress permission.");
        Assert(input != null, "Invalid address input");
        State.OracleContract.Value = input;
        return new Empty();
    }
    
    //new version that supports trace id that differentiate google apple
    public override Empty StartOracleDataFeedsTask(StartOracleDataFeedsTaskRequest input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No StartOracleDataFeedsTask permission.");
        Assert(input != null, "Invalid StartOracleDataFeedsTaskRequest input.");
        Assert(IsValidGuardianType(input.Type), "Invalid input type.");
        Assert(input.SubscriptionId > 0, "Invalid input subscription id.");
        Assert(State.OidcProviderData[input.Type] != null, "StartOracleDataFeedsTaskRequest OidcProviderData doesn't exist");
        Assert(State.OracleContract.Value != null, "Oracle contract should be set.");

        var specificData = GetJobSepc(State.OidcProviderData[input.Type].JwksEndpoint);
        var specificDataWrapper = new AetherLink.Contracts.DataFeeds.Coordinator.SpecificData
        {
            Data = ByteString.CopyFromUtf8(specificData),
            DataVersion = 0
        }.ToByteString();
        State.OracleContract.SendRequest.Send(new SendRequestInput
        {
            SubscriptionId = input.SubscriptionId,
            RequestTypeIndex = 1,
            SpecificData = specificDataWrapper,
            TraceId = HashHelper.ComputeFrom(State.OidcProviderData[input.Type].Issuer)
        });
        
        State.OidcProviderData[input.Type].SubscriptionId = input.SubscriptionId;
        State.OidcProviderData[input.Type].RequestTypeIndex = 1;
        State.OidcProviderData[input.Type].SpecificData = specificData;
        
        Context.Fire(new OracleDataFeedsTaskStarted
        {
            SubscriptionId = State.OidcProviderData[input.Type].SubscriptionId,
            RequestTypeIndex = State.OidcProviderData[input.Type].RequestTypeIndex
        });
        return new Empty();
    }
    
    private string GetJobSepc(string jwksEndpoint)
    {
        var jobSpecPrefix ="{\"Cron\":\"0 */2 * * * ?\",\"DataFeedsJobSpec\":{\"Type\":\"PlainDataFeeds\",\"Url\":\"";
        var jobSpecSuffix = "\"}}";
        return jobSpecPrefix + jwksEndpoint + jobSpecSuffix;
    }
    
    public override Empty HandleOracleFulfillment(HandleOracleFulfillmentInput input)
    {
        Assert(State.OracleContract.Value == Context.Sender, "Only oracle contract can invoke.");
        Assert(input != null, "Invalid input.");
        Assert(input.RequestTypeIndex > 0, "Invalid request type index.");
        Assert(!input.Response.IsNullOrEmpty(), "Invalid input response.");
        Assert(input.TraceId != null, "Invalid trace id.");

        var responseJson = input.Response.ToStringUtf8();
        var response = Jwks.Parser.ParseJson(responseJson);
        Assert(response != null, "Invalid HandleOracleFulfillmentInput response input.");
        
        SendOracleNoticeReceivedEvent(input, responseJson);
        if (input.TraceId.Equals(HashHelper.ComputeFrom(State.OidcProviderData[GuardianType.OfGoogle].Issuer)))
        {
            foreach (var keyDto in response.Keys)
            {
                SetPublicKeyAndChunks(GuardianType.OfGoogle, keyDto.Kid, keyDto.N);
            }
            ClearCurrentKids(GuardianType.OfGoogle, response.Keys);
            SendOracleNoticeFinished(GuardianType.OfGoogle, responseJson);
        }
        else if (input.TraceId.Equals(HashHelper.ComputeFrom(State.OidcProviderData[GuardianType.OfApple].Issuer)))
        {
            foreach (var keyDto in response.Keys)
            {
                SetPublicKeyAndChunks(GuardianType.OfApple, keyDto.Kid, keyDto.N);
            }
            ClearCurrentKids(GuardianType.OfApple, response.Keys);
            SendOracleNoticeFinished(GuardianType.OfApple, responseJson);
        }
        else
        {
            Assert(false, "Invalid trace id input.");
        }
    
        return new Empty();
    }

    private void SendOracleNoticeReceivedEvent(HandleOracleFulfillmentInput input, string response)
    {
        Context.Fire(new OracleNoticeReceived
        {
            RequestId = input.RequestId,
            Response = response,
            RequestTypeIndex = input.RequestTypeIndex,
            TraceId = input.TraceId,
            Timestamp = Context.CurrentBlockTime.Seconds
        });
    }

    private void SendOracleNoticeFinished(GuardianType guardianType, string response)
    {
        Context.Fire(new OracleNoticeFinished
        {
            GuardianType = (int)guardianType,
            Response = response,
            Timestamp = Context.CurrentBlockTime.Seconds
        });
    }

    private void ClearCurrentKids(GuardianType type, RepeatedField<JwkRecord> jwkRecords)
    {
        State.KidsByGuardianType[type] ??= new CurrentKids()
        {
            Kids = { }
        };
        var currentKids = jwkRecords.Select(jwk => jwk.Kid).ToList();
        var removingKids = State.KidsByGuardianType[type].Kids.Where(kid => !currentKids.Contains(kid)).ToList();
        foreach (var removingKid in removingKids)
        {
            State.KidsByGuardianType[type].Kids.Remove(removingKid);
            if (State.PublicKeysChunksByKid[type][removingKid] != null)
            {
                State.PublicKeysChunksByKid[type].Remove(removingKid);
            }
        }
    }
    
    public override CurrentKids GetGoogleKids(Empty input)
    {
        return State.KidsByGuardianType[GuardianType.OfGoogle];
    }
    
    public override CurrentKids GetAppleKids(Empty input)
    {
        return State.KidsByGuardianType[GuardianType.OfApple];
    }

    public override ZkNonceList GetZkNonceListByCaHash(Hash input)
    {
        Assert(input != null, "Invalid GetZkNonceListByCaHash input.");
        return State.ZkNonceInfosByCaHash[input];
    }
}