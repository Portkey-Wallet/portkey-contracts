using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf;
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
            ContractAdmin = DefaultAddress,
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
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
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
    public async Task<Address> SocialRecoveryTest()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove },
            ProjectCode = "123"
        });

        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        var invited = Invited.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(Invited)).NonIndexed);
        invited.CaHash.ShouldBe(caInfo.CaHash);
        invited.ContractAddress.ShouldBe(CaContractAddress);
        invited.ProjectCode.ShouldBe("123");
        invited.ReferralCode.ShouldBeEmpty();
        invited.MethodName.ShouldBe("SocialRecovery");

        var delegateAllowance = await TokenContractStub.GetTransactionFeeDelegationsOfADelegatee.CallAsync(
            new GetTransactionFeeDelegationsOfADelegateeInput()
            {
                DelegateeAddress = caInfo.CaAddress,
                DelegatorAddress = User2Address
            });
        delegateAllowance.Delegations["ELF"].ShouldBe(10000000000000000L);

        return caInfo.CaAddress;
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
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("");
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
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);

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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task SocialRecovery_InvalidateDocTest()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
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
                    VerificationDoc = $"{0},{_guardian},{VerifierAddress.ToBase58()},{salt}"
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldBe("");

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

        var executionResult = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove1 }
        });
        executionResult.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task SocialRecovery_Address_Exists()
    {
        await CreateHolderDefault();
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian,
            0, salt, operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature =
            GenerateSignature(VerifierKeyPair, VerifierAddress, expiredVerificationTime, _guardian, 0, salt,
                operationType);
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt1,
            operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian},{expiredVerificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        result.TransactionResult.Error.ShouldContain("Could not find any recognizable digits.");

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
                        $"{"abc"},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}"
                }
            }
        };

        var executionResult = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove1 }
        });
        executionResult.TransactionResult.Error.ShouldContain("");

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
                        $"{0},{_guardianNotExist.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}"
                }
            }
        };

        var exeRsult = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove2 }
        });
        exeRsult.TransactionResult.Error.ShouldBe("");

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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(3)},{VerifierAddress4.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };

        var eresult = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove3 }
        });
        eresult.TransactionResult.Error.ShouldBe("");

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
                    VerificationDoc = $"{0},{_guardian},{VerifierAddress.ToBase58()},{salt}"
                }
            }
        };

        var inputResult = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput()
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove4 }
        });
        inputResult.TransactionResult.Error.ShouldBe("");
    }


    [Fact]
    public async Task SocialRecoveryTest_GuardiansApproved()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(10), _guardian,
            0, salt, operationType);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
    public async Task SocialRecoveryTest_Signature()
    {
        await CreateHolderDefault();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, operationType, SideChianId);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{SideChianId}"
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
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldNotBe(User2Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
    }
    
    [Fact]
    public async Task SocialRecoveryTest_OpenCheckOperationDetail()
    {
        await CreateHolderDefault();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, operationType, SideChianId, User2Address.ToBase58());
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{SideChianId},{HashHelper.ComputeFrom(User2Address.ToBase58()).ToHex()}"
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
        caInfo.ManagerInfos.Last().Address.ShouldBe(User2Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
    }
    
    [Fact]
    public async Task SocialRecoveryTest_Fail_OpenCheckOperationDetail()
    {
        await CreateHolderDefault();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, operationType, SideChianId);
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
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{SideChianId}"
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
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldNotBe(User2Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
    }

    [Fact]
    public async Task<Address> AddManagerInfoTest()
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

        return caInfo.CaAddress;
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

        for (int i = 2; i < 70; i++)
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
        output.ManagerInfos.Count.ShouldBe(70);

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
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveOtherManagerInfo).ToString();
        var operateDetails = $"{User1Address.ToBase58()}";
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType, MainChainId, operateDetails);
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
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}," +
                            $"{MainChainId},{HashHelper.ComputeFrom(operateDetails).ToHex()}"
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

        result = await CaContractStub.RemoveOtherManagerInfo.SendAsync(new RemoveOtherManagerInfoInput
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
        result.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task RemoveOtherManagerInfoTest_ManagerNotExist()
    {
        await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveOtherManagerInfo).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
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
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        var salt = Guid.NewGuid().ToString("N");
        var removeOperationType = Convert.ToInt32(OperationType.RemoveOtherManagerInfo).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(50), _guardian,
            0, salt, removeOperationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(100), _guardian1, 0, salt,
                removeOperationType);
        var signature2 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(200),
            _guardian2, 0, salt, removeOperationType);

        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(50)},{VerifierAddress.ToBase58()},{salt},{removeOperationType},{MainChainId}"
                }
            }
        };

        var result = await CaContractUser1Stub.RemoveOtherManagerInfo.SendAsync(
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
        result.TransactionResult.Error.ShouldContain("");

        guardianApprove.AddRange(new[]
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

        result = await CaContractUser1Stub.RemoveOtherManagerInfo.SendAsync(
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
        result.TransactionResult.Error.ShouldBe("");

        guardianApprove.RemoveAt(2);
        guardianApprove.RemoveAt(1);
        guardianApprove.AddRange(new[]
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(100)},{VerifierAddress1.ToBase58()},{salt},{removeOperationType}"
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
                    VerificationDoc =
                        $"{0},{_guardian2.ToHex()},{verificationTime.AddSeconds(200)},{VerifierAddress.ToBase58()},{salt},{removeOperationType}"
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

        output.ManagerInfos.Count.ShouldBe(2);
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
            ManagerInfos = { }
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

    [Fact]
    public async Task SetForbiddenForwardCallContractMethodTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.SetForbiddenForwardCallContractMethod.SendAsync(
            new SetForbiddenForwardCallContractMethodInput
            {
                MethodName = "TestForbiddenForwardCallContractMethod",
                Address = DefaultAddress,
                Forbidden = true
            });
    }

    [Fact]
    public async Task ManagerApproveTest()
    {
        await InitTransferLimitTest();

        var approveVerifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var approveOpType = Convert.ToInt32(OperationType.Approve).ToString();
        var operateDetails = $"{User2Address.ToBase58()}_ELF_10000"; 
        var approveSign = GenerateSignature(VerifierKeyPair, VerifierAddress, approveVerifyTime, _guardian, 0, salt,
            approveOpType, MainChainId, operateDetails);
        await CaContractStubManagerInfo1.ManagerApprove.SendAsync(new ManagerApproveInput
        {
            CaHash = _transferLimitTestCaHash,
            Spender = User2Address,
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierServers[0].Id,
                        Signature = approveSign,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{approveVerifyTime},{VerifierAddress.ToBase58()},{salt},{approveOpType}," +
                            $"{MainChainId},{HashHelper.ComputeFrom(operateDetails).ToHex()}"
                    }
                }
            },
            Symbol = "ELF",
            Amount = 10000
        });
        
        var executionResult = await CaContractStubManagerInfo1.ManagerApprove.SendWithExceptionAsync(new ManagerApproveInput
        {
            CaHash = _transferLimitTestCaHash,
            Spender = User2Address,
            Symbol = "ELF",
            Amount = 10000
        });
        executionResult.TransactionResult.Error.ShouldContain("invalid input Guardians Approved");
        await CaContractStub.AddManagerApproveSpenderWhitelist.SendAsync(
            new AddManagerApproveSpenderWhitelistInput
            {
                SpenderList = { User2Address }
            });
        await CaContractStubManagerInfo1.ManagerApprove.SendAsync(new ManagerApproveInput
        {
            CaHash = _transferLimitTestCaHash,
            Spender = User2Address,
            Symbol = "ELF",
            Amount = 10000
        });
    }

    [Fact]
    public async Task ManagerApproveSpenderWhitelistTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            Symbol = "ELF",
            To = User1Address,
            Amount = 10000000000
        });
        var executionResult = await CaContractStubManagerInfo1.AddManagerApproveSpenderWhitelist.SendWithExceptionAsync(
            new AddManagerApproveSpenderWhitelistInput
            {
                SpenderList = {  }
            });
        executionResult.TransactionResult.Error.ShouldContain("Invalid input");
        var executionResult1 = await CaContractStubManagerInfo1.AddManagerApproveSpenderWhitelist.SendWithExceptionAsync(
            new AddManagerApproveSpenderWhitelistInput
            {
                SpenderList = { TokenContractAddress }
            });
        executionResult1.TransactionResult.Error.ShouldContain("No permission.");
        await CaContractStub.AddManagerApproveSpenderWhitelist.SendAsync(
            new AddManagerApproveSpenderWhitelistInput
            {
                SpenderList = { TokenContractAddress }
            });
        var checkSpender = await CaContractStub.CheckInManagerApproveSpenderWhitelist.CallAsync(TokenContractAddress);
        checkSpender.Value.ShouldBeTrue();
        
        var executionResult5 = await CaContractStubManagerInfo1.RemoveManagerApproveSpenderWhitelist.SendWithExceptionAsync(
            new RemoveManagerApproveSpenderWhitelistInput
            {
                SpenderList = {  }
            });
        executionResult5.TransactionResult.Error.ShouldContain("Invalid input");
        var executionResult6 = await CaContractStubManagerInfo1.RemoveManagerApproveSpenderWhitelist.SendWithExceptionAsync(
            new RemoveManagerApproveSpenderWhitelistInput
            {
                SpenderList = { TokenContractAddress }
            });
        executionResult6.TransactionResult.Error.ShouldContain("No permission.");
        await CaContractStub.RemoveManagerApproveSpenderWhitelist.SendAsync(
            new RemoveManagerApproveSpenderWhitelistInput
            {
                SpenderList = { TokenContractAddress }
            });
        var checkSpender1 = await CaContractStub.CheckInManagerApproveSpenderWhitelist.CallAsync(TokenContractAddress);
        checkSpender1.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ManagerApprove_ErrorOperationTypeTest()
    {
        await InitTransferLimitTest();

        var approveVerifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var approveOpType = Convert.ToInt32(OperationType.ModifyTransferLimit).ToString();
        var approveSign = GenerateSignature(VerifierKeyPair, VerifierAddress, approveVerifyTime, _guardian, 0, salt,
            approveOpType);
        var executionResult = await CaContractStubManagerInfo1.ManagerApprove.SendWithExceptionAsync(
            new ManagerApproveInput
            {
                CaHash = _transferLimitTestCaHash,
                Spender = User2Address,
                GuardiansApproved =
                {
                    new GuardianInfo
                    {
                        IdentifierHash = _guardian,
                        Type = GuardianType.OfEmail,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierServers[0].Id,
                            Signature = approveSign,
                            VerificationDoc =
                                $"{0},{_guardian.ToHex()},{approveVerifyTime},{VerifierAddress.ToBase58()},{salt},{approveOpType},{MainChainId}"
                        }
                    }
                },
                Symbol = "ELF",
                Amount = 10000
            });
        executionResult.TransactionResult.Error.ShouldContain("JudgementStrategy validate failed");
    }

    private async Task CreateHolderNoPermission()
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
        var salt = Guid.NewGuid().ToString("N");
        var operation = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operation);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operation},{MainChainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        });
    }
    
    [Fact]
    public async Task<Address> SocialRecovery_ZKLoginTest()
    {
        await CreateHolderWithZkLogin();
        const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                IdentifierHash = Hash.LoadFromHex("6b9ef910ee5f37b307b2320bc6b090af64a7accbb00f49fae5b8677d13a51276"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8"),
                    // Signature = signature,
                    // VerificationDoc =
                    //     $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("6b9ef910ee5f37b307b2320bc6b090af64a7accbb00f49fae5b8677d13a51276"),
                    Salt = "801fed43d8e940448297e0054cde0749",
                    Nonce = "3e59cecd0fa87632a2aba8334a67333a0943f1c620e8b87c5a6486dc76edb3bb",
                    ZkProof = "{\"pi_a\":[\"1835711353314891866773996467945488412122056560399852492499629420145170775603\",\"10929121428644391988093906032220514214996670629503418705298966308304083400106\",\"1\"],\"pi_b\":[[\"18840146228960295689846947870771810741659888162582722386683639248379595868363\",\"11894272382473924104688351934179005172676498238074801709706784611023997321944\"],[\"8515913670521307872158311121057301365057464770440093699290751666385314360789\",\"7313327600451314634196668630518506974337603850525245782560483249691296051706\"],[\"1\",\"0\"]],\"pi_c\":[\"12244886529014467607914084594279537498503442753395914603866890175886993868598\",\"7419086538599063374544143314983188234654827591882882187825948319833587492293\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "1835711353314891866773996467945488412122056560399852492499629420145170775603","10929121428644391988093906032220514214996670629503418705298966308304083400106","1" },
                        ZkProofPiB1 = { "18840146228960295689846947870771810741659888162582722386683639248379595868363","11894272382473924104688351934179005172676498238074801709706784611023997321944" },
                        ZkProofPiB2 = { "8515913670521307872158311121057301365057464770440093699290751666385314360789","7313327600451314634196668630518506974337603850525245782560483249691296051706" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "12244886529014467607914084594279537498503442753395914603866890175886993868598","7419086538599063374544143314983188234654827591882882187825948319833587492293","1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        AddManagerAddress = new AddManager
                        {
                            CaHash = Hash.Empty,
                            ManagerAddress = Address.FromBase58("SBqd65m6H1WaJxgAbDtgM4G2voT26EJcUWAZSVCAkoWpZFcuo"),
                            Timestamp = new Timestamp
                            {
                                Seconds = 1721203238,
                                Nanos = 912000000
                            }
                        }
                    }
                }
            }
        };

        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = Address.FromBase58("SBqd65m6H1WaJxgAbDtgM4G2voT26EJcUWAZSVCAkoWpZFcuo"),
                ExtraData = "{\"transactionTime\":1721612262260,\"deviceInfo\":\"7cnulxkD5S2l809oJtNE3yng6pXWqeLzCAIMd+BKApgZ94hkW51Yl566M9mqUC81\",\"version\":\"2.0.0\"}"
            },
            LoginGuardianIdentifierHash = Hash.LoadFromHex("6b9ef910ee5f37b307b2320bc6b090af64a7accbb00f49fae5b8677d13a51276"),
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("");
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("6b9ef910ee5f37b307b2320bc6b090af64a7accbb00f49fae5b8677d13a51276")
        });
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        var managerInfoSocialRecovered = ManagerInfoSocialRecovered.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(ManagerInfoSocialRecovered)).NonIndexed);
        managerInfoSocialRecovered.CaHash.ShouldBe(caInfo.CaHash);
        managerInfoSocialRecovered.CaAddress.ShouldBe(caInfo.CaAddress);
        return caInfo.CaAddress;
    }
    
    [Fact]
    public async Task<Address> SocialRecovery_OldUser_ZKLoginTest()
    {
        await CreateHolderWithGoogleAccount();
        const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
        var identifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f");
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                IdentifierHash = identifierHash,
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("ed811d099f03e5fe719bf2279816bd73b9d62600467a49db038556625cb9941a"),
                    Salt = "619b1b216aad43a18383b68244105502",
                    Nonce = "b341f8925c6a9bd18e789f090330eb779119a65f488bdf4d5b7ab2ce722fefdd",
                    ZkProof = "{\"pi_a\":[\"8847341809119341539986277480118678645015398950965498836358658385136783409528\",\"35889733690489589413933147955398800746698985857111843744354947119618976973\",\"1\"],\"pi_b\":[[\"8829218332452874761351108900792848856688278006377326095018497856652241335313\",\"6475745852872692972729403199842149148418689221004301160989041740103763308288\"],[\"19912733143310881368112508383519815348299026082901846504129125803172038134240\",\"4865595519414308381320873127924994199818069125193398721282295451946731822067\"],[\"1\",\"0\"]],\"pi_c\":[\"12451434721157118723129478379973545168431931504742539290689011581723390315029\",\"6865908500956857395032826212874972934315751614569712624608015895613231300435\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "8847341809119341539986277480118678645015398950965498836358658385136783409528","35889733690489589413933147955398800746698985857111843744354947119618976973","1" },
                        ZkProofPiB1 = { "8829218332452874761351108900792848856688278006377326095018497856652241335313","6475745852872692972729403199842149148418689221004301160989041740103763308288" },
                        ZkProofPiB2 = { "19912733143310881368112508383519815348299026082901846504129125803172038134240","4865595519414308381320873127924994199818069125193398721282295451946731822067" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "12451434721157118723129478379973545168431931504742539290689011581723390315029","6865908500956857395032826212874972934315751614569712624608015895613231300435","1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        AddManagerAddress = new AddManager
                        {
                            CaHash = Hash.Empty,
                            ManagerAddress = Address.FromBase58("2qmHosxRmUu5h8gr2oCQLp55sDr7PEFcUP5pgLtj5PyVdUHmtT"),
                            Timestamp = new Timestamp
                            {
                                Seconds = 1721203238,
                                Nanos = 912000000
                            }
                        }
                    }
                }
            }
        };
    
        var result = await CaContractStub.SocialRecovery.SendAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = Address.FromBase58("2qmHosxRmUu5h8gr2oCQLp55sDr7PEFcUP5pgLtj5PyVdUHmtT"),
                ExtraData = "{\"transactionTime\":1721654972056,\"deviceInfo\":\"qDX/QQazJST/K32/6+nHk3EhymKFVWkvfz8A55EXrpAjY7Igdcyh5vLNqgSeQcOh\",\"version\":\"2.0.0\"}"
            },
            LoginGuardianIdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("");
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f")
        });
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        var managerInfoSocialRecovered = ManagerInfoSocialRecovered.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(ManagerInfoSocialRecovered)).NonIndexed);
        managerInfoSocialRecovered.CaHash.ShouldBe(caInfo.CaHash);
        managerInfoSocialRecovered.CaAddress.ShouldBe(caInfo.CaAddress);
        return caInfo.CaAddress;
    }
}