using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private readonly Hash _guardian;
    private readonly Hash _guardian1;
    private readonly Hash _guardian2;
    private readonly Hash _guardianNotExist;
    private readonly Hash _guardianEmpty;
    private const string VerifierName = "HuoBi";
    private readonly Hash _verifierIdEmpty;
    private Hash _verifierId;
    private const string VerifierName1 = "PortKey";
    private Hash _verifierId1;
    private const string VerifierName2 = "Binance";
    private Hash _verifierId2;
    private const string ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Portkey.png";
    private const string Salt = "salt";

    public CAContractTests()
    {
        _guardian = HashHelper.ComputeFrom("test@google.com");
        _guardian1 = HashHelper.ComputeFrom("test1@google.com");
        _guardian2 = HashHelper.ComputeFrom("test2@google.com");
        _guardianNotExist = HashHelper.ComputeFrom("NotExists@google.com");
        _guardianEmpty = HashHelper.ComputeFrom("");
        _verifierIdEmpty = HashHelper.ComputeFrom("");
    }

    private ByteString GenerateSignature(ECKeyPair verifier, Address verifierAddress,
        DateTime verificationTime, Hash guardianType, int type, string salt, string operationType)
    {
        if (string.IsNullOrWhiteSpace(salt))
        {
            salt = Salt;
        }

        var data =
            $"{type},{guardianType.ToHex()},{verificationTime},{verifierAddress.ToBase58()},{salt},{operationType}";
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(verifier.PrivateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private async Task<Hash> CreateHolder()
    {
        var verificationTime = DateTime.UtcNow;

        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = true
        });

        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
        }
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(10), _guardian,
            0, salt, operationType);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType}"
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
        holderInfo.GuardianList.Guardians.First().IdentifierHash.ShouldBe(_guardian);

        //success
        var manager = new ManagerInfo
        {
            Address = DefaultAddress,
            ExtraData = "iphone14-2022"
        };
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
        await CaContractUser1Stub.AddManagerInfo.SendAsync(new AddManagerInfoInput
        {
            CaHash = holderInfo.CaHash,
            ManagerInfo = manager
        });

        return holderInfo.CaHash;
    }

    private async Task<Hash> CreateCAHolderNoPermission()
    {
        var verificationTime = DateTime.UtcNow;
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = true
        });
        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
        }
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}"
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
        holderInfo.GuardianList.Guardians.First().IdentifierHash.ShouldBe(_guardian);

        return holderInfo.CaHash;
    }

    [Fact]
    public async Task<Hash> AddGuardianTest()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0, salt,
                operationType);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }

    [Fact]
    public async Task AddGuardianTest_failed_validateType()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var removeOperationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            removeOperationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0, salt,
                removeOperationType);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{removeOperationType}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{removeOperationType}"
                }
            },

            GuardiansApproved = { guardianApprove }
        };
        var result = await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        result.TransactionResult.Error.ShouldContain("");
    }


    [Fact]
    public async Task AddGuardianTest_failed_duplicate_verifiedDoc()
    {
        var verificationTime = DateTime.UtcNow;
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = true
        });
        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
        }
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(10), _guardian,
            0, salt, operationType);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType}"
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
        holderInfo.GuardianList.Guardians.First().IdentifierHash.ShouldBe(_guardian);

        //success
        var manager = new ManagerInfo
        {
            Address = DefaultAddress,
            ExtraData = "iphone14-2022"
        };
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
        await CaContractUser1Stub.AddManagerInfo.SendAsync(new AddManagerInfoInput
        {
            CaHash = holderInfo.CaHash,
            ManagerInfo = manager
        });

        var caHash = holderInfo.CaHash;

        var addGuardianOperationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddMinutes(1), _guardian,
            0, Salt, addGuardianOperationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddMinutes(2), _guardian1, 0, Salt,
                addGuardianOperationType);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime.AddMinutes(1)},{VerifierAddress.ToBase58()},{Salt},{addGuardianOperationType}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature1,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{addGuardianOperationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var exceptionResult = await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        exceptionResult.TransactionResult.Error.ShouldContain("");
    }


    [Fact]
    public async Task<Hash> AddGuardianTest_RepeatedGuardianIdentifierHash_DifferentVerifier()
    {
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var salt2 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(1), _guardian,
            0, salt, operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(2), _guardian1, 0, salt1,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(3), _guardian1, 0, salt2,
                operationType);
        var caHash = await AddGuardianTest();
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(2)},{VerifierAddress1.ToBase58()},{salt1},{operationType}"
                }
            },
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(3)},{VerifierAddress2.ToBase58()},{salt2},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        await CaContractStub.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            holderInfo.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId2);
        }
        return caHash;
    }

    [Fact]
    public async Task<Hash> AddGuardianTest_Success_GuardianCount4_Approve3()
    {
        var caHash = await AddGuardian();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(100), _guardian,
            0, salt, operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(200), _guardian1, 0, salt,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(300), _guardian1, 0, salt,
                operationType);
        var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(400),
            _guardian2, 0, salt, operationType);
        var signature4 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(500), _guardian2, 0, salt,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(100)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(200)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature3,
                    VerificationDoc =
                        $"{0},{_guardian2.ToHex()},{verificationTime.AddSeconds(400)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature4,
                    VerificationDoc =
                        $"{0},{_guardian2.ToHex()},{verificationTime.AddSeconds(500)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        await CaContractStub.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(5);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian2);
            holderInfo.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId1);
        }
        return caHash;
    }

    private async Task<Hash> AddGuardian()
    {
        var caHash = await AddGuardianTest_RepeatedGuardianIdentifierHash_DifferentVerifier();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(11), _guardian,
            0, salt, operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(12), _guardian1, 0, salt,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(13), _guardian1, 0, salt,
                operationType);
        var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(14),
            _guardian2, 0, salt, operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(11)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(12)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(13)},{VerifierAddress2.ToBase58()},{salt},{operationType}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature3,
                    VerificationDoc =
                        $"{0},{_guardian2.ToHex()},{verificationTime.AddSeconds(14)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        await CaContractStub.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(4);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian2);
            holderInfo.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId);
        }
        return caHash;
    }

    // [Fact]
    // public async Task AddGuardianTest_Failed_ApproveCountNotEnough_CountLessThan4()
    // {
    //     var caHash = await AddGuardian();
    //     var verificationTime = DateTime.UtcNow;
    //     var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, GuardianType, 0);
    //     var signature1 =
    //         GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, GuardianType1, 0);
    //     var signature2 =
    //         GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, GuardianType1, 0);
    //     var signature4 =
    //         GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, GuardianType2, 0);
    //     var guardianApprove = new List<Guardian>
    //     {
    //         new Guardian
    //         {
    //             GuardianType = new GuardianType
    //             {
    //                 GuardianType_ = GuardianType,
    //                 Type = 0
    //             },
    //             Verifier = new Verifier
    //             {
    //                 Name = VerifierName,
    //                 Signature = signature,
    //                 VerificationDoc = $"{0},{GuardianType.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
    //             }
    //         },
    //         new Guardian
    //         {
    //             GuardianType = new GuardianType
    //             {
    //                 GuardianType_ = GuardianType1,
    //                 Type = 0
    //             },
    //             Verifier = new Verifier
    //             {
    //                 Name = VerifierName1,
    //                 Signature = signature1,
    //                 VerificationDoc = $"{0},{GuardianType1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
    //             }
    //         }
    //     };
    //     var input = new AddGuardianInput
    //     {
    //         CaHash = caHash,
    //         GuardianToAdd = new Guardian
    //         {
    //             GuardianType = new GuardianType
    //             {
    //                 GuardianType_ = GuardianType2,
    //                 Type = 0
    //             },
    //             Verifier = new Verifier
    //             {
    //                 Name = VerifierName1,
    //                 Signature = signature4,
    //                 VerificationDoc = $"{0},{GuardianType2.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
    //             }
    //         },
    //         GuardiansApproved = {guardianApprove}
    //     };
    //     var executionResult = await CaContractStub.AddGuardian.SendWithExceptionAsync(input);
    //     executionResult.TransactionResult.Error.ShouldContain("Not Satisfied criterion to create a CA Holder");
    // }

    [Fact]
    public async Task AddGuardianTest_Failed_IncorrectData()
    {
        var caHash = await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt1},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var executionResult = await CaContractStub.AddGuardian.SendAsync(input);
        executionResult.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task AddGuardianTest_Failed_IncorrectAddress()
    {
        var caHash = await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var executionResult = await CaContractStub.AddGuardian.SendAsync(input);
        executionResult.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task AddGuardianTest_AlreadyExist()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);

        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
        }
        {
            var guardianApprove = new List<GuardianInfo>
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
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}"
                    }
                }
            };
            var input = new AddGuardianInput
            {
                CaHash = caHash,
                GuardianToAdd = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                        Signature = signature1,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType}"
                    }
                },
                GuardiansApproved = { guardianApprove }
            };
            await CaContractStub.AddGuardian.SendAsync(input);
        }
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
        }
    }

    [Fact]
    public async Task AddGuardianTest_Failed_HolderNotExist()
    {
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    VerificationDoc = $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt}",
                    Signature = signature
                }
            }
        };
        var executionResult = await CaContractStub.AddGuardian.SendWithExceptionAsync(new AddGuardianInput
        {
            CaHash = HashHelper.ComputeFrom("aaa"),
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType}",
                    Signature = signature1
                }
            },
            GuardiansApproved =
            {
                guardianApprove
            }
        });
        executionResult.TransactionResult.Error.ShouldContain(
            $"CA holder is null.CA hash:{HashHelper.ComputeFrom("aaa")}");
    }

    [Fact]
    public async Task AddGuardianTest_Failed_InvalidInput()
    {
        var caHash = await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            }
        };
        {
            var executionResult = await CaContractStub.AddGuardian.SendWithExceptionAsync(new AddGuardianInput
            {
                GuardianToAdd = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                        Signature = signature1,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType}",
                    }
                },
                GuardiansApproved =
                {
                    guardianApprove
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.AddGuardian.SendWithExceptionAsync(new AddGuardianInput
            {
                CaHash = caHash,
                GuardiansApproved =
                {
                    guardianApprove
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.AddGuardian.SendWithExceptionAsync(new AddGuardianInput
            {
                CaHash = caHash,
                GuardianToAdd = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                        Signature = signature1,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt1},{operationType}",
                    }
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }

    [Fact]
    public async Task AddGuardian_failed_for_guardianNotExits()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt,
                operationType);
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var result = await CaContractStub.AddGuardian.SendAsync(input);
        result.TransactionResult.Error.ShouldBe("");
    }

    [Fact]
    public async Task<Hash> RemoveGuardianTest()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian,
            0, salt, operationType);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
        }
        var removeOperationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var guardianApprove1 = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{removeOperationType}",
                }
            }
        };
        await CaContractStubManagerInfo1.RemoveGuardian.SendAsync(new RemoveGuardianInput
        {
            CaHash = caHash,
            GuardianToRemove = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardiansApproved = { guardianApprove1 }
        });
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        }
        return caHash;
    }

    [Fact]
    public async Task RemoveGuardianTest_SameIdentifierHash()
    {
        var caHash = await AddGuardianTest_RepeatedGuardianIdentifierHash_DifferentVerifier();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(30), _guardian,
            0, salt, operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(20), _guardian1, 0, salt,
                operationType);
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(30)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(20)},{VerifierAddress2.ToBase58()},{salt},{operationType}"
                }
            }
        };

        await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                VerifierId = _verifierId1
            }
        });
        await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                VerifierId = _verifierId2
            }
        });

        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = Hash.Empty
        });
        output.GuardianList.Guardians.Where(g => g.IsLoginGuardian).ToList().Count.ShouldBe(3);

        await CaContractStubManagerInfo1.RemoveGuardian.SendAsync(new RemoveGuardianInput
        {
            CaHash = caHash,
            GuardianToRemove = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardiansApproved = { guardianApprove }
        });
    }

    [Fact]
    public async Task RemoveGuardian_failed_guardianNotExits()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType}",
                }
            }
        };

        var result = await CaContractStub.RemoveGuardian.SendAsync(new RemoveGuardianInput
        {
            CaHash = caHash,
            GuardianToRemove = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("");
    }

    [Fact]
    public async Task RemoveGuardianTest_AlreadyRemoved()
    {
        var caHash = await RemoveGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian1,
            0, salt, operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(4), _guardian1, 0, salt,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress1.ToBase58()},{salt},{operationType}",
                }
            }
        };
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        }
        await CaContractStub.RemoveGuardian.SendAsync(new RemoveGuardianInput
        {
            CaHash = caHash,
            GuardianToRemove = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(4)},{VerifierAddress1.ToBase58()},{salt},{operationType}",
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        }
    }

    [Fact]
    public async Task RemoveGuardianTest_Failed_HolderNotExist()
    {
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{operationType}",
                }
            }
        };
        var executionResult = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
        {
            CaHash = HashHelper.ComputeFrom("aaa"),
            GuardianToRemove = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt1},{operationType}",
                }
            },
            GuardiansApproved =
            {
                guardianApprove
            }
        });
        executionResult.TransactionResult.Error.ShouldContain(
            $"CA holder is null.CA hash:{HashHelper.ComputeFrom("aaa")}");
    }

    [Fact]
    public async Task RemoveGuardianTest_Failed_InvalidInput()
    {
        var caHash = await CreateHolder();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian1, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt1,
                operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            }
        };
        {
            var executionResult = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1
                    }
                },
                GuardiansApproved =
                {
                    guardianApprove
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardiansApproved =
                {
                    guardianApprove
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1
                    }
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }

    [Fact]
    public async Task RemoveGuardianTest_Failed_LastLoginGuardian()
    {
        var caHash = await AddGuardianTest_RepeatedGuardianIdentifierHash_DifferentVerifier();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var removeOperationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var addOperationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian,
            0, salt, removeOperationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt,
                addOperationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 0, salt,
                addOperationType);
        List<GuardianInfo> guardianApprove;
        {
            guardianApprove = new List<GuardianInfo>
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
                            $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{removeOperationType}",
                    }
                },
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2,
                        Signature = signature2,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(7)},{VerifierAddress2.ToBase58()},{salt},{removeOperationType}",
                    }
                }
            };
            await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = _guardian1,
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId1,
                    Salt = salt
                }
            });
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(2);
            var executionResult = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain(
                "Cannot remove a Guardian for login, to remove it, unset it first.");


            var signature4 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5),
                _guardian, 0, salt, removeOperationType);
            var signature5 =
                GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(5), _guardian1, 0,
                    salt, removeOperationType);
            var guardianApprove1 = new List<GuardianInfo>
            {
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId,
                        Signature = signature4,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{removeOperationType}",
                    }
                },
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2,
                        Signature = signature5,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress2.ToBase58()},{salt},{removeOperationType}",
                    }
                }
            };
            await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = _guardian1,
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId2,
                    Salt = Salt
                }
            });
            await CaContractStub.RemoveGuardian.SendAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1
                    }
                },
                GuardiansApproved = { guardianApprove1 }
            });
            var executionResult1 = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult1.TransactionResult.Error.ShouldContain(
                "Cannot remove a Guardian for login, to remove it, unset it first.");
            holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = _guardian1,
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId2,
                    Salt = Salt
                }
            });
            guardianApprove = new List<GuardianInfo>
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
                            $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{removeOperationType}",
                    }
                }
            };
            await CaContractStub.RemoveGuardian.SendAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
        }

        {
            guardianApprove = new List<GuardianInfo>
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
                            $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{removeOperationType}",
                    }
                },
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2,
                        Signature = signature2,
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress2.ToBase58()},{salt},{removeOperationType}",
                    }
                }
            };
            var exception = await CaContractStub.RemoveGuardian.SendWithExceptionAsync(new RemoveGuardianInput
            {
                CaHash = caHash,
                GuardianToRemove = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            exception.TransactionResult.Error.ShouldContain(
                "Cannot remove a Guardian for login, to remove it, unset it first.");
        }
    }

    [Fact]
    public async Task UpdateGuardianTest()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian,
            0, salt, operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            }
        };
        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        {
            var guardian = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            guardian.GuardianList.Guardians.Count.ShouldBe(2);
            guardian.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            guardian.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId2);
        }
    }

    [Fact]
    public async Task UpdateGuardianTest_LoginGuardian()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow.AddMinutes(2);
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(15),
            _guardian1, 0, salt, operationType);
        var signature2 = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(30),
            _guardian1, 0, salt, operationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(30)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            }
        };
        await CaContractStub.AddGuardian.SendAsync(new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(15)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1
            }
        });
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId
            }
        });
        var updateOperationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(60), _guardian,
            0, salt, updateOperationType);
        var signature4 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(70),
            _guardian1, 0, salt, updateOperationType);
        var guardianApprove1 = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature3,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(60)},{VerifierAddress.ToBase58()},{salt},{updateOperationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature4,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(70)},{VerifierAddress.ToBase58()},{salt},{updateOperationType}"
                }
            }
        };

        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardiansApproved = { guardianApprove1 }
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[1].VerifierId.ShouldBe(_verifierId2);
        await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId2
            }
        });
        holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[1].VerifierId.ShouldBe(_verifierId2);
    }

    [Fact]
    public async Task UpdateGuardianTest_NotLoginGuardian()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow.AddMinutes(2);
        var salt = Guid.NewGuid().ToString("N");
        var updateOperationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var addOperationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            addOperationType);
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(1), _guardian1,
            0, salt, addOperationType);
        var signature2 = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt,
            addOperationType);
        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{addOperationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{addOperationType}"
                }
            }
        };
        await CaContractStub.AddGuardian.SendAsync(new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt},{addOperationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1
            }
        });

        var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, updateOperationType);
        var signature4 = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5),
            _guardian1, 0, salt, updateOperationType);

        var guardianApprove1 = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature3,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{updateOperationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature4,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{updateOperationType}"
                }
            }
        };

        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardiansApproved = { guardianApprove1 }
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[2].VerifierId.ShouldBe(_verifierId2);
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId2
            }
        });
        holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[2].VerifierId.ShouldBe(_verifierId2);
        GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(3);
    }

    [Fact]
    public async Task UpdateGuardianTest_AlreadyExist()
    {
        var caHash = await AddGuardianTest_RepeatedGuardianIdentifierHash_DifferentVerifier();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var salt2 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt1,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 0, salt2,
                operationType);

        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt1},{operationType}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress2.ToBase58()},{salt2},{operationType}",
                }
            },
        };
        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        {
            var guardian = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            guardian.GuardianList.Guardians.Count.ShouldBe(3);
            guardian.GuardianList.Guardians[1].IdentifierHash.ShouldBe(_guardian1);
            guardian.GuardianList.Guardians[1].VerifierId.ShouldBe(_verifierId1);
            guardian.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            guardian.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId2);
        }
    }

    [Fact]
    public async Task UpdateGuardianTest_Failed_InvalidInput()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var salt2 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt1,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 0, salt2,
                operationType);

        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt1},{operationType}",
                }
            },
        };
        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                GuardianToUpdatePre = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                    }
                },
                GuardianToUpdateNew = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                CaHash = caHash,
                GuardianToUpdateNew = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                CaHash = caHash,
                GuardianToUpdatePre = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                    }
                },
                GuardianToUpdateNew = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                CaHash = caHash,
                GuardianToUpdatePre = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                    }
                },
                GuardianToUpdateNew = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain("Inconsistent guardian.");
        }

        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                CaHash = caHash,
                GuardianToUpdatePre = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
            {
                CaHash = HashHelper.ComputeFrom("111111"),
                GuardianToUpdatePre = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                    }
                },
                GuardianToUpdateNew = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2
                    }
                },
                GuardiansApproved = { guardianApprove }
            });
            executionResult.TransactionResult.Error.ShouldContain(
                $"CA holder is null.CA hash:{HashHelper.ComputeFrom("111111")}");
        }
    }

    [Fact]
    public async Task UpdateGuardian_GuardianTypeDiff_Test()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var salt1 = Guid.NewGuid().ToString("N");
        var salt2 = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt1,
                operationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 0, salt2,
                operationType);

        var guardianApprove = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc = $"{0},{_guardian1},{verificationTime},{VerifierAddress1.ToBase58()}"
                }
            },
        };
        var result = await CaContractStub.UpdateGuardian.SendWithExceptionAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        result.TransactionResult.Error.ShouldContain("Inconsistent guardian.");
    }


    private async Task<Hash> CreateHolder_ChangeOperationTypeInSignatureEnabled()
    {
        var verificationTime = DateTime.UtcNow;

        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = true
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = false
        });

        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
        }
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(10), _guardian,
            0, salt, operationType);
        await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType}"
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
        holderInfo.GuardianList.Guardians.First().IdentifierHash.ShouldBe(_guardian);

        //success
        var manager = new ManagerInfo
        {
            Address = DefaultAddress,
            ExtraData = "iphone14-2022"
        };
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
        await CaContractUser1Stub.AddManagerInfo.SendAsync(new AddManagerInfoInput
        {
            CaHash = holderInfo.CaHash,
            ManagerInfo = manager
        });

        return holderInfo.CaHash;
    }


    private ByteString GenerateSignature_Old(ECKeyPair verifier, Address verifierAddress,
        DateTime verificationTime, Hash guardianType, int type, string salt)
    {
        if (string.IsNullOrWhiteSpace(salt))
        {
            salt = Salt;
        }

        var data = $"{type},{guardianType.ToHex()},{verificationTime},{verifierAddress.ToBase58()},{salt}";
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(verifier.PrivateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    [Fact]
    public async Task<Hash> AddGuardian_Test_Old()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder_ChangeOperationTypeInSignatureEnabled();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }

    [Fact]
    public async Task<Hash> AddGuardian_Test_NoVerifierDoc()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder_ChangeOperationTypeInSignatureEnabled();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }
    
    
    [Fact]
    public async Task<Hash> AddGuardian_Test_Invalidate_VerifierDocLength()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder_ChangeOperationTypeInSignatureEnabled();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }
    
    [Fact]
    public async Task<Hash> AddGuardian_Test_Invalidate_OperationType()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder();
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.Unknown).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt,operationType);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{operationType}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }
    
    
    [Fact]
    public async Task<Hash> AddGuardian_Test_VerifierDoc_IsNull()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder_ChangeOperationTypeInSignatureEnabled();
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()}";
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = signature,
                    VerificationDoc = verificationDoc
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        {
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
            GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        }
        return caHash;
    }
    
}