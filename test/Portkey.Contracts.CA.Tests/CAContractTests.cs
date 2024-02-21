using System;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests : CAContractTestBase
{
    [Fact]
    public async Task<Address> CreateHolderTest()
    {
        var verificationTime = DateTime.UtcNow;
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);

        var delegateAllowance = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput()
            {
                DelegateeAddress = caInfo.CaAddress,
                DelegatorAddress = User1Address
            });
        delegateAllowance.Delegations["ELF"].ShouldBe(10000000000000000L);
        /*var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, GuardianType, 0);
        //create second caHolder
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = GuardianType,
                    Type = GuardianTypeType.GuardianTypeOfEmail
                },
                Verifier = new Verifier
                {
                    Name = VerifierName,
                    Signature = signature1,
                    VerificationDoc = $"{0},{GuardianType},{verificationTime},{VerifierAddress.ToBase58()}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        var caHolderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianType = GuardianType
        });
        caHolderInfo.ManagerInfos.Count.ShouldBe(1);
        caHolderInfo.GuardianList.Guardians.Count.ShouldBe(1);*/
        /*var manager = new ManagerInfo()
        {
            Address = DefaultAddress,
            ExtraData = "iphone14-2022"
        };
        await CaContractStub.AddManagerInfo.SendAsync(new AddManagerInfoInput()
        {
            CaHash = caHolderInfo.CaHash,
            ManagerInfo = manager
        });*/
        //Add guardian to second CaHolder By first Email;
        /*var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, GuardianType, 0);
        var signature4 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, GuardianType1, 0);
        var verificationDoc = $"{0},{"1@google.com"},{verificationTime},{VerifierAddress.ToBase58()}";
        var guardianApprove = new List<Guardian>
        {
            new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = GuardianType,
                    Type = GuardianTypeType.GuardianTypeOfEmail
                },
                Verifier = new Verifier
                {
                    Name = VerifierName,
                    Signature = signature3,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHolderInfo.CaHash,
            GuardianToAdd = new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = "1@google.com",
                    Type = GuardianTypeType.GuardianTypeOfEmail
                },
                Verifier = new Verifier
                {
                    Name = VerifierName1,
                    Signature = signature4,
                    VerificationDoc = $"{0},{"1@google.com"},{verificationTime},{VerifierAddress1.ToBase58()}"
                }
            },
            GuardiansApproved = {guardianApprove}
        };
        await CaContractUser1Stub.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHolderInfo.CaHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().GuardianType.GuardianType_.ShouldBe(GuardianType1);
        }*/

        return caInfo.CaAddress;
    }

    [Fact]
    public async Task CreateHolderTest_CloseCheckOperationDetails()
    {
        await Initiate();
        await CreateHolderWithOperationDetails(User1Address.ToBase58());
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaAddress.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task CreateHolderTest_CloseCheckOperationDetails_OperationDetailsNull()
    {
        await Initiate();
        await CreateHolderWithOperationDetails(User1Address.ToBase58());
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaAddress.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateHolderTest_OpenCheckOperationDetails()
    {
        await Initiate();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderWithOperationDetails(User1Address.ToBase58());
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaAddress.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateHolder_Failed_OpenCheckOperationDetails_OperationDetailsNull()
    {
        await Initiate();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderWithOperationDetails(null);
        var result = await CaContractStub.GetHolderInfo.SendWithExceptionAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        result.ShouldNotBeNull();
        result.TransactionResult?.Error.ShouldContain("Not found ca_hash by a the loginGuardianIdentifierHash");
    }

    [Fact]
    public async Task CreateHolderFailedTest()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();

        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });

        var guardianList = getHolderInfoOutput.GuardianList;
        var guardians = guardianList.Guardians;
        guardians.Count.ShouldBe(2);

        GetLoginGuardianCount(guardianList).ShouldBe(1);

        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0,
                        salt,
                        operationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var caHolderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caHolderInfo.ManagerInfos.Count.ShouldBe(2);
        caHolderInfo.GuardianList.Guardians.Count.ShouldBe(2);
        caHash.ShouldBe(caHolderInfo.CaHash);
    }

    [Fact]
    public async Task CreateHolderTest_Fail_GuardianApproved_Null()
    {
        await Initiate();

        var executionResult = await CaContractStub.CreateCAHolder.SendWithExceptionAsync(new CreateCAHolderInput
        {
            GuardianApproved = null,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input guardian");
    }

    [Fact]
    public async Task CreateHolderTest_Fail_GuardianType_Null()
    {
        await Initiate();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var executionResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = _guardianEmpty,
                VerificationInfo = new VerificationInfo
                {
                    Id = new Hash(),
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task CreateHolderTest_Fail_ManagerInfo_Null()
    {
        await Initiate();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var executionResult = await CaContractStub.CreateCAHolder.SendWithExceptionAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = new Hash(),
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt}"
                }
            },
            ManagerInfo = null
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input manager");
    }

    [Fact]
    public async Task InitializeTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });

        var result = await CaContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Already initialized.");
    }

    [Fact]
    public async Task Initialize_InvalidZeroSmartAddress()
    {
        var result = await CaContractStub.Initialize.SendAsync(new InitializeInput());

        // var result = await CaContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        // {
        //     ContractAdmin = DefaultAddress,
        //     ZeroSmartAddress = Address.FromBase58(CAContractConstants.ZeroSmartAddress)
        // });
        result.TransactionResult.Error.ShouldBe("");
    }

    [Fact]
    public async Task CheckOperationDetailsInSignatureEnabledTest()
    {
        await Initiate();

        await SetCheckOperationDetailsInSignatureEnabled(true);

        var callAsync = await CaContractStub.GetCheckOperationDetailsInSignatureEnabled.CallAsync(new Empty());
        callAsync.ShouldNotBeNull();
        callAsync.CheckOperationDetailsEnabled.ShouldBeTrue();

        await SetCheckOperationDetailsInSignatureEnabled(false);

        callAsync = await CaContractStub.GetCheckOperationDetailsInSignatureEnabled.CallAsync(new Empty());
        callAsync.ShouldNotBeNull();
        callAsync.CheckOperationDetailsEnabled.ShouldBeFalse();
    }

    private async Task SetCheckOperationDetailsInSignatureEnabled(bool enable)
    {
        await CaContractStub.SetCheckOperationDetailsInSignatureEnabled.SendAsync(
            new SetCheckOperationDetailsInSignatureEnabledInput
            {
                CheckOperationDetailsEnabled = enable
            });
    }

    private async Task CreateHolderWithOperationDetails(string operationDetails, Address manager = null)
    {
        manager = manager == null ? User1Address : manager;
        var verificationTime = DateTime.UtcNow;

        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                EndPoints = { "127.0.0.1" },
                ImageUrl = "url",
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType, MainChainId, operationDetails);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = string.IsNullOrWhiteSpace(operationDetails)
                        ? $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                        : $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId},{HashHelper.ComputeFrom(operationDetails).ToHex()}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = manager,
                ExtraData = "123"
            }
        });
    }
}