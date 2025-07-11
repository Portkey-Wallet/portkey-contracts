using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Cryptography;
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

    private async Task ReportPreCrossChainSyncHolderInfoTest_Init()
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
    public async Task ReportPreCrossChainSyncHolderInfoTest()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
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
    public async Task ReportPreCrossChainSyncHolderInfoTest_CloseCheckOperationDetails()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
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
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_CloseCheckOperationDetails()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        var result = await ReportPreCrossChainSyncHolderInfo(null, false);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not supported");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_OperationDetailsIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var result = await ReportPreCrossChainSyncHolderInfo(null, false);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not supported");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_NoPermission()
    {
        await Initiate();
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
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput();
        var result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_GuardianApproveIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput();
        IExecutionResult<Empty> result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("invalid input guardian");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_ManagerIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "1,2,3,4,5,6,7,8"
                }
            }
        };
        var result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");
    }
    
    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_ManagerAddressIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "1,2,3,4,5,6,7,8"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = null,
                ExtraData = "123"
            }
        };
        var result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("invalid input managerInfo");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_CreateChainId()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "1,2,3,4,5,6,7,8"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            }
        };
        IExecutionResult<Empty> result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Invalid input CreateChainId");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_CaHashIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput
        {
            GuardianApproved = new GuardianInfo
            {
                Type = GuardianType.OfEmail,
                IdentifierHash = _guardian,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.Empty,
                    Signature = ByteString.Empty,
                    VerificationDoc = "1,2,3,4,5,6,7,8"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            CaHash = null,
            CreateChainId = SideChianId
        };
        var result = await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
            reportPreCrossChainSyncHolderInfoInput);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Invalid input CaHash");
    }

    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_HolderInfoExist()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true, MainChainId);
        var caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User1Address);
        caInfo.GuardianList.Guardians.Count.ShouldBe(1);
        caInfo.CaHash.ShouldBe(_specificCaHash);
        caInfo.CreateChainId.ShouldBe(SideChianId);

        await ReportPreCrossChainSyncHolderInfo(User2Address.ToBase58(), true, MainChainId, User2Address);
        caInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ManagerInfos.Count.ShouldBe(1);
        caInfo.ManagerInfos.First().Address.ShouldBe(User1Address);
    }
    
    [Fact]
    public async Task ReportPreCrossChainSyncHolderInfoTest_Fail_GuardianApproveFail()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User2Address.ToBase58(), true, MainChainId);
        var caInfo = await CaContractStub.GetHolderInfo.CallWithExceptionAsync(new GetHolderInfoInput()
        {
            LoginGuardianIdentifierHash = _guardian
        });
        caInfo.ShouldNotBeNull();
        caInfo.Value.ShouldContain("Not found ca_hash by a the loginGuardianIdentifierHash");
    }

    [Fact]
    public async Task CreateHolderTest_WhenAccelerateFailed()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true, MainChainId);
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
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await SetCheckOperationDetailsInSignatureEnabled(true);
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
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
    public async Task AccelerateSocialRecoveryTest_CloseCheckOperationDetails()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
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
    public async Task AccelerateSocialRecoveryTest_Fail_OperationDetailIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
        IExecutionResult<Empty> result = await AccelerateSocialRecovery(null, false);
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not on registered chain");
        
    }
    
    [Fact]
    public async Task SocialRecoveryTest_Fail_GuardianApprovedIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
        var result = await CaContractStub.SocialRecovery.SendWithExceptionAsync(new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian
        });
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not on registered chain");
    }
    
    [Fact]
    public async Task SocialRecoveryTest_Fail_VerificationInfoIsNull()
    {
        await ReportPreCrossChainSyncHolderInfoTest_Init();
        await ReportPreCrossChainSyncHolderInfo(User1Address.ToBase58(), true);
        var guardianApprove = new List<GuardianInfo>
        {
            new()
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerificationInfo = null
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
        result.ShouldNotBeNull();
        result.TransactionResult.Error.ShouldContain("Not on registered chain");
    }

    [ItemCanBeNull]
    private async Task<IExecutionResult<Empty>> ReportPreCrossChainSyncHolderInfo(string operationDetails, bool success,
        int chainId = 0, Address mananger = null)
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
        var reportPreCrossChainSyncHolderInfoInput = new ReportPreCrossChainSyncHolderInfoInput()
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
                        ? $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId}"
                        : $"{0},{_guardian.ToHex()},{verificationTime},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId},{HashHelper.ComputeFrom(operationDetails).ToHex()}"
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
            return await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendWithExceptionAsync(
                reportPreCrossChainSyncHolderInfoInput);
        }

        return await CaContractStub.ReportPreCrossChainSyncHolderInfo.SendAsync(reportPreCrossChainSyncHolderInfoInput);
    }

    private async Task<IExecutionResult<Empty>> AccelerateSocialRecovery(string operationDetails, bool success,
        int chainId = 0)
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
                    VerificationDoc = string.IsNullOrWhiteSpace(operationDetails)
                        ? $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId}"
                        : $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType},{chainId},{HashHelper.ComputeFrom(operationDetails).ToHex()}"
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

    [Fact]
    public async Task SignatureTest()
    {
        var guardianInfo = new GuardianInfo
        {
            Type = GuardianType.OfGoogle,
            IdentifierHash = HashHelper.ComputeFrom("107025214944261598742"),
            VerificationInfo = new VerificationInfo
            {
                Id = Hash.Empty,
                Signature = ByteStringHelper.FromHexString(
                    "54c956fc3b1a578360356cc5e1488361214e26836cb05cf37aa33d541ba8b8f3769453f0fa8c1945ca09c6fd62363c1fa5e90e147c6b5014528ed1be3ce7db7500"),
                VerificationDoc =
                    "2,445d490722d306d86d86661b6fc70e81a4ebc7da81951ea9d848e148f96dd77f,2024/01/29 14:42:55.930,2XyMGfQ6sPY8bArZDsj8yDcH23xiqui4euKcygpa1dyjG1GWKx,93667de1357e204ca5841fd365cce68c,1,9992731"
            }
        };
        var verifierAddress = "2XyMGfQ6sPY8bArZDsj8yDcH23xiqui4euKcygpa1dyjG1GWKx";
        var operationDetails = "jhm5fRei4Wsamw5TmS47CspQ5geVjhp2eKnMqt6YKLPo1LhT6";

        var result = CheckVerifierSignature(guardianInfo, verifierAddress, false, operationDetails);
        result.ShouldBeTrue();

        guardianInfo.VerificationInfo.Signature = ByteStringHelper.FromHexString(
            "070fc10ff4fbd9659d4f5646c6967199450fea30b82271d4d3b1d03a227e1ad36941701198b04640ae2b42b85090c1d18ba45bd56464876c73d8bd81e4a5541501");
        guardianInfo.VerificationInfo.VerificationDoc =
            "2,445d490722d306d86d86661b6fc70e81a4ebc7da81951ea9d848e148f96dd77f,2024/01/29 15:17:50.850,2XyMGfQ6sPY8bArZDsj8yDcH23xiqui4euKcygpa1dyjG1GWKx,93667de1357e204ca5841fd365cce68c,1,9992731";
        result = CheckVerifierSignature(guardianInfo, verifierAddress, false, operationDetails);
        result.ShouldBeFalse();
        result = CheckVerifierSignature(guardianInfo, verifierAddress, true, operationDetails);
        result.ShouldBeTrue();
    }

    private bool CheckVerifierSignature(GuardianInfo guardianInfo, string verifierAddress, bool hasOperationDetails,
        string operationDetails)
    {
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        string realVerificationDoc;
        if (hasOperationDetails)
        {
            realVerificationDoc = $"{guardianInfo.VerificationInfo.VerificationDoc},{operationDetails}";
        }
        else
        {
            realVerificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        }

        var data = HashHelper.ComputeFrom(realVerificationDoc);
        if (!CryptoHelper.RecoverPublicKey(guardianInfo.VerificationInfo.Signature.ToByteArray(), data.ToByteArray(),
                out var publicKey))
        {
            return false;
        }

        var verifierAddressFromPublicKey = Address.FromPublicKey(publicKey);
        if (verifierAddressFromPublicKey.ToBase58() != verifierAddress)
        {
            return false;
        }

        return true;
    }
}