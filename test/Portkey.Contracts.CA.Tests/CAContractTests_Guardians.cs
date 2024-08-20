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
        //wrong operationType would be asserted signature verified error
        var removeOperationType = Convert.ToInt32(OperationType.RemoveGuardian).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime.AddSeconds(5), _guardian1, 0, salt,
                operationType);
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
        // var result = await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        // result.TransactionResult.Error.ShouldContain("");
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
            GenerateSignature(VerifierKeyPair1, VerifierAddress, verificationTime.AddSeconds(1), _guardian1, 0, salt1,
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
        // var executionResult = await CaContractStub.AddGuardian.SendAsync(input);
        // executionResult.TransactionResult.Error.ShouldContain("");
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
    public async Task AddGuardian_Test_NoVerifierDoc()
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

        // await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        // {
        //     var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        //     {
        //         CaHash = caHash
        //     });
        //     holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
        //     holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        //     GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        // }
        // return caHash;
    }


    [Fact]
    public async Task/*<Hash>*/ AddGuardian_Test_Invalidate_VerifierDocLength()
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
                        $"{0},{_guardian1.ToHex()},{verificationTime.AddSeconds(5)}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };

        // await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        // {
        //     var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        //     {
        //         CaHash = caHash
        //     });
        //     holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
        //     holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        //     GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        // }
        // return caHash;
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
    public async Task AddGuardian_Test_VerifierDoc_IsNull()
    {
        var verificationTime = DateTime.UtcNow;
        var caHash = await CreateHolder_ChangeOperationTypeInSignatureEnabled();
        var salt = Guid.NewGuid().ToString("N");
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

        // await CaContractStubManagerInfo1.AddGuardian.SendAsync(input);
        // {
        //     var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        //     {
        //         CaHash = caHash
        //     });
        //     holderInfo.GuardianList.Guardians.Count.ShouldBe(1);
        //     holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian);
        //     GetLoginGuardianCount(holderInfo.GuardianList).ShouldBe(1);
        // }
    }
}