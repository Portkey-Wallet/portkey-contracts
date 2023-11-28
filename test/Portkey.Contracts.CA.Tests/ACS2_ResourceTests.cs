using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Standards.ACS1;
using AElf.Standards.ACS3;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private const string NativeToken = "ELF";
    [Fact]
    public async Task ACS2_GetResourceInfo_CreateHolder_Test()
    {
        var delegateInfo1 = new DelegateInfo
        {
            ContractAddress = CaContractAddress,
            MethodName = nameof(CaContractStub.CreateCAHolder),
            Delegations =
            {
                new Dictionary<string, long>
                {
                    [NativeToken] = 1000,
                }
            },
            IsUnlimitedDelegate = false
        };
        await TokenContractImplStub.SetTransactionFeeDelegateInfos.SendAsync(new SetTransactionFeeDelegateInfosInput
        {
            DelegatorAddress = Accounts[0].Address,
            DelegateInfoList = { delegateInfo1 }
        });
        var transaction = GenerateCaTransaction(Accounts[0].Address, nameof(CaContractUser1Stub.CreateCAHolder),
           await CreateHolderInputAsync());
        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result.WritePaths.Count.ShouldBeGreaterThan(0);
        result.NonParallelizable.ShouldBeFalse();
    }
    
     private async Task<CreateCAHolderInput> CreateHolderInputAsync()
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
       return new CreateCAHolderInput()
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
        };
    }
     
    private async Task<SocialRecoveryInput> CreateSocialRecoveryInput()
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(5)},{VerifierAddress.ToBase58()},{salt},{operationType}"
                }
            }
        };

        return new SocialRecoveryInput
        {
            ManagerInfo = new ManagerInfo
            {
                Address = User2Address,
                ExtraData = "567"
            },
            LoginGuardianIdentifierHash = _guardian,
            GuardiansApproved = { guardianApprove }
        };
    }

    [Fact]
    public async Task ACS2_GetResourceInfo_SocialRecovery_Test()
    {
        var transaction = GenerateCaTransaction(Accounts[0].Address, nameof(CaContractStub.SocialRecovery),
          await  CreateSocialRecoveryInput());

        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result.NonParallelizable.ShouldBeFalse();
        result.WritePaths.Count.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public async Task ACS2_GetResourceInfo_SyncHolderInfo_Test()
    {
       var existInput = await CreateValidateCAHolderInfoWithManagerInfosExistsInput();
       var verifierInfo = GenerateCaTransaction(Accounts[0].Address,nameof(CaContractStub.ValidateCAHolderInfoWithManagerInfosExists),existInput);
       var syncHolderInfoInput = new SyncHolderInfoInput
       {
           VerificationTransactionInfo = new VerificationTransactionInfo
           {
               FromChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
               ParentChainHeight = 100,
               TransactionBytes = verifierInfo.ToByteString(),
           }
       };
      var transaction = GenerateCaTransaction(Accounts[0].Address,nameof(CaContractStub.SyncHolderInfo),syncHolderInfoInput);
      var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
      result.NonParallelizable.ShouldBeFalse();
      result.WritePaths.Count.ShouldBeGreaterThan(0);
    }
    
    [Fact]
    public async Task ACS2_GetResourceInfo_ValidateCAHolderInfoWithManagerInfosExistsInput_Test()
    {
        var existInput = await CreateValidateCAHolderInfoWithManagerInfosExistsInput();
        var verifierInfo = GenerateCaTransaction(Accounts[0].Address,nameof(CaContractStub.ValidateCAHolderInfoWithManagerInfosExists),existInput);
        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(verifierInfo);
        result.NonParallelizable.ShouldBeFalse();
        result.WritePaths.Count.ShouldBeGreaterThan(0);
    }
    
    

    private async Task<ValidateCAHolderInfoWithManagerInfosExistsInput> CreateValidateCAHolderInfoWithManagerInfosExistsInput()
    {
        await CreateHolder();
        var getHolderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
       return new ValidateCAHolderInfoWithManagerInfosExistsInput
        {
            CaHash = getHolderInfo.CaHash,
            ManagerInfos = { getHolderInfo.ManagerInfos },
            LoginGuardians = { getHolderInfo.GuardianList.Guardians.Select(g => g.IdentifierHash) },
            CreateChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
            GuardianList = getHolderInfo.GuardianList
        };
    }


    [Fact]
    public async Task ACS2_GetResourceInfo_UnsupportedMethod_Test()
    {
        var transaction = GenerateCaTransaction(Accounts[0].Address, "TestMethod",
            new Empty());

        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result.NonParallelizable.ShouldBeTrue();
    }
    
    private Transaction GenerateCaTransaction(Address from, string method, IMessage input)
    {
        return new Transaction
        {
            From = from,
            To = CaContractAddress,
            MethodName = method,
            Params = ByteString.CopyFrom(input.ToByteArray())
        };
    }
}