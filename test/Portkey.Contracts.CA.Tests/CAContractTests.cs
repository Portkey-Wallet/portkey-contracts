using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests : CAContractTestBase
{
    [Fact]
    public async Task CreateHolderTest()
    {
        var verificationTime = DateTime.UtcNow;
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
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
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
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

        var guardian = new Guardian
        {
            IdentifierHash = _guardian,
            Type = GuardianType.OfEmail,
            VerifierId = _verifierId
        };
        getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, guardian);
        var verificationTime = DateTime.UtcNow;
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
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
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
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
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var executionResult = await CaContractStub.CreateCAHolder.SendWithExceptionAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = _guardianEmpty,
                VerificationInfo = new VerificationInfo
                {
                    Id = new Hash(),
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Guardian verification failed.");
    }

    [Fact]
    public async Task CreateHolderTest_Fail_ManagerInfo_Null()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            },
            ManagerInfo = null
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input manager");
    }

    /*[Fact]
    public async Task CreateHolder_fail_invalid_guardian()
    {
        // createCaHolder
        var verificationTime = DateTime.UtcNow;
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, GuardianType, 0);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = GuardianType,
                    Type = 0
                },
                Verifier = new Verifier
                {
                    Name = VerifierName,
                    Signature = signature,
                    VerificationDoc = $"{0},{GuardianType},{verificationTime},{VerifierAddress.ToBase58()}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianType = GuardianType
        });
        holderInfo.GuardianList.Guardians.First().GuardianType.GuardianType_.ShouldBe(GuardianType);
        var caHash = holderInfo.CaHash;
        // AddGuardian
        // await AddGuardian();
        var guardianApprove = new List<Guardian>
        {
            new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = GuardianType,
                    Type = 0
                },
                Verifier = new Verifier
                {
                    Name = VerifierName,
                    Signature = signature,
                    VerificationDoc = $"{0},{GuardianType},{verificationTime},{VerifierAddress.ToBase58()}"
                }
            },
            
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = GuardianType2,
                    Type = 0
                },
                Verifier = new Verifier
                {
                    Name = VerifierName,
                    Signature = signature,
                    VerificationDoc = $"{0},{GuardianType2},{verificationTime},{VerifierAddress.ToBase58()}"
                }
            },
            GuardiansApproved = {guardianApprove}
        };
        await CaContractStub.AddGuardian.SendAsync(input);
        
        // setLoginTypeForGuardian 
        // await SetLoginGuardianType_Succeed_Test();
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            GuardianType =  new GuardianType
            {
                Type = GuardianTypeType.GuardianTypeOfEmail,
                GuardianType_ = GuardianType1
            }
        });

        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            
            GuardianApproved = new Guardian
            {
                GuardianType = new GuardianType
                {
                    GuardianType_ = "1@google.com",
                    Type = 0
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        

    }*/

    [Fact]
    public async Task CreateHolderTest_Delegator()
    {
        await CreateHolderTest();

        var delegations = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput
            {
                DelegateeAddress = CaContractAddress,
                DelegatorAddress = User1Address
            });

        delegations.Delegations["ELF"].ShouldBe(100_00000000);
    }

    private async Task<TransactionFeeDelegations> GetDelegator_ByCaHash_Helper(Hash caHash)
    {
        var hashCode = caHash.ToString();
        TransactionFeeDelegations delegations =
            await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
                new GetTransactionFeeDelegationsOfADelegateeInput
                {
                    DelegateeAddress = Address.FromBase58(hashCode),
                    DelegatorAddress = null
                });
        return delegations;
    }
}