using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private bool _isInitialized;
    private RepeatedField<VerifierServer> _verifierServers;
    private Hash _transferLimitTestCaHash;
    private long _elfDefaultSingleLimit = 1000000;
    private long _elfDefaultDailyLimit = 5000000;
    private long _defaultTokenTransferLimit = 10000000;


    [Fact]
    public async Task SetTransferLimitTest()
    {
        await InitTransferLimitTest();

        var setLimitVerifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var setLimitOpType = Convert.ToInt32(OperationType.ModifyTransferLimit).ToString();
        var setLimitSign = GenerateSignature(VerifierKeyPair, VerifierAddress, setLimitVerifyTime, _guardian, 0, salt,
            setLimitOpType);
        await CaContractStubManagerInfo1.SetTransferLimit.SendAsync(new SetTransferLimitInput
        {
            CaHash = _transferLimitTestCaHash,
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierServers[0].Id,
                        Signature = setLimitSign,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{setLimitVerifyTime},{VerifierAddress.ToBase58()},{salt},{setLimitOpType}"
                    }
                }
            },
            Symbol = "ELF",
            SingleLimit = _elfDefaultSingleLimit,
            DailyLimit = _elfDefaultDailyLimit
        });
    }

    [Fact]
    public async Task SetAnotherTokenTransferLimitTest()
    {
        await InitTransferLimitTest();

        var setLimitVerifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var setLimitOpType = Convert.ToInt32(OperationType.ModifyTransferLimit).ToString();
        var setLimitSign = GenerateSignature(VerifierKeyPair, VerifierAddress, setLimitVerifyTime, _guardian, 0, salt,
            setLimitOpType);
        await CaContractStubManagerInfo1.SetTransferLimit.SendAsync(new SetTransferLimitInput
        {
            CaHash = _transferLimitTestCaHash,
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierServers[0].Id,
                        Signature = setLimitSign,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{setLimitVerifyTime},{VerifierAddress.ToBase58()},{salt},{setLimitOpType}"
                    }
                }
            },
            Symbol = "CPU",
            SingleLimit = _elfDefaultSingleLimit,
            DailyLimit = _elfDefaultDailyLimit
        });

        var transferLimitResult = await CaContractStub.GetTransferLimit.CallAsync(new GetTransferLimitInput()
        {
            CaHash = _transferLimitTestCaHash,
            Symbol = "CPU",
        });

        transferLimitResult.SingleLimit.ShouldBe(_elfDefaultSingleLimit);
        transferLimitResult.DailyLimit.ShouldBe(_elfDefaultDailyLimit);
        transferLimitResult.DailyTransferredAmount.ShouldBe(0);
    }

    [Fact]
    public async Task SetTransferLimit_NoLimitTest()
    {
        await InitTransferLimitTest();

        var setLimitVerifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var setLimitOpType = Convert.ToInt32(OperationType.ModifyTransferLimit).ToString();
        var setLimitSign = GenerateSignature(VerifierKeyPair, VerifierAddress, setLimitVerifyTime, _guardian, 0, salt,
            setLimitOpType);
        await CaContractStubManagerInfo1.SetTransferLimit.SendAsync(new SetTransferLimitInput
        {
            CaHash = _transferLimitTestCaHash,
            GuardiansApproved =
            {
                new GuardianInfo
                {
                    IdentifierHash = _guardian,
                    Type = GuardianType.OfEmail,
                    VerificationInfo = new VerificationInfo
                    {
                        Id = _verifierServers[0].Id,
                        Signature = setLimitSign,
                        VerificationDoc =
                            $"{0},{_guardian.ToHex()},{setLimitVerifyTime},{VerifierAddress.ToBase58()},{salt},{setLimitOpType}"
                    }
                }
            },
            Symbol = "ELF",
            SingleLimit = -1,
            DailyLimit = -1
        });
    }


    [Fact]
    public async Task SetTransferLimit_ErrorOperationTypeTest()
    {
        await InitTransferLimitTest();
        {
            var setLimitVerifyTime = DateTime.UtcNow;
            var salt = Guid.NewGuid().ToString("N");
            var setLimitOpType = Convert.ToInt32(OperationType.Approve).ToString();
            var setLimitSign = GenerateSignature(VerifierKeyPair, VerifierAddress, setLimitVerifyTime, _guardian, 0,
                salt,
                setLimitOpType);
            var executionResult = await CaContractStubManagerInfo1.SetTransferLimit.SendWithExceptionAsync(
                new SetTransferLimitInput
                {
                    CaHash = _transferLimitTestCaHash,
                    GuardiansApproved =
                    {
                        new GuardianInfo
                        {
                            IdentifierHash = _guardian,
                            Type = GuardianType.OfEmail,
                            VerificationInfo = new VerificationInfo
                            {
                                Id = _verifierServers[0].Id,
                                Signature = setLimitSign,
                                VerificationDoc =
                                    $"{0},{_guardian.ToHex()},{setLimitVerifyTime},{VerifierAddress.ToBase58()},{salt},{setLimitOpType}"
                            }
                        }
                    },
                    Symbol = "ELF",
                    SingleLimit = _elfDefaultSingleLimit,
                    DailyLimit = _elfDefaultDailyLimit
                });
            executionResult.TransactionResult.Error.ShouldContain("JudgementStrategy validate failed");
        }
    }

    [Fact]
    public async Task GetTransferLimitTest()
    {
        await SetTransferLimitTest();
        var transferLimitResult = await CaContractStub.GetTransferLimit.CallAsync(new GetTransferLimitInput()
        {
            CaHash = _transferLimitTestCaHash,
            Symbol = "ELF",
        });

        transferLimitResult.SingleLimit.ShouldBe(_elfDefaultSingleLimit);
        transferLimitResult.DailyLimit.ShouldBe(_elfDefaultDailyLimit);
        transferLimitResult.DailyTransferredAmount.ShouldBe(0);
    }

    [Fact]
    public async Task SetDefaultTokenTransferLimitTest()
    {
        await InitTransferLimitTest();

        await CaContractStub.SetDefaultTokenTransferLimit.SendAsync(new SetDefaultTokenTransferLimitInput
        {
            Symbol = "ELF",
            DefaultLimit = _defaultTokenTransferLimit
        });
    }

    [Fact]
    public async Task GetDefaultTokenTransferLimitTest()
    {
        await SetDefaultTokenTransferLimitTest();

        var defaultTokenTransferLimit = await CaContractStub.GetDefaultTokenTransferLimit.CallAsync(
            new GetDefaultTokenTransferLimitInput()
            {
                Symbol = "ELF",
            });
        defaultTokenTransferLimit.Symbol.ShouldBe("ELF");
        defaultTokenTransferLimit.DefaultLimit.ShouldBe(_defaultTokenTransferLimit);
    }

    private async Task InitTransferLimitTest()
    {
        if (_isInitialized) return;

        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.ChangeOperationTypeInSignatureEnabled.SendAsync(new OperationTypeInSignatureEnabledInput()
        {
            OperationTypeInSignatureEnabled = true
        });

        await InitTestVerifierServer();
        await InitTestTransferLimitCaHolder();
        await InitDefaultTransferTokenLimit();

        _isInitialized = true;
    }

    private async Task InitTestVerifierServer()
    {
        if (_verifierServers != null) return;

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

        var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        _verifierServers = verifierServers.VerifierServers;
        _verifierId = _verifierServers[0].Id;
        _verifierId1 = _verifierServers[1].Id;
        _verifierId2 = _verifierServers[2].Id;
    }

    private async Task InitTestTransferLimitCaHolder()
    {
        if (_transferLimitTestCaHash != null) return;

        var verifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var opType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verifyTime, _guardian, 0, salt, opType);
        var id = _verifierServers[0].Id;

        var manager = new ManagerInfo
        {
            Address = User1Address,
            ExtraData = "123"
        };
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
                        $"{0},{_guardian.ToHex()},{verifyTime},{VerifierAddress.ToBase58()},{salt},{opType}"
                }
            },
            ManagerInfo = manager
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        _transferLimitTestCaHash = holderInfo.CaHash;

        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
    }

    private async Task InitDefaultTransferTokenLimit()
    {
        await CaContractStub.SetDefaultTokenTransferLimit.SendAsync(new SetDefaultTokenTransferLimitInput()
        {
            Symbol = "ELF",
            DefaultLimit = 10000
        });
    }
}