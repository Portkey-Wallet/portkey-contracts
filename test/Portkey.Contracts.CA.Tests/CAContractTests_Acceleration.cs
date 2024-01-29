using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using JetBrains.Annotations;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests : CAContractTestBase
{
    private readonly Hash _specificCaHash = HashHelper.ComputeFrom("12345");

    private async Task CreateHolderOnNonCreateChainTest_Init()
    {
        await Initiate();
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

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaHash.ShouldBe(_specificCaHash);
        caInfo.CreateChainId.ShouldBe(SideChianId);
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_CloseCheckOperationDetails()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(false);
        var result = await CreateHolderOnNonCreateChain(null, false);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not supported");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_OperationDetailsIsNull()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(null, true);
        var result = await CaContractStub.GetHolderInfo.SendWithExceptionAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not found ca_hash by a the loginGuardianIdentifierHash");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_NoPermission()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CaContractStub.AddCreatorController.SendAsync(new ControllerInput
        {
            Address = User1Address
        });
        await CaContractStub.RemoveCreatorController.SendAsync(new ControllerInput
        {
            Address = DefaultAddress
        });
        await CaContractStub.ChangeAdmin.SendAsync(new AdminInput
        {
            Address = User1Address
        });
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput();
        var result = await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
            createCaHolderOnNonCreateChainInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_GuardianApproveIsNull()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput();
        IExecutionResult<Empty> result = await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
            createCaHolderOnNonCreateChainInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("invalid input guardian");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_ManagerIsNull()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "111"
                }
            }
        };
        var result = await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
            createCaHolderOnNonCreateChainInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_CreateChainId()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "111"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        };
        IExecutionResult<Empty> result = await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
            createCaHolderOnNonCreateChainInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Invalid input CreateChainId");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_CaHashIsNull()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "111"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            CaHash = Hash.Empty,
            CreateChainId = SideChianId
        };
        var result = await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
            createCaHolderOnNonCreateChainInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Invalid input CaHash");
    }

    [Fact]
    public async Task CreateHolderOnNonCreateChainTest_Fail_HolderInfoExist()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true, MainChainId);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User1Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaHash.ShouldBe(_specificCaHash);
        caInfo.CreateChainId.ShouldBe(SideChianId);
        
        await CreateHolderOnNonCreateChain(User2Address.ToBase58(), true, MainChainId, User2Address);
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User1Address);
    }
    
    [Fact]
    public async Task CreateHolderTest_WhenAccelerateFailed()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true, MainChainId);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            CaHash = _specificCaHash
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User1Address);

        await CreateHolderWithOperationDetails(User2Address.ToBase58(), User2Address);
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User2Address);
        
        var result = await CaContractStub.GetHolderInfo.SendWithExceptionAsync(new GetHolderInfoInput()
        {
            CaHash = _specificCaHash
        });
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Holder is not found");
    }

    [Fact]
    public async Task AccelerateSocialRecoveryTest()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true);
        await AccelerateSocialRecovery(User2Address.ToBase58(), true);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(2);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaHash.ShouldBe(_specificCaHash);
        caInfo.CreateChainId.ShouldBe(SideChianId);
    }
    
    [Fact]
    public async Task AccelerateSocialRecoveryTest_Fail_CloseCheckOperationDetails()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true);
        await SetCheckOperationDetailsInSignatureEnabled(false);
        var result = await AccelerateSocialRecovery(User2Address.ToBase58(), false);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not on registered chain");
    }

    [Fact]
    public async Task AccelerateSocialRecoveryTest_Fail_OperationDetailIsNull()
    {
        await CreateHolderOnNonCreateChainTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await CreateHolderOnNonCreateChain(User1Address.ToBase58(), true);
        var result = await AccelerateSocialRecovery(null, true);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldNotBe(User2Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaHash.ShouldBe(_specificCaHash);
        caInfo.CreateChainId.ShouldBe(SideChianId);
    }

    [ItemCanBeNull]
    private async Task<IExecutionResult<Empty>> CreateHolderOnNonCreateChain(string operationDetails, bool success, int chainId = 0, Address mananger = null)
    {
        chainId = chainId == 0 ? SideChianId : chainId;
        mananger = mananger == null ? User1Address : mananger;
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime, _guardian, 0, salt,
            operationType, chainId, operationDetails);
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var createCaHolderOnNonCreateChainInput = new CreateCAHolderOnNonCreateChainInput()
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
                        $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = mananger,
                ExtraData = "123"
            },
            CaHash = _specificCaHash,
            CreateChainId = SideChianId
        };
        if (!success)
        {
            return await CaContractStub.CreateCAHolderOnNonCreateChain.SendWithExceptionAsync(
                createCaHolderOnNonCreateChainInput);
        }

        return await CaContractStub.CreateCAHolderOnNonCreateChain.SendAsync(createCaHolderOnNonCreateChainInput);
    }

    private async Task<IExecutionResult<Empty>> AccelerateSocialRecovery(string operationDetails, bool success, int chainId = 0)
    {
        chainId = chainId == 0 ? SideChianId : chainId;
        var verificationTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var operationType = Convert.ToInt32(OperationType.SocialRecovery).ToString();
        var signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verificationTime.AddSeconds(5), _guardian,
            0, salt, operationType, chainId, operationDetails);
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId}"
                }
            }
        };

        var socialRecoveryInput = new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        };
        if (!success)
        {
            return await CaContractStub.SocialRecovery.SendWithExceptionAsync(socialRecoveryInput);
        }

        return await CaContractStub.SocialRecovery.SendAsync(socialRecoveryInput);
    }
}