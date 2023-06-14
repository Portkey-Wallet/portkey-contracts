using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private async Task CreateHolderDefault()
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
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
        }
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
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
            LoginGuardianIdentifierHash = _guardian
        });
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
    }

    [Fact]
    public async Task SocialRecoveryTest()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });

        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);

        var delegateAllowance = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput()
            {
                DelegateeAddress = caInfo.CaAddress,
                DelegatorAddress = User2Address
            });
        delegateAllowance.Delegations["ELF"].ShouldBe(10000000000000000L);
    }

    [Fact]
    public async Task SocialRecoveryTest_Delegator()
    {
        await SocialRecoveryTest();

        var delegations = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput
            {
                DelegateeAddress = CaContractAddress,
                DelegatorAddress = User2Address
            });

        delegations.Delegations["ELF"].ShouldBe(100_00000000);
    }

    [Fact]
    public async Task SocialRecovery_StrategyTest()
    {
        var hash = await AddGuardianTest();
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = hash
        });

        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");
    }

    [Fact]
    public async Task SocialRecovery_VerifierServerTest()
    {
        await CreateHolderDefault();
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var input = new RemoveVerifierServerInput
        {
            Id = id
        };
        await CaContractStub.RemoveVerifierServer.SendAsync(input);

        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);

        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");
    }

    [Fact]
    public async Task SocialRecovery_InvalidateDocTest()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");

        var guardianApprove1 = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature
                }
            }
        };

        var executionResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove1 }
        });
        executionResult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");
    }

    [Fact]
    public async Task SocialRecovery_Address_Exists()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("ManagerInfo exists");
    }

    [Fact]
    public async Task SocialRecovery_FailedTest()
    {
        await CreateHolderDefault();
        var expiredVerificationTime = DateTime.UtcNow.AddHours(-10);
        var verificationTime = DateTime.UtcNow;
        var signature =
            GenerateSignature(VerifierKeyPair, VerifierAddress, expiredVerificationTime, _guardian, 0);
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var id2 = verifierServer.VerifierServers[1].Id;
        // Verifier signature has expired.
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian},{expiredVerificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");

        //VerificationDoc parse failed. Invalid guardian type name.
        var guardianApprove1 = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo()
                {
                    Id = id,
                    Signature = signature1,
                    VerificationDoc =
                        $"{"abc"},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var executionResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove1 }
        });
        executionResult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");

        //Invalid guardian type.
        var guardianApprove2 = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardianNotExist.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var exeRsult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove2 }
        });
        exeRsult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");

        var guardianApprove3 = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress4.ToBase58()},{Salt}"
                }
            }
        };

        var eresult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove3 }
        });
        eresult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder：guardianCount");

        var guardianApprove4 = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id2,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var inputResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove4 }
        });
        inputResult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");
    }


    [Fact]
    public async Task SocialRecoveryTest_GuardiansApproved()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian
        });

        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        result.TransactionResult.Error.ShouldContain("invalid input Guardians Approved");
    }

    [Fact]
    public async Task SocialRecoveryTest_CaholderIsNotExits()
    {
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian
        });
        result.TransactionResult.Error.ShouldContain("CA Holder does not exist");
    }

    [Fact]
    //SocialRecoveryInput is null;
    public async Task SocialRecoveryTest_inputNull()
    {
        await CreateHolderDefault();
        var socialRecoverySendAsync = await CaContractStub.SocialRecovery.SendWithExceptionAsync(
            new SocialRecoveryInput()
            {
            });
        socialRecoverySendAsync.TransactionResult.Error.ShouldContain("invalid input");
        
        socialRecoverySendAsync = await CaContractStub.SocialRecovery.SendWithExceptionAsync(
            new SocialRecoveryInput
            {
                LoginGuardianIdentifierHash = _guardian,
                ManagerInfo = new ManagerInfo
                {
                    Address = DefaultAddress
                }
            });
        socialRecoverySendAsync.TransactionResult.Error.ShouldContain("invalid input extraData");

        socialRecoverySendAsync = await CaContractStub.SocialRecovery.SendWithExceptionAsync(
            new SocialRecoveryInput
            {
                LoginGuardianIdentifierHash = _guardian,
                ManagerInfo = new ManagerInfo
                {
                    Address = DefaultAddress,
                    ExtraData = ""
                }
            });
        socialRecoverySendAsync.TransactionResult.Error.ShouldContain("invalid input extraData");
    }

    [Fact]
    public async Task SocialRecoveryTest_LoginGuardianIdentifierHashIsNull()
    {
        await CreateHolderDefault();
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
        });
        result.TransactionResult.Error.ShouldContain("invalid input login guardian");
    }

    //manager is null
    [Fact]
    public async Task SocialRecoveryTest_ManagerInfoIsNull()
    {
        await CreateHolderDefault();
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        result.TransactionResult.Error.ShouldContain("invalid input");
    }


    [Fact]
    public async Task SocialRecoveryTest_ManagerInfoExits()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("ManagerInfo exists");

        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SocialRecoveryTest_Guardian()
    {
        await CreateHolderDefault();
        // Guardian_ is "";
        var executionResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = null
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input login guardian");
    }

    [Fact]
    public async Task SocialRecoveryTest_ManagerInfo()
    {
        await CreateHolderDefault();
        //manager is null;
        var exresult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        exresult.TransactionResult.Error.ShouldContain("invalid input");

        //Address  is  null;
        var exeResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = ""
            },
            LoginGuardianIdentifierHash = _guardian
        });
        exeResult.TransactionResult.Error.ShouldContain("invalid input extraData");

        var eResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
            },
            LoginGuardianIdentifierHash = _guardian
        });
        eResult.TransactionResult.Error.ShouldContain("invalid input extraData");
        //Address is null
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = null,
                ExtraData = "123"
            },
            LoginGuardianIdentifierHash = _guardian
        });
        result.TransactionResult.Error.ShouldContain("invalid input");
        //ExtraData is "";
        var executionResult = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = ""
            },
            LoginGuardianIdentifierHash = _guardian
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input extraData");
        //ExtraData is null
        var exceptionAsync = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
            },
            LoginGuardianIdentifierHash = _guardian
        });
        exceptionAsync.TransactionResult.Error.ShouldContain("invalid input extraData");
    }

    [Fact]
    public async Task AddManagerInfoTest()
    {
        await CreateHolderDefault();
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });

        //success
        var manager = new ManagerInfo()
        {
            Address = User2Address,
            ExtraData = "iphone14-2022"
        };
        await CaContractUser1Stub.AddManagerInfo.SendAsync(new AddManagerInfoInput()
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = manager
        });
        var delegateAllowance = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput()
            {
                DelegateeAddress = caInfo.CaAddress,
                DelegatorAddress = User2Address
            });
        delegateAllowance.Delegations["ELF"].ShouldBe(10000000000000000L);
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.ShouldContain(manager);

        //caHolder not exist
        var notExistedCaHash = HashHelper.ComputeFrom("Invalid CaHash");
        var txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(
            new AddManagerInfoInput()
            {
                CaHash = notExistedCaHash,
                ManagerInfo = new ManagerInfo()
                {
                    Address = User2Address,
                    ExtraData = "iphone14-2022"
                }
            });
        txExecutionResult.TransactionResult.Error.ShouldContain("CA holder is null");

        //input caHash is null
        txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput()
        {
            ManagerInfo = new ManagerInfo()
            {
                Address = User2Address,
                ExtraData = "iphone14-2022"
            }
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input CaHash");

        //input manager is null
        txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput()
        {
            CaHash = caInfo.CaHash
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input manager");
    }

    [Fact]
    public async Task AddManagerInfo_Delegator()
    {
        await AddManagerInfoTest();

        var delegations = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput
            {
                DelegateeAddress = CaContractAddress,
                DelegatorAddress = User2Address
            });

        delegations.Delegations["ELF"].ShouldBe(100_00000000);
    }

    [Fact]
    public async Task AddManagerInfo_NoPermissionTest()
    {
        await CreateHolderNoPermission();
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });

        //success
        var manager = new ManagerInfo()
        {
            Address = User3Address,
            ExtraData = "iphone14-2022"
        };
        var result = await CaContractStub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput()
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = manager
        });
        result.TransactionResult.Error.ShouldContain("No Permission");
    }

    [Fact]
    public async Task AddManagerInfo_invalid_input()
    {
        await CreateHolderDefault();
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });

        //input Address is null
        var txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(
            new AddManagerInfoInput()
            {
                CaHash = caInfo.CaHash,
                ManagerInfo = new ManagerInfo()
                {
                    Address = null,
                    ExtraData = "iphone14-2022"
                }
            });
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input manager");

        //inout ExtraData is null
        txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput()
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo()
            {
                Address = User2Address
            }
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input manager");

        //inout ExtraData is ""
        txExecutionResult = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput()
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo()
            {
                Address = User2Address,
                ExtraData = ""
            }
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input manager");
    }

    [Fact]
    public async Task AddManagerInfo_Address_Exists()
    {
        await CreateHolderDefault();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        var result = await CaContractUser1Stub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput
        {
            CaHash = output.CaHash,
            ManagerInfo = new ManagerInfo
            {
                ExtraData = "test",
                Address = User1Address
            }
        });
        result.TransactionResult.Error.ShouldContain("ManagerInfo address exists");
    }

    [Fact]
    public async Task AddManagerInfoTest_Fail_MaxCount()
    {
        await CreateHolder();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });

        for (int i = 2; i < 100; i++)
        {
            await CaContractStub.AddManagerInfo.SendAsync(new AddManagerInfoInput
            {
                CaHash = output.CaHash,
                ManagerInfo = new ManagerInfo
                {
                    Address = Accounts[i].Address,
                    ExtraData = i.ToString()
                }
            });
        }

        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        output.ManagerInfos.Count.ShouldBe(100);

        var result = await CaContractStub.AddManagerInfo.SendWithExceptionAsync(new AddManagerInfoInput
        {
            CaHash = output.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = Accounts[100].Address,
                ExtraData = "100"
            }
        });
        result.TransactionResult.Error.ShouldContain("The amount of ManagerInfos out of limit");
    }

    [Fact]
    public async Task RemoveManagerInfoTest()
    {
        await CreateHolder();
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        //caHolder not existed
        var notExistedCaHash = HashHelper.ComputeFrom("Invalid CaHash");
        var txExecutionResult = await CaContractStub.RemoveManagerInfo.SendWithExceptionAsync(
            new RemoveManagerInfoInput
            {
                CaHash = notExistedCaHash
            });
        txExecutionResult.TransactionResult.Error.ShouldContain("CA holder is null.");

        //input caHash is null
        txExecutionResult = await CaContractStub.RemoveManagerInfo.SendWithExceptionAsync(
            new RemoveManagerInfoInput());
        txExecutionResult.TransactionResult.Error.ShouldContain("invalid input CaHash");

        //input is null can not be 
        /*var managerSendWithExceptionAsync = await CaContractUser1Stub.RemoveManagerInfo.SendWithExceptionAsync(null);
        managerSendWithExceptionAsync.TransactionResult.Error.ShouldContain("invalid input");        */

        //success
        var manager = new ManagerInfo
        {
            Address = User1Address,
            ExtraData = "123"
        };
        await CaContractUser1Stub.RemoveManagerInfo.SendAsync(new RemoveManagerInfoInput
        {
            CaHash = caInfo.CaHash
        });

        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.ShouldNotContain(manager);
        var delegateAllowance = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput
            {
                DelegateeAddress = caInfo.CaAddress,
                DelegatorAddress = User1Address
            });
        delegateAllowance.Delegations.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveManagerInfo_NoPermission_Test()
    {
        await CreateHolderDefault();
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        var manager = new ManagerInfo
        {
            Address = User3Address,
            ExtraData = "123"
        };

        var result = await CaContractStub.RemoveManagerInfo.SendWithExceptionAsync(new RemoveManagerInfoInput
        {
            CaHash = caInfo.CaHash
        });
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task RemoveOtherManagerInfoTest()
    {
        await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
        await CaContractStub.RemoveOtherManagerInfo.SendAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId,
                        Signature = signature,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                    }
                }
            }
        });
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveOtherManagerInfoTest_Fail_InvalidInput()
    {
        await CreateHolder();

        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });

        var result =
            await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput());
        result.TransactionResult.Error.ShouldContain("invalid input CaHash");

        var invalidHash = HashHelper.ComputeFrom("invalid hash");
        result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = invalidHash
        });
        result.TransactionResult.Error.ShouldContain($"CA holder is null.CA hash:{invalidHash}");

        result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash
        });
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");

        result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        result.TransactionResult.Error.ShouldContain("invalid input guardiansApproved");
        
        result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            GuardiansApproved = { new List<GuardianInfo>() }
        });
        result.TransactionResult.Error.ShouldContain("invalid input guardiansApproved");

        result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    IdentifierHash = Hash.Empty,
                    Type = 0,
                    VerificationInfo = new VerificationInfo()
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder：guardianCount:1, guardianApprovedCount:0");
    }
    
    [Fact]
    public async Task RemoveOtherManagerInfoTest_ManagerNotExist()
    {
        await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
        await CaContractStub.RemoveOtherManagerInfo.SendAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User3Address,
                ExtraData = "123"
            },
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId,
                        Signature = signature,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                    }
                }
            }
        });
        
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveOtherManagerInfoTest_Fail_SelfRemove()
    {
        await CreateHolder();
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        var result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = DefaultAddress,
                ExtraData = "123"
            }
        });
        result.TransactionResult.Error.ShouldContain("One should not remove itself");
    }
    
    [Fact]
    public async Task RemoveOtherManagerInfoTest_Fail_NoPermission()
    {
        await CreateCAHolderNoPermission();
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        var result = await CaContractStub.RemoveOtherManagerInfo.SendWithExceptionAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caInfo.CaHash,
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task RemoveOtherManagerInfoTest_GuardianApproved()
    {
        var caHash = await AddGuardian();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.GuardianList.Guardians.Count.ShouldBe(4);
        output.ManagerInfos.Count.ShouldBe(2);
        
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0);
        var signature2 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian2, 0);
        
        var guardianApprove = new List<GuardianInfo>
        {
            new ()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        };

        var result = await CaContractUser1Stub.RemoveOtherManagerInfo.SendWithExceptionAsync(
            new RemoveOtherManagerInfoInput
            {
                CaHash = caHash,
                ManagerInfo = new ManagerInfo
                {
                    Address = DefaultAddress,
                    ExtraData = "123"
                },
                GuardiansApproved = { guardianApprove }
            });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder：guardianCount:4, guardianApprovedCount:1");
        
        guardianApprove.AddRange(new []
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc = ""
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature2,
                    VerificationDoc = ""
                }
            }
        });
        
        result = await CaContractUser1Stub.RemoveOtherManagerInfo.SendWithExceptionAsync(
            new RemoveOtherManagerInfoInput
            {
                CaHash = caHash,
                ManagerInfo = new ManagerInfo
                {
                    Address = DefaultAddress,
                    ExtraData = "123"
                },
                GuardiansApproved = { guardianApprove }
            });
        result.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder：guardianCount:4, guardianApprovedCount:1");

        guardianApprove.RemoveAt(2);
        guardianApprove.RemoveAt(1);
        guardianApprove.AddRange(new []
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc = $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature2,
                    VerificationDoc = $"{0},{_guardian2.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            }
        });

        await CaContractUser1Stub.RemoveOtherManagerInfo.SendAsync(new RemoveOtherManagerInfoInput
        {
            CaHash = caHash,
            ManagerInfo = new ManagerInfo
            {
                Address = DefaultAddress,
                ExtraData = "123"
            },
            GuardiansApproved = { guardianApprove }
        });

        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateManagerInfo()
    {
        var caHash = await CreateHolder();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");

        await CaContractStub.UpdateManagerInfos.SendAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash,
            ManagerInfos =
            {
                new ManagerInfo
                {
                    Address = User1Address,
                    ExtraData = "456"
                }
            }
        });
        
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("456");
    }
    
    [Fact]
    public async Task UpdateManagerInfo_Fail_InvalidInput()
    {
        var caHash = await CreateHolder();

        var result = await CaContractStub.UpdateManagerInfos.SendWithExceptionAsync(new UpdateManagerInfosInput());
        result.TransactionResult.Error.ShouldContain("invalid input CaHash");
        
        result = await CaContractStub.UpdateManagerInfos.SendWithExceptionAsync(new UpdateManagerInfosInput
        {
            CaHash = Hash.Empty
        });
        result.TransactionResult.Error.ShouldContain($"CA holder is null.CA hash:{Hash.Empty}");
        
        result = await CaContractStub.UpdateManagerInfos.SendWithExceptionAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash
        });
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");
        
        result = await CaContractStub.UpdateManagerInfos.SendWithExceptionAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash,
            ManagerInfos = {}
        });
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");
    }

    [Fact]
    public async Task UpdateManagerInfo_ManagerInfoNotExists()
    {
        var caHash = await CreateHolder();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");

        await CaContractStub.UpdateManagerInfos.SendAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash,
            ManagerInfos =
            {
                new ManagerInfo
                {
                    Address = User2Address,
                    ExtraData = "456"
                }
            }
        });
        
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");
    }

    [Fact]
    public async Task UpdateManagerInfo_SameManagerInfo()
    {
        var caHash = await CreateHolder();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");

        await CaContractStub.UpdateManagerInfos.SendAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash,
            ManagerInfos =
            {
                new ManagerInfo
                {
                    Address = User1Address,
                    ExtraData = "123"
                }
            }
        });
        
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");
    }
    
    [Fact]
    public async Task UpdateManagerInfo_MixedManagerInfo()
    {
        var caHash = await CreateHolder();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("123");

        var transactionResult = await CaContractStub.UpdateManagerInfos.SendAsync(new UpdateManagerInfosInput
        {
            CaHash = caHash,
            ManagerInfos =
            {
                new ManagerInfo
                {
                    Address = User1Address,
                    ExtraData = "123"
                },
                new ManagerInfo
                {
                    Address = User2Address,
                    ExtraData = "456"
                },
                new ManagerInfo
                {
                    Address = User1Address,
                    ExtraData = "789"
                }
            }
        });
        
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        
        output.ManagerInfos.Count.ShouldBe(2);
        output.ManagerInfos[0].Address.ShouldBe(User1Address);
        output.ManagerInfos[0].ExtraData.ShouldBe("789");
    }

    private async Task CreateHolderNoPermission()
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
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = "url",
                EndPoints = { "127.0.0.1" },
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
    }
}