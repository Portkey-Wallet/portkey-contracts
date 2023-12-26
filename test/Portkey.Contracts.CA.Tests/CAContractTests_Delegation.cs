using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task RegisterProjectDelegate_Success()
    {
        await Initiate();
        var result = await CaContractStub.RegisterProjectDelegatee.SendAsync(new RegisterProjectDelegateeInput()
        {
            ProjectName = "portkey",
            Salts = {"1", "2", "1"},
            Signer = DefaultAddress
        });
        var projectDelegateHash = Hash.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        projectDelegate.DelegateeHashList.Count.ShouldBe(2);
        projectDelegate.DelegateeAddressList.Count.ShouldBe(2);
        projectDelegate.ProjectController.ShouldBe(DefaultAddress);

        await CaContractStub.AddProjectDelegateeList.SendAsync(new AddProjectDelegateeListInput
        {
            ProjectHash = projectDelegateHash,
            Salts = {"3"}
        });
        projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        projectDelegate.DelegateeHashList.Count.ShouldBe(3);
        projectDelegate.DelegateeAddressList.Count.ShouldBe(3);
        projectDelegate.DelegateeHashList[2]
            .ShouldBe(HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("3"), projectDelegateHash));

        await CaContractStub.RemoveProjectDelegateeList.SendAsync(new RemoveProjectDelegateeListInput()
        {
            ProjectHash = projectDelegateHash,
            DelegateeHashList = {projectDelegate.DelegateeHashList[0]}
        });
        projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        projectDelegate.DelegateeHashList.Count.ShouldBe(2);
        projectDelegate.DelegateeAddressList.Count.ShouldBe(2);
        projectDelegate.DelegateeHashList[0]
            .ShouldBe(HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("2"), projectDelegateHash));

        await CaContractStub.SetProjectDelegateSigner.SendAsync(new SetProjectDelegateSignerInput()
        {
            ProjectHash = projectDelegateHash,
            Signer = User1Address
        });
        projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        projectDelegate.Signer.ShouldBe(User1Address);

        await CaContractStub.SetProjectDelegateController.SendAsync(new SetProjectDelegateControllerInput
        {
            ProjectHash = projectDelegateHash,
            ProjectController = User1Address
        });
        projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        projectDelegate.ProjectController.ShouldBe(User1Address);

        await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            To = projectDelegate.DelegateeAddressList[0],
            Amount = 10000000000,
            Symbol = "ELF",
            Memo = "test"
        });
        await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            To = User1Address,
            Amount = 10000000000,
            Symbol = "ELF",
            Memo = "test"
        });
        var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
        {
            Owner = projectDelegate.DelegateeAddressList[0],
            Symbol = "ELF"
        });
        balance.Balance.ShouldBe(10000000000);

        await CaContractUser1Stub.WithdrawProjectDelegateeToken.SendAsync(new WithdrawProjectDelegateeTokenInput
        {
            ProjectHash = projectDelegateHash,
            DelegateeHash = projectDelegate.DelegateeHashList[0],
            Amount = 5000000000
        });
        balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput()
        {
            Owner = projectDelegate.DelegateeAddressList[0],
            Symbol = "ELF"
        });
        balance.Balance.ShouldBe(5000000000);
    }


    [Fact]
    public async Task RegisterProjectDelegate_Fail()
    {
        await Initiate();
        var result = await CaContractStub.RegisterProjectDelegatee.SendWithExceptionAsync(
            new RegisterProjectDelegateeInput()
            {
                ProjectName = "",
                Salts = {"1"}
            });
        result.TransactionResult.Error.ShouldContain("Invalid project name.");
        result = await CaContractStub.RegisterProjectDelegatee.SendWithExceptionAsync(
            new RegisterProjectDelegateeInput()
            {
                ProjectName = "portkey",
                Salts = { }
            });
        result.TransactionResult.Error.ShouldContain("Input salts is empty");
        result = await CaContractStub.RegisterProjectDelegatee.SendWithExceptionAsync(
            new RegisterProjectDelegateeInput()
            {
                ProjectName = "portkey",
                Salts = {"1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11"}
            });
        result.TransactionResult.Error.ShouldContain("Exceed salts max count");
    }

    [Fact]
    public async Task UpdateProjectDelegate_NoPermission()
    {
        await Initiate();
        var projectDelegateeHash = await RegisterProjectDelegatee();
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateeHash);
        await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            To = User1Address,
            Amount = 10000000000,
            Symbol = "ELF",
            Memo = "test"
        });
        var result = await CaContractUser1Stub.AddProjectDelegateeList.SendWithExceptionAsync(
            new AddProjectDelegateeListInput()
            {
                ProjectHash = projectDelegateeHash,
                Salts = {"2"}
            });
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractUser1Stub.RemoveProjectDelegateeList.SendWithExceptionAsync(
            new RemoveProjectDelegateeListInput()
            {
                ProjectHash = projectDelegateeHash,
                DelegateeHashList = {Hash.Empty}
            });
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractUser1Stub.SetProjectDelegateController.SendWithExceptionAsync(
            new SetProjectDelegateControllerInput()
            {
                ProjectHash = projectDelegateeHash,
                ProjectController = User2Address
            });
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractUser1Stub.SetProjectDelegateSigner.SendWithExceptionAsync(
            new SetProjectDelegateSignerInput()
            {
                ProjectHash = projectDelegateeHash,
                Signer = User2Address
            });
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractUser1Stub.WithdrawProjectDelegateeToken.SendWithExceptionAsync(
            new WithdrawProjectDelegateeTokenInput()
            {
                ProjectHash = projectDelegateeHash,
                DelegateeHash = projectDelegate.DelegateeHashList[0],
                Amount = 111
            });
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractUser1Stub.SetCaProjectDelegateHash.SendWithExceptionAsync(projectDelegateeHash);
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task SetProjectDelegate()
    {
        await Initiate();
        var projectHash = await RegisterProjectDelegatee();
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectHash);
        await CaContractStub.SetCaProjectDelegateHash.SendAsync(projectHash);
        await CreateHolderOnly(projectHash);
        var deletatees = await TokenContractStub.GetTransactionFeeDelegatees.CallAsync(
            new GetTransactionFeeDelegateesInput
            {
                DelegatorAddress = User1Address
            });
        var caAddress = deletatees.DelegateeAddresses[0];
    }
    
    [Fact]
    public async Task UpgradeProjectDelegate()
    {
        await Initiate();
        var caHash = await CreateHolderOnly(null);
        var deletatees = await TokenContractStub.GetTransactionFeeDelegatees.CallAsync(
            new GetTransactionFeeDelegateesInput
            {
                DelegatorAddress = User1Address
            });
        var caAddress = deletatees.DelegateeAddresses[0];
        var projectDeletatees = await TokenContractStub.GetTransactionFeeDelegatees.CallAsync(
            new GetTransactionFeeDelegateesInput
            {
                DelegatorAddress = caAddress
            });
        projectDeletatees.DelegateeAddresses.Count.ShouldBe(0);
        
        var projectHash = await RegisterProjectDelegatee();
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectHash);
        await CaContractStub.SetCaProjectDelegateHash.SendAsync(projectHash);
        var manager = new ManagerInfo()
        {
            Address = User2Address,
            ExtraData = "iphone14-2023"
        };
        await CaContractUser1Stub.AddManagerInfo.SendAsync(new AddManagerInfoInput()
        {
            CaHash = caHash,
            ManagerInfo = manager
        });
        
        projectDeletatees = await TokenContractStub.GetTransactionFeeDelegatees.CallAsync(
            new GetTransactionFeeDelegateesInput
            {
                DelegatorAddress = caAddress
            });
        projectDeletatees.DelegateeAddresses.Count.ShouldBe(1);
        int selectIndex =
            (int) Math.Abs(caAddress.ToByteArray().ToInt64(true) % projectDelegate.DelegateeHashList.Count);
        projectDeletatees.DelegateeAddresses[0].ShouldBe(projectDelegate.DelegateeAddressList[selectIndex]);
    }

    private async Task<Hash> RegisterProjectDelegatee()
    {
        var result = await CaContractStub.RegisterProjectDelegatee.SendAsync(new RegisterProjectDelegateeInput()
        {
            ProjectName = "Portkey",
            Salts = {"1", "2"},
            Signer = DefaultAddress
        });
        var projectDelegateHash = Hash.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectDelegateHash);
        foreach (var delegateeAddress in projectDelegate.DelegateeAddressList)
        {
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Amount = 1000000000000,
                Symbol = "ELF",
                To = delegateeAddress
            });
        }

        projectDelegate.DelegateeHashList.Count.ShouldBe(2);
        return projectDelegateHash;
    }

    [Fact]
    public async Task SetSecondaryDelegationFeeTest_Fail_InvalidInput()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        var secondaryDelegationFee = await CaContractStub.GetSecondaryDelegationFee.CallAsync(new Empty());
        secondaryDelegationFee.Amount.ShouldBe(0);
        await CaContractStub.SetSecondaryDelegationFee.SendAsync(new SetSecondaryDelegationFeeInput
        {
            DelegationFee = new SecondaryDelegationFee
            {
                Amount = 10000000000
            }
        });

        var result =
            await CaContractStub.SetSecondaryDelegationFee.SendWithExceptionAsync(new SetSecondaryDelegationFeeInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }

    private async Task<Hash> CreateHolderOnly(Hash projectHash)
    {
        var verificationTime = DateTime.UtcNow;
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
        
        var delegateInfo = new DelegateInfo()
        {
            IdentifierHash = _guardian,
            ChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
            Timestamp = verificationTime.ToTimestamp(),
            ExpirationTime = 3600,
            ProjectHash = projectHash,
            Delegations =
            {
                new Dictionary<string, long>
                {
                    ["ELF"] = 100
                }
            },
            IsUnlimitedDelegate = true,
            Signature = ""
        };
        var delegateInfoSignature = CryptoHelper.SignWithPrivateKey(DefaultKeyPair.PrivateKey, HashHelper.ComputeFrom(delegateInfo).ToByteArray()).ToHex();
        delegateInfo.Signature = delegateInfoSignature;
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
                        $"{0},{_guardian.ToHex()},{verificationTime.AddSeconds(10)},{VerifierAddress.ToBase58()},{salt},{operationType},{IntMainChainId}"
                }
            },
            ManagerInfo = new ManagerInfo
            {
                Address = User1Address,
                ExtraData = "123"
            },
            DelegateInfo = delegateInfo
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

}