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
    private const string VerifierName3 = "Gass";
    private Hash _verifierId3;
    private const string VerifierName4 = "Min";
    private Hash _verifierId4;
    private const string ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Portkey.png";
    private const string Salt = "salt";
    public const int MainChainId = 9992731;
    public const int SideChianId = 1866392;

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
        DateTime verificationTime, Hash guardianType, int type, string salt, string operationType,
        int targetChainId = MainChainId, string operationDetails = null)
    {
        if (string.IsNullOrWhiteSpace(salt))
        {
            salt = Salt;
        }

        string data;
        if (string.IsNullOrWhiteSpace(operationDetails))
        {
            data =
                $"{type},{guardianType.ToHex()},{verificationTime},{verifierAddress.ToBase58()},{salt},{operationType},{targetChainId}";
        }
        else
        {
            data =
                $"{type},{guardianType.ToHex()},{verificationTime},{verifierAddress.ToBase58()},{salt},{operationType},{targetChainId},{HashHelper.ComputeFrom(operationDetails).ToHex()}";
        }

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
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName3,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress3 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName4,
                ImageUrl = ImageUrl,
                EndPoints = {"127.0.0.1"},
                VerifierAddressList = {VerifierAddress4}
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
            _verifierId3 = verifierServers.VerifierServers[3].Id;
            _verifierId4 = verifierServers.VerifierServers[4].Id;
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        await Initiate();

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
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}";
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
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
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{removeOperationType},{MainChainId}";
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{removeOperationType},{MainChainId}"
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddMinutes(1), _guardian1,
            0, Salt, addGuardianOperationType);
        var signature2 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddMinutes(2), _guardian1, 0, Salt,
                addGuardianOperationType);
        var verificationDoc =
            $"{0},{_guardian1.ToHex()},{verificationTime.AddMinutes(1)},{VerifierAddress.ToBase58()},{Salt},{addGuardianOperationType},{MainChainId}";
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
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{addGuardianOperationType},{MainChainId}"
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
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(3), _guardian1, 3, salt2,
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(2)},{VerifierAddress1.ToBase58()},{salt1},{operationType},{MainChainId}"
                }
            },
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(3)},{VerifierAddress2.ToBase58()},{salt2},{operationType},{MainChainId}"
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
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(100), _guardian,
                        0, salt, operationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(100)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(200), _guardian1, 0, salt,
                            operationType),
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(200)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(400),
                        _guardian1, 3, salt, operationType),
                    VerificationDoc =
                        $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(400)},{VerifierAddress2.ToBase58()},{salt},{operationType},{MainChainId}"
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
                    Id = _verifierId4,
                    Signature = GenerateSignature(VerifierKeyPair4, VerifierAddress4, verificationTime.AddSeconds(500), _guardian2, 0, salt,
                        operationType),
                    VerificationDoc =
                        $"{0},{_guardian2.ToHex()},{verificationTime.AddSeconds(500)},{VerifierAddress4.ToBase58()},{salt},{operationType},{MainChainId}"
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
            holderInfo.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId4);
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
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(13), _guardian1, 3, salt,
                operationType);
        var signature3 = GenerateSignature(VerifierKeyPair3, VerifierAddress3, verificationTime.AddSeconds(14),
            _guardian2, 2, salt, operationType);
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(11)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(12)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(13)},{VerifierAddress2.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfGoogle,
                IdentifierHash = _guardian2,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId3,
                    Signature = signature3,
                    VerificationDoc =
                        $"{2},{_guardian2.ToHex()},{verificationTime.AddSeconds(14)},{VerifierAddress3.ToBase58()},{salt},{operationType},{MainChainId}"
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
            holderInfo.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId3);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt1},{operationType},{MainChainId}"
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}"
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
                            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}"
                    }
                },
                GuardiansApproved = { guardianApprove }
            };
            var result = await CaContractStub.AddGuardian.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("The verifier already exists");
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
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
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                            $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{removeOperationType},{MainChainId}",
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
        var setLoginGuardianOperationType = Convert.ToInt32(OperationType.SetLoginAccount).ToString();
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(30),
                        _guardian,
                        0, salt, operationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(30)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            new()
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(20),
                        _guardian1, 3, salt,
                        operationType),
                    VerificationDoc =
                        $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(20)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };

        List<GuardianInfo> setLoginGuardianApprove;
        {
            setLoginGuardianApprove = new List<GuardianInfo>
            {
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId,
                        Signature = GenerateSignature(VerifierKeyPair, VerifierAddress,
                            verificationTime.AddSeconds(61),
                            _guardian,
                            0, salt, setLoginGuardianOperationType),
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(61)},{VerifierAddress.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                    }
                },
                new GuardianInfo
                {
                    Type = GuardianType.OfApple,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId2,
                        Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2,
                            verificationTime.AddSeconds(31), _guardian1, 3, salt,
                            setLoginGuardianOperationType),
                        VerificationDoc =
                            $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(31)},{VerifierAddress2.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}"
                    }
                }
            };

            await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
            {
                CaHash = caHash,
                GuardianToSetLogin = new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                        Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1,
                            verificationTime.AddSeconds(71),
                            _guardian1, 0, salt, setLoginGuardianOperationType),
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(71)},{VerifierAddress1.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                    }
                },
                GuardiansApproved = {setLoginGuardianApprove}
            });

            // await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
            // {
            //     CaHash = caHash,
            //     Guardian = new Guardian
            //     {
            //         Type = GuardianType.OfEmail,
            //         IdentifierHash = _guardian1,
            //         VerifierId = _verifierId1
            //     }
            // });
            List<GuardianInfo> setLoginGuardianApprove1;
            {
                setLoginGuardianApprove1 = new List<GuardianInfo>
                {
                    new GuardianInfo
                    {
                        Type = GuardianType.OfEmail,
                        IdentifierHash = _guardian,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId,
                            Signature = GenerateSignature(VerifierKeyPair, VerifierAddress,
                                verificationTime.AddSeconds(62),
                                _guardian,
                                0, salt, setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(62)},{VerifierAddress.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                        }
                    },
                    new GuardianInfo
                    {
                        Type = GuardianType.OfEmail,
                        IdentifierHash = _guardian1,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId1,
                            Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1,
                                verificationTime.AddSeconds(72),
                                _guardian1, 0, salt, setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(72)},{VerifierAddress1.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                        }
                    }
                };
                await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
                {
                    CaHash = caHash,
                    GuardianToSetLogin = new GuardianInfo
                    {
                        Type = GuardianType.OfApple,
                        IdentifierHash = _guardian1,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId2,
                            Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2,
                                verificationTime.AddSeconds(32), _guardian1, 3, salt,
                                setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(32)},{VerifierAddress2.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}"
                        }
                    },
                    GuardiansApproved = {setLoginGuardianApprove1},
                });
                // await CaContractStubManagerInfo1.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
                // {
                //     CaHash = caHash,
                //     Guardian = new Guardian
                //     {
                //         Type = GuardianType.OfApple,
                //         IdentifierHash = _guardian1,
                //         VerifierId = _verifierId2
                //     }
                // });

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
                    GuardiansApproved = {guardianApprove}
                });
            }
        }
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}",
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(4)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}",
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress1.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
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
        var salt2 = Guid.NewGuid().ToString("N");
        var removeOperationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var addOperationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var setLoginGuardianOperationType = Convert.ToInt32(OperationType.SetLoginAccount).ToString();
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
                        Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6),
                            _guardian,
                            0, salt, removeOperationType),
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{removeOperationType},{MainChainId}",
                    }
                },
                new GuardianInfo
                {
                    Type = GuardianType.OfEmail,
                    IdentifierHash = _guardian1,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierId1,
                        Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1,
                            0, salt,
                            addOperationType),
                        VerificationDoc =
                            $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(7)},{VerifierAddress2.ToBase58()},{salt},{removeOperationType},{MainChainId}",
                    }
                }
            };
            List<GuardianInfo> setLoginGuardianApprove;
            {
                setLoginGuardianApprove = new List<GuardianInfo>
                {
                    new GuardianInfo
                    {
                        Type = GuardianType.OfEmail,
                        IdentifierHash = _guardian,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId,
                            Signature = GenerateSignature(VerifierKeyPair, VerifierAddress,
                                verificationTime.AddSeconds(61),
                                _guardian,
                                0, salt, setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(61)},{VerifierAddress.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                        }
                    },
                    new GuardianInfo
                    {
                        Type = GuardianType.OfApple,
                        IdentifierHash = _guardian1,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId2,
                            Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2,
                                verificationTime.AddSeconds(31), _guardian1, 3, salt2,
                                setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{3},{_guardian1.ToHex()},{verificationTime.AddSeconds(31)},{VerifierAddress2.ToBase58()},{salt2},{setLoginGuardianOperationType},{MainChainId}"
                        }
                    }
                };

                await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
                {
                    CaHash = caHash,
                    GuardianToSetLogin = new GuardianInfo
                    {
                        Type = GuardianType.OfEmail,
                        IdentifierHash = _guardian1,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId1,
                            Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(71),
                                _guardian1,    0, salt, setLoginGuardianOperationType),
                            VerificationDoc =
                                $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(71)},{VerifierAddress1.ToBase58()},{salt},{setLoginGuardianOperationType},{MainChainId}",
                        }
                    },
                    GuardiansApproved = {setLoginGuardianApprove},
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
                    GuardiansApproved = {guardianApprove}
                });
                executionResult.TransactionResult.Error.ShouldContain(
                    "Cannot remove a Guardian for login, to remove it, unset it first.");
                await CaContractStub.RemoveGuardian.SendAsync(new RemoveGuardianInput
                {
                    CaHash = caHash,
                    GuardianToRemove = new GuardianInfo
                    {
                        Type = GuardianType.OfApple,
                        IdentifierHash = _guardian1,
                        VerificationInfo = new VerificationInfo
                        {
                            Id = _verifierId2
                        }
                    },
                    GuardiansApproved = {guardianApprove}
                });
            }
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
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
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
                        operationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(30),
                        _guardian1, 0, salt, operationType),
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(30)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };
        await CaContractStub.AddGuardian.SendAsync(new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfFacebook,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(15),
                        _guardian1, 5, salt, operationType),
                    VerificationDoc =
                        $"{5},{_guardian1.ToHex()},{verificationTime.AddSeconds(15)},{VerifierAddress2.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        var setLoginOperationType = Convert.ToInt32(OperationType.SetLoginAccount).ToString();
        var guardianApproveSetLogin = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
                        operationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(30),
                        _guardian1, 0, salt, operationType),
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(30)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                }
            }
        };
        // await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        // {
        //     CaHash = caHash,
        //     Guardian = new Guardian
        //     {
        //         IdentifierHash = _guardian1,
        //         Type = GuardianType.OfEmail,
        //         VerifierId = _verifierId1
        //     }
        // });
        // await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        // {
        //     CaHash = caHash,
        //     Guardian = new Guardian
        //     {
        //         IdentifierHash = _guardian1,
        //         Type = GuardianType.OfEmail,
        //         VerifierId = _verifierId
        //     }
        // });
        var updateOperationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature3 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(60), _guardian,
            0, salt, updateOperationType);
        var signature4 = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(70),
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(60)},{VerifierAddress.ToBase58()},{salt},{updateOperationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(70)},{VerifierAddress1.ToBase58()},{salt},{updateOperationType},{MainChainId}"
                }
            }
        };

        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfFacebook,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfFacebook,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId3
                }
            },
            GuardiansApproved = { guardianApprove1 }
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[2].VerifierId.ShouldBe(_verifierId3);
        // await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
        // {
        //     CaHash = caHash,
        //     Guardian = new Guardian
        //     {
        //         IdentifierHash = _guardian1,
        //         Type = GuardianType.OfEmail,
        //         VerifierId = _verifierId1
        //     }
        // });
        holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[1].VerifierId.ShouldBe(_verifierId1);
    }

    [Fact]
    public async Task UpdateGuardianTest_NotLoginGuardian()
    {
        var caHash = await AddGuardianTest();
        var verificationTime = DateTime.UtcNow.AddMinutes(2);
        var salt = Guid.NewGuid().ToString("N");
        var updateOperationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var addOperationType = Convert.ToInt32(OperationType.AddGuardian).ToString();
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
                        addOperationType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{addOperationType},{MainChainId}"
                }
            },
            new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0, salt,
                        addOperationType),
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt},{addOperationType},{MainChainId}"
                }
            }
        };
        await CaContractStub.AddGuardian.SendAsync(new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                IdentifierHash = _guardian1,
                Type = GuardianType.OfFacebook,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime.AddSeconds(1), _guardian1,
                        5, salt, addOperationType),
                    VerificationDoc =
                        $"{5},{_guardian1.ToHex()},{verificationTime.AddSeconds(1)},{VerifierAddress2.ToBase58()},{salt},{addOperationType},{MainChainId}"
                }
            },
            GuardiansApproved = { guardianApprove }
        });
        // await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        // {
        //     CaHash = caHash,
        //     Guardian = new Guardian
        //     {
        //         IdentifierHash = _guardian1,
        //         Type = GuardianType.OfEmail,
        //         VerifierId = _verifierId1
        //     }
        // });

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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{updateOperationType},{MainChainId}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{updateOperationType},{MainChainId}"
                }
            }
        };

        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfFacebook,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfFacebook,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId3
                }
            },
            GuardiansApproved = { guardianApprove1 }
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[2].VerifierId.ShouldBe(_verifierId3);
        // await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        // {
        //     CaHash = caHash,
        //     Guardian = new Guardian
        //     {
        //         IdentifierHash = _guardian1,
        //         Type = GuardianType.OfPhone,
        //         VerifierId = _verifierId3
        //     }
        // });
        holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        holderInfo.GuardianList.Guardians.Count.ShouldBe(3);
        holderInfo.GuardianList.Guardians[2].VerifierId.ShouldBe(_verifierId3);
        GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
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
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 3, salt2,
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
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
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt1},{operationType},{MainChainId}",
                }
            },
            // new GuardianInfo
            // {
            //     Type = GuardianType.OfApple,
            //     IdentifierHash = _guardian1,
            //     VerificationInfo = new VerificationInfo
            //     {
            //         Id = _verifierId2,
            //         Signature = signature2,
            //         VerificationDoc =
            //             $"{3},{_guardian1.ToHex()},{verificationTime},{VerifierAddress2.ToBase58()},{salt2},{operationType},{MainChainId}",
            //     }
            // },
        };
        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfApple,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId4
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
            guardian.GuardianList.Guardians.Last().VerifierId.ShouldBe(_verifierId4);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
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
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{salt1},{operationType},{MainChainId}",
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}"
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
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt, operationType);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}";
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
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
    
    [Fact]
    public async Task AddGuardianWithZkTestLoginAndAddZkGuardian()
    {
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()}";
        var caHash = await CreateHolderWithZkLogin();
        const string circuitId = "0999c81d5873bc7c3c5bc7e5d5e63be4d4ca91b77b45f9954b79e1d33499f25e";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e191968da0238ca69e0c34d60dac3a405087da1e8dc616efb15d5f11a78d8c6bc042d1f5500d204beb77cb818781594fd8e03f60c94a70bd5271e67087c7f5d60998200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("e6d9987084a434139ecae0f8bf5780f00db02a6fb14776f9f0eac81e9d756847"),
                    Salt = "d18cd64281104e98b0c632c060a7def9",
                    Nonce = "737a2d5e4d0b95997f825da34d9565ab4939ebac6dab74b5e3642937cc263ffc",
                    ZkProof = "{\"pi_a\":[\"4035871483944802535425644544179696235715394422866642493076017930158083623005\",\"7620965939039042558699214279795172448638305768268750153307202207911725830031\",\"1\"],\"pi_b\":[[\"5552212103697648587743177516784229969971296445612062008559229743862894194070\",\"12429972106196336895041680878191783565354444551638795929415647872116587747398\"],[\"14890271894639276116254429243704367322165212968393784387522359760364273087320\",\"18764178293290589388227585706335098178944575700460512426760426303720506815885\"],[\"1\",\"0\"]],\"pi_c\":[\"11087623231982956306795665583572523938241137132272271106806885581070610927547\",\"1593338240063758084947228428061868253531881335202141681059270270757504025474\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "4035871483944802535425644544179696235715394422866642493076017930158083623005",
                            "7620965939039042558699214279795172448638305768268750153307202207911725830031",
                            "1" },
                        ZkProofPiB1 = { "5552212103697648587743177516784229969971296445612062008559229743862894194070",
                            "12429972106196336895041680878191783565354444551638795929415647872116587747398" },
                        ZkProofPiB2 = { "14890271894639276116254429243704367322165212968393784387522359760364273087320",
                            "18764178293290589388227585706335098178944575700460512426760426303720506815885" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "11087623231982956306795665583572523938241137132272271106806885581070610927547",
                            "1593338240063758084947228428061868253531881335202141681059270270757504025474",
                            "1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        
                    }
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfGoogle,
                IdentifierHash =  Hash.LoadFromHex("a831cbb61da83b8e155a30a3e57dd8216a022278475951f7ea860c8f4fe63be5"),
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("0745df56b7a450d3a5d66447515ec2306b5a207277a5a82e9eb50488d19f5a37"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                        // $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress1.ToBase58()},{salt},{operationType},{MainChainId}"
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("91db521d82251b5f1de0e0e4c88e0ef3f2f434250a46bb6b907dcab5f4323d86"),
                    Salt = "7815b24d3b2147f0a388ca2bb3b50c16",
                    Nonce = "737a2d5e4d0b95997f825da34d9565ab4939ebac6dab74b5e3642937cc263ffc",
                    ZkProof = "{\"pi_a\":[\"8576139714645804322158709817482876352669814669434650728598149288388887345555\",\"12859389537870236783076005815068919291895563674564564642580917255735562225319\",\"1\"],\"pi_b\":[[\"12890697038987679677853857334337145838524230954756178194484503629669445168642\",\"4566336719762529934041772892193211171903926419148660193518443309337219267503\"],[\"12287523699954793663202844805997344851657304962443474257278768950585329029909\",\"3477689140754236326583557089376503245613084179352418520905019750409170366946\"],[\"1\",\"0\"]],\"pi_c\":[\"14247397392623988779863444402758620375975423104173693429620526512430934808185\",\"13098409122546471127943751168348374048869658131178715937393653776790059097238\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "8576139714645804322158709817482876352669814669434650728598149288388887345555",
                            "12859389537870236783076005815068919291895563674564564642580917255735562225319",
                            "1" },
                        ZkProofPiB1 = { "12890697038987679677853857334337145838524230954756178194484503629669445168642",
                            "4566336719762529934041772892193211171903926419148660193518443309337219267503" },
                        ZkProofPiB2 = { "12287523699954793663202844805997344851657304962443474257278768950585329029909",
                            "3477689140754236326583557089376503245613084179352418520905019750409170366946" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "14247397392623988779863444402758620375975423104173693429620526512430934808185",
                            "13098409122546471127943751168348374048869658131178715937393653776790059097238",
                            "1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        // AddManagerAddress = new AddManager
                        // {
                        //     CaHash = Hash.Empty,
                        //     ManagerAddress = Address.FromBase58("YanBhpryvqf9RcFVapi1vhu5fvCzoVeT8x5AbRBvTNZKsQJRf"),
                        //     Timestamp = new Timestamp
                        //     {
                        //         Seconds = 1721203238,
                        //         Nanos = 912000000
                        //     }
                        // }
                    }
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        var result = await CaContractStub.AddGuardian.SendAsync(input);
        var guardianAdded = GuardianAdded.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(GuardianAdded)).NonIndexed);
        guardianAdded.CaHash.ShouldBe(caHash);
        guardianAdded.CaAddress.ShouldBe(holderInfo.CaAddress);
        // guardianAdded.GuardianAdded_.IdentifierHash.ShouldBe(Hash.LoadFromHex("048990922ac3e5c16d36ec48f0a2311f07e4c1ced723c5c9f84f64eb6264f44f"));
        // holderInfo.GuardianList.Guardians.Count.ShouldBe(2);
        // holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
        // GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
    }
    
    [Fact]
    public async Task<Hash> AddGuardianWithZkTestLoginWithZkAndAddEmailGuardian()
    {
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var signature = GenerateSignature_Old(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt);
        var signature1 =
            GenerateSignature_Old(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0,
                salt);
        var verificationDoc =
            $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()}";
        var caHash = await CreateHolderWithZkLogin();
        const string circuitId = "0999c81d5873bc7c3c5bc7e5d5e63be4d4ca91b77b45f9954b79e1d33499f25e";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e191968da0238ca69e0c34d60dac3a405087da1e8dc616efb15d5f11a78d8c6bc042d1f5500d204beb77cb818781594fd8e03f60c94a70bd5271e67087c7f5d60998200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("e6d9987084a434139ecae0f8bf5780f00db02a6fb14776f9f0eac81e9d756847"),
                    Salt = "d18cd64281104e98b0c632c060a7def9",
                    Nonce = "737a2d5e4d0b95997f825da34d9565ab4939ebac6dab74b5e3642937cc263ffc",
                    ZkProof = "{\"pi_a\":[\"4035871483944802535425644544179696235715394422866642493076017930158083623005\",\"7620965939039042558699214279795172448638305768268750153307202207911725830031\",\"1\"],\"pi_b\":[[\"5552212103697648587743177516784229969971296445612062008559229743862894194070\",\"12429972106196336895041680878191783565354444551638795929415647872116587747398\"],[\"14890271894639276116254429243704367322165212968393784387522359760364273087320\",\"18764178293290589388227585706335098178944575700460512426760426303720506815885\"],[\"1\",\"0\"]],\"pi_c\":[\"11087623231982956306795665583572523938241137132272271106806885581070610927547\",\"1593338240063758084947228428061868253531881335202141681059270270757504025474\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "4035871483944802535425644544179696235715394422866642493076017930158083623005",
                            "7620965939039042558699214279795172448638305768268750153307202207911725830031",
                            "1" },
                        ZkProofPiB1 = { "5552212103697648587743177516784229969971296445612062008559229743862894194070",
                            "12429972106196336895041680878191783565354444551638795929415647872116587747398" },
                        ZkProofPiB2 = { "14890271894639276116254429243704367322165212968393784387522359760364273087320",
                            "18764178293290589388227585706335098178944575700460512426760426303720506815885" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "11087623231982956306795665583572523938241137132272271106806885581070610927547",
                            "1593338240063758084947228428061868253531881335202141681059270270757504025474",
                            "1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        
                    }
                }
            }
        };
        var input = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = Hash.LoadFromHex("88b1fa5946b1eba7b99c63a2863934e9e62ab02a7b92501f3235d5ad17dc58c2"),
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("61bc9c43eb8d311c21b3fe082884875e4fd377f11aa8ac278ba4ffab4e1ba3c9"),
                    // Signature = signature,
                    // VerificationDoc =
                    //     $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(6)},{VerifierAddress.ToBase58()},{salt},{operationType},{MainChainId}",
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        var result = await CaContractStub.AddGuardian.SendAsync(input);
        var guardianAdded = GuardianAdded.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(GuardianAdded)).NonIndexed);
        guardianAdded.CaHash.ShouldBe(caHash);
        guardianAdded.CaAddress.ShouldBe(holderInfo.CaAddress);
        return caHash;
    }
    
    [Fact]
    public async Task UpdateGuardianZkTest()
    {
        var caHash = await AddGuardianWithZkTestLoginWithZkAndAddEmailGuardian();
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.UpdateGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(6), _guardian,
            0, salt, operationType);
        var guardianApprove = new List<GuardianInfo>
        {
            new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("e6d9987084a434139ecae0f8bf5780f00db02a6fb14776f9f0eac81e9d756847"),
                    Salt = "d18cd64281104e98b0c632c060a7def9",
                    Nonce = "737a2d5e4d0b95997f825da34d9565ab4939ebac6dab74b5e3642937cc263ffc",
                    ZkProof = "{\"pi_a\":[\"4035871483944802535425644544179696235715394422866642493076017930158083623005\",\"7620965939039042558699214279795172448638305768268750153307202207911725830031\",\"1\"],\"pi_b\":[[\"5552212103697648587743177516784229969971296445612062008559229743862894194070\",\"12429972106196336895041680878191783565354444551638795929415647872116587747398\"],[\"14890271894639276116254429243704367322165212968393784387522359760364273087320\",\"18764178293290589388227585706335098178944575700460512426760426303720506815885\"],[\"1\",\"0\"]],\"pi_c\":[\"11087623231982956306795665583572523938241137132272271106806885581070610927547\",\"1593338240063758084947228428061868253531881335202141681059270270757504025474\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "4035871483944802535425644544179696235715394422866642493076017930158083623005",
                            "7620965939039042558699214279795172448638305768268750153307202207911725830031",
                            "1" },
                        ZkProofPiB1 = { "5552212103697648587743177516784229969971296445612062008559229743862894194070",
                            "12429972106196336895041680878191783565354444551638795929415647872116587747398" },
                        ZkProofPiB2 = { "14890271894639276116254429243704367322165212968393784387522359760364273087320",
                            "18764178293290589388227585706335098178944575700460512426760426303720506815885" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "11087623231982956306795665583572523938241137132272271106806885581070610927547",
                            "1593338240063758084947228428061868253531881335202141681059270270757504025474",
                            "1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = "0999c81d5873bc7c3c5bc7e5d5e63be4d4ca91b77b45f9954b79e1d33499f25e",
                    NoncePayload = new NoncePayload
                    {
                        
                    }
                }
            }
        };
        await CaContractStub.UpdateGuardian.SendAsync(new UpdateGuardianInput
        {
            CaHash = caHash,
            GuardianToUpdatePre = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = Hash.LoadFromHex("88b1fa5946b1eba7b99c63a2863934e9e62ab02a7b92501f3235d5ad17dc58c2"),
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("61bc9c43eb8d311c21b3fe082884875e4fd377f11aa8ac278ba4ffab4e1ba3c9")
                }
            },
            GuardianToUpdateNew = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = Hash.LoadFromHex("88b1fa5946b1eba7b99c63a2863934e9e62ab02a7b92501f3235d5ad17dc58c2"),
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8")
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
            guardian.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(Hash.LoadFromHex("88b1fa5946b1eba7b99c63a2863934e9e62ab02a7b92501f3235d5ad17dc58c2"));
            guardian.GuardianList.Guardians.Last().VerifierId.ShouldBe(Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8"));
        }
    }
    
    [Fact]
    public async Task AppendGuardianTest()
    {
        var caHash = await AddGuardianTest();
        await CaContractStub.AppendGuardianPoseidonHash.SendAsync(new AppendGuardianRequest
        {
            Input = { new AppendGuardianInput
            {
                CaHash = caHash,
                Guardians =
                {
                    new PoseidonGuardian()
                    {
                        Type = GuardianType.OfEmail,
                        IdentifierHash = _guardian1,
                        PoseidonIdentifierHash = "3950244133829448880235746316633373605489408990829000264333731122287772474181"
                    }
                }
            } }
        });
        {
            var guardian = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                CaHash = caHash
            });
            guardian.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
            guardian.GuardianList.Guardians.Last().PoseidonIdentifierHash.ShouldBe("3950244133829448880235746316633373605489408990829000264333731122287772474181");
        }
    }
}