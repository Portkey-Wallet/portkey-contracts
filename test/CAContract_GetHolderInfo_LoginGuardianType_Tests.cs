using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task GetHolderInfo_ByCaHash_Test()
    {
        var caHash = await CreateHolder();

        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        getHolderInfoOutput.CaHash.ShouldBe(caHash);
        getHolderInfoOutput.ManagerInfos[0].Address.ShouldBe(User1Address);
        getHolderInfoOutput.ManagerInfos[0].ExtraData.ShouldBe("123");

        var guardianList = getHolderInfoOutput.GuardianList;
        var guardians = guardianList.Guardians;
        guardians.Count.ShouldBe(1);
        guardians[0].Type.ShouldBe(GuardianType.OfEmail);
        guardians[0].IdentifierHash.ShouldBe(_guardian);

        GetLoginGuardianCount(guardianList).ShouldBe(1);
    }

    [Fact]
    public async Task GetHolderInfo_ByLoginGuardian_Test()
    {
        var caHash = await CreateHolder();

        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = null,
            LoginGuardianIdentifierHash = _guardian
        });

        getHolderInfoOutput.CaHash.ShouldBe(caHash);
        getHolderInfoOutput.ManagerInfos[0].Address.ShouldBe(User1Address);
        getHolderInfoOutput.ManagerInfos[0].ExtraData.ShouldBe("123");
        var guardianList = getHolderInfoOutput.GuardianList;
        var guardians = guardianList.Guardians;

        guardians.Count.ShouldBe(1);
        guardians[0].Type.ShouldBe(GuardianType.OfEmail);
        guardians[0].IdentifierHash.ShouldBe(_guardian);

        GetLoginGuardianCount(guardianList).ShouldBe(1);
    }

    [Fact]
    public async Task GetHolderInfo_ByNULL_Test()
    {
        await CreateHolder();

        var executionResult = await CaContractStub.GetHolderInfo.CallWithExceptionAsync(new GetHolderInfoInput
        {
            CaHash = null,
            LoginGuardianIdentifierHash = null
        });

        executionResult.Value.ShouldContain("CaHash is null, or loginGuardianIdentifierHash is empty: , ");
    }

    [Fact]
    public async Task GetHolderInfo_ByInvalidCaHash_Test()
    {
        await CreateHolder();

        var executionResult = await CaContractStub.GetHolderInfo.CallWithExceptionAsync(new GetHolderInfoInput
        {
            CaHash = new Hash()
        });

        executionResult.Value.ShouldContain("Holder is not found");
    }

    [Fact]
    public async Task GetHolderInfo_ByInvalidLoginGuardianIdentifierHash_Test()
    {
        await CreateHolder();

        var hash = HashHelper.ComputeFrom("Invalid");
        var executionResult = await CaContractStub.GetHolderInfo.CallWithExceptionAsync(new GetHolderInfoInput
        {
            CaHash = null,
            LoginGuardianIdentifierHash = hash
        });

        executionResult.Value.ShouldContain($"Not found ca_hash by a the loginGuardianIdentifierHash {hash}");
    }

    [Fact]
    public async Task SetLoginGuardian_Succeed_Test()
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

        getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);

        // check loginGuardianIdentifierHash -> caHash mapping
        getHolderInfoOutput = await GetHolderInfo_Helper(null, _guardian);
        guardianList = getHolderInfoOutput.GuardianList;
        guardianList.ShouldNotBeNull();

        getHolderInfoOutput = await GetHolderInfo_Helper(null, _guardian1);
        guardianList = getHolderInfoOutput.GuardianList;
        guardianList.ShouldNotBeNull();
    }

    [Fact]
    public async Task SetLoginGuardian_Again_Succeed_Test()
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

        getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);

        getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);
    }

    [Fact]
    public async Task SetLoginGuardian_CaHashNull_Test()
    {
        await CreateCAHolder_AndGetCaHash_Helper();


        var executionResult = await CaContractStub.SetGuardianForLogin.SendWithExceptionAsync(
            new SetGuardianForLoginInput
            {
                CaHash = null,
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierIdEmpty,
                    IdentifierHash = _guardian1
                }
            });

        executionResult.TransactionResult.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task SetLoginGuardian_CaHashNotExits_Test()
    {
        await CreateCAHolder_AndGetCaHash_Helper();
        var executionResult = await CaContractStub.SetGuardianForLogin.SendWithExceptionAsync(
            new SetGuardianForLoginInput
            {
                CaHash = HashHelper.ComputeFrom("123"),
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierIdEmpty,
                    IdentifierHash = _guardian1
                }
            });

        executionResult.TransactionResult.Error.ShouldContain("CA Holder is null");
    }

    [Fact]
    public async Task SetLoginGuardian_GuardianNull_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();
        var executionResult = await CaContractStub.SetGuardianForLogin.SendWithExceptionAsync(
            new SetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = null
            });

        executionResult.TransactionResult.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task SetLoginGuardian_GuardianIdentifierHashEmpty_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();


        var executionResult = await CaContractStub.SetGuardianForLogin.SendWithExceptionAsync(
            new SetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierIdEmpty,
                    IdentifierHash = null
                }
            });

        executionResult.TransactionResult.Error.ShouldContain("Guardian IdentifierHash should not be null");
    }

    [Fact]
    public async Task SetLoginGuardian_GuardianNotExists_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();

        var guardian = new Guardian
        {
            Type = GuardianType.OfEmail,
            VerifierId = _verifierId,
            IdentifierHash = _guardianNotExist,
            Salt = Salt
        };

        var getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, guardian);

        var guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(1);
        guardianList.Guardians.Count.ShouldBe(2);
        guardianList.Guardians[0].IdentifierHash.ShouldBe(_guardian);
    }


    /*[Fact]
public async Task SetLoginGuardian_RegisterByOthers()
{
    var caHash = await CreateAHolder_AndGetCaHash_Helper();
   
    var getHolderInfoOutput = await CaContractStub.GetHolderInfo.SendAsync(new GetHolderInfoInput
    {
        CaHash = caHash,
        LoginGuardian = null
    });
   
    var guardianList = getHolderInfoOutput.Output.GuardianList;
    var guardians = guardianList.Guardians;
    guardians.Count.ShouldBe(2);
   
    GetLoginGuardianCount(guardianList).ShouldBe(1);
    var guardianType = new GuardianType
    {
        Type = GuardianTypeType.GuardianTypeOfEmail,
        GuardianType_ = GuardianType1
    };
    getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, guardianType);
    var verificationTime = DateTime.UtcNow;
    var signature1 = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, GuardianType, 0);
    await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
    {
        GuardianApproved = new Guardian
        {
            GuardianType = guardianType,
            Verifier = new Verifier
            {
                Name = VerifierName,
                Signature = signature1,
                VerificationDoc = $"{0},{GuardianType.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
            }
        },
        Manager = new Manager
        {
            Address = User1Address,
            ExtraData = "123"
        }
    });
    var caHolderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
    {
        LoginGuardian = GuardianType
    });
    await SetGuardianForLogin_AndGetHolderInfo_Helper(caHolderInfo.CaHash, guardianType);
    caHolderInfo.ManagerInfos.Count.ShouldBe(2);
    caHolderInfo.GuardianList.Guardians.Count.ShouldBe(2);

}*/


    [Fact]
    public async Task UnsetLoginGuardian_Succeed_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0);
        var signature2 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian1, 0);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            },
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
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
                    Id = _verifierId2,
                    Signature = signature2,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress2.ToBase58()},{Salt}"
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
                VerifierId = _verifierId2
            }
        });

        var getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        var guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(3);

        await UnsetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId,
                IdentifierHash = _guardian
            }
        });

        getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian1
        });
        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(1);

        // check loginGuardianIdentifierHash mapping is removed.
        var executionResult = await CaContractStub.GetHolderInfo.CallWithExceptionAsync(new GetHolderInfoInput
        {
            CaHash = null,
            LoginGuardianIdentifierHash = _guardian
        });

        executionResult.Value.ShouldContain("Not found ca_hash by a the loginGuardianIdentifierHash");
    }


    [Fact]
    public async Task UnsetLoginGuardian_GuardianIdentifierHashNotIn_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();

        var getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        var guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);

        await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                Type = GuardianType.OfEmail,
                VerifierId = _verifierIdEmpty,
                IdentifierHash = _guardianNotExist
            }
        });

        guardianList = getHolderInfoOutput.GuardianList;
        GetLoginGuardianCount(guardianList).ShouldBe(2);
    }


    [Fact]
    public async Task UnsetLoginGuardian_Again_Succeed_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Multi_Helper();

        var getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1,
                IdentifierHash = _guardian
            }
        });

        getHolderInfoOutput = await GetHolderInfo_Helper(caHash, null);
        var guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(3);

        getHolderInfoOutput = await UnsetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);

        await CaContractStub.UnsetGuardianForLogin.SendAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId1,
                    IdentifierHash = _guardian1
                }
            });
        getHolderInfoOutput = await GetHolderInfo_Helper(caHash, null);
        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);
    }

    [Fact]
    public async Task UnsetLoginGuardian_CaHashNull_Test()
    {
        await CreateCAHolder_AndGetCaHash_Helper();
        var executionResult = await CaContractStub.UnsetGuardianForLogin.SendWithExceptionAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = null,
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierIdEmpty,
                    IdentifierHash = _guardian1
                }
            });

        executionResult.TransactionResult.Error.ShouldNotBeNull();

        var result = await CaContractStub.UnsetGuardianForLogin.SendWithExceptionAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = HashHelper.ComputeFrom("123"),
                Guardian = new Guardian
                {
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierIdEmpty,
                    IdentifierHash = _guardian1
                }
            });

        result.TransactionResult.Error.ShouldContain("CA holder is null");
    }

    [Fact]
    public async Task UnsetLoginGuardian_GuardianNull_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();


        var executionResult = await CaContractStub.UnsetGuardianForLogin.SendWithExceptionAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = null
            });

        executionResult.TransactionResult.Error.ShouldNotBeNull();
    }


    [Fact]
    public async Task UnsetLoginGuardian_FailedUniqueLoginGuardianIdentifierHash_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();

        var getHolderInfoOutput = await SetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        var guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(2);

        getHolderInfoOutput = await UnsetGuardianForLogin_AndGetHolderInfo_Helper(caHash, null);

        guardianList = getHolderInfoOutput.GuardianList;

        GetLoginGuardianCount(guardianList).ShouldBe(1);

        var result = await CaContractStub.UnsetGuardianForLogin.SendWithExceptionAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId
                }
            });
        result.TransactionResult.Error.ShouldContain("only one LoginGuardian,can not be Unset");
    }

    [Fact]
    public async Task UnsetLoginGuardian_GuardianIdentifierHashEmpty_Test()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();
        var executionResult = await CaContractStub.UnsetGuardianForLogin.SendWithExceptionAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = null,
                    Type = GuardianType.OfEmail
                }
            });

        executionResult.TransactionResult.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnsetLoginGuardian_GuardianNotExitsTest()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Helper();
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
        var result = await CaContractStub.UnsetGuardianForLogin.SendAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = HashHelper.ComputeFrom("1111@gmail.com"),
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId2
                }
            });
        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        getHolderInfoOutput.GuardianList.Guardians.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UnsetLoginGuardian_GuardianMultiTest()
    {
        var caHash = await CreateCAHolder_AndGetCaHash_Multi_Helper();
        var output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = null
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
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = null
        });
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1
            }
        });
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = null
        });
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = new Guardian
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId2
            }
        });
        output = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = null
        });
        var result = await CaContractStub.UnsetGuardianForLogin.SendAsync(
            new UnsetGuardianForLoginInput
            {
                CaHash = caHash,
                Guardian = new Guardian
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerifierId = _verifierId1
                }
            });
        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });
        GetLoginGuardianCount(getHolderInfoOutput.GuardianList).ShouldBe(3);
    }

    private async Task<GetHolderInfoOutput> SetGuardianForLogin_AndGetHolderInfo_Helper(
        Hash caHash, Guardian guardian)
    {
        await CaContractStub.SetGuardianForLogin.SendAsync(new SetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = guardian ?? new Guardian
            {
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1,
                IdentifierHash = _guardian1,
                Salt = Salt
            }
        });

        return await GetHolderInfo_Helper(caHash, null);
    }

    private async Task<GetHolderInfoOutput> GetHolderInfo_Helper(Hash caHash,
        Hash loginGuardianIdentifierHash)
    {
        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash,
            LoginGuardianIdentifierHash = loginGuardianIdentifierHash
        });

        return getHolderInfoOutput;
    }

    private async Task<GetHolderInfoOutput> UnsetGuardianForLogin_AndGetHolderInfo_Helper(
        Hash caHash, Guardian guardian)
    {
        await CaContractStub.UnsetGuardianForLogin.SendAsync(new UnsetGuardianForLoginInput
        {
            CaHash = caHash,
            Guardian = guardian ?? new Guardian
            {
                Type = GuardianType.OfEmail,
                VerifierId = _verifierId1,
                IdentifierHash = _guardian1,
            }
        });

        var getHolderInfoOutput = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            CaHash = caHash
        });

        return getHolderInfoOutput;
    }


    private async Task<Hash> CreateCAHolder_AndGetCaHash_Helper()
    {
        var caHash = await CreateHolder();

        await AddAGuardian_Helper(caHash);

        return caHash;
    }

    private async Task<Hash> CreateCAHolder_AndGetCaHash_Multi_Helper()
    {
        var caHash = await CreateHolder();

        await AddAGuardian_Helper_Multi(caHash);

        return caHash;
    }

    private async Task AddAGuardian_Helper(Hash caHash)
    {
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
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
            holderInfo.GuardianList.Guardians.Last().IdentifierHash.ShouldBe(_guardian1);
        }
    }

    private async Task AddAGuardian_Helper_Multi(Hash caHash)
    {
        var verificationTime = DateTime.UtcNow;
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0);
        var signature1 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian1, 0);
        var signature01 =
            GenerateSignature(VerifierKeyPair1, VerifierAddress1, verificationTime, _guardian, 0);
        var signature02 =
            GenerateSignature(VerifierKeyPair2, VerifierAddress2, verificationTime, _guardian, 0);
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
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
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            },
            GuardiansApproved = { guardianApprove }
        };
        await CaContractStub.AddGuardian.SendAsync(input);
        var guardianApprove1 = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            },
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            }
        };
        var input1 = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature01,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            },
            GuardiansApproved = { guardianApprove1 }
        };
        await CaContractStub.AddGuardian.SendAsync(input1);
        var guardianApprove2 = new List<GuardianInfo>
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{Salt}"
                }
            },
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian1,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature1,
                    VerificationDoc =
                        $"{0},{_guardian1.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            },
            new()
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId1,
                    Signature = signature01,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress1.ToBase58()},{Salt}"
                }
            }
        };
        var input2 = new AddGuardianInput
        {
            CaHash = caHash,
            GuardianToAdd = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = _verifierId2,
                    Signature = signature02,
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress2.ToBase58()},{Salt}"
                }
            },
            GuardiansApproved = { guardianApprove2 }
        };
        await CaContractStub.AddGuardian.SendAsync(input2);
    }

    private int GetLoginGuardianCount(GuardianList list)
    {
        return list.Guardians.Where(g => g.IsLoginGuardian).ToList().Count;
    }
}