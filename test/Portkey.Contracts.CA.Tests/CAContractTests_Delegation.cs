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
        
        var projectResult = await CaContractStub.AddProjectDelegateeList.SendWithExceptionAsync(new AddProjectDelegateeListInput
        {
            ProjectHash = projectDelegateHash,
            Salts = {"3"}
        });
        projectResult.TransactionResult.Error.ShouldContain("Input salts already existed");

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
    public async Task SetCAProjectDelegate()
    {
        await Initiate();
        var projectHash = await RegisterProjectDelegatee();
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectHash);
        await CaContractStub.SetCaProjectDelegateHash.SendAsync(projectHash);
        await CreateHolderOnly(null);
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
        int selectIndex =
            (int) Math.Abs(caAddress.ToByteArray().ToInt64(true) % projectDelegate.DelegateeHashList.Count);
        projectDeletatees.DelegateeAddresses[0].ShouldBe(projectDelegate.DelegateeAddressList[selectIndex]);
    }

    [Fact]
    public async Task AddTransactionWhiteList()
    {
        await Initiate();
        await CaContractStub.AddTransactionWhitelist.SendAsync(new WhitelistTransactions
        {
            MethodNames = { "SocialRecovery", "AddGuardian"}
        });
        var result = await CaContractStub.GetTransactionWhitelist.CallAsync(new Empty());
        result.MethodNames.Count.ShouldBe(2);
        result.MethodNames[0].ShouldBe("SocialRecovery");
        result.MethodNames[1].ShouldBe("AddGuardian");
        await CaContractStub.RemoveTransactionWhitelist.SendAsync(new WhitelistTransactions
        {
            MethodNames = { "AddGuardian" }
        });
        result = await CaContractStub.GetTransactionWhitelist.CallAsync(new Empty());
        result.MethodNames.Count.ShouldBe(1);
        result.MethodNames[0].ShouldBe("SocialRecovery");
    }

    [Fact]
    public async Task<Address> SetProjectDelegate()
    {
        await Initiate();
        var projectHash = await RegisterProjectDelegatee();
        var projectDelegate = await CaContractStub.GetProjectDelegatee.CallAsync(projectHash);
        await CaContractStub.SetCaProjectDelegateHash.SendAsync(projectHash);
        await CaContractStub.AddTransactionWhitelist.SendAsync(new WhitelistTransactions
        {
            MethodNames = { "SocialRecovery" }
        });
        var delegateInfo = new DelegateInfo()
        {
            IdentifierHash = _guardian,
            ChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
            Timestamp = DateTime.UtcNow.ToTimestamp(),
            ExpirationTime = 3600,
            ProjectHash = projectHash,
            Delegations =
            {
                new Dictionary<string, long>
                {
                    ["ELF"] = 10000000000
                }
            },
            IsUnlimitedDelegate = false,
            Signature = ""
        };
        var delegateInfoSignature = CryptoHelper.SignWithPrivateKey(DefaultKeyPair.PrivateKey, HashHelper.ComputeFrom(delegateInfo).ToByteArray()).ToHex();
        delegateInfo.Signature = delegateInfoSignature;
        
        await CreateHolderOnly(delegateInfo);
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
        
        int selectIndex =
            (int) Math.Abs(caAddress.ToByteArray().ToInt64(true) % projectDelegate.DelegateeHashList.Count);
        var output = await TokenContractStub.GetTransactionFeeDelegateeList.CallAsync(new GetTransactionFeeDelegateeListInput
        {
            DelegatorAddress = caAddress,
            ContractAddress = CaContractAddress,
            MethodName = "SocialRecovery"
        });
        output.DelegateeAddresses.Count.ShouldBe(1);
        output.DelegateeAddresses[0].ShouldBe(projectDelegate.DelegateeAddressList[selectIndex]);
        var transactionFeeDelegationInfo = await TokenContractStub.GetTransactionFeeDelegateInfo.CallAsync(new GetTransactionFeeDelegateInfoInput
        {
            DelegatorAddress = caAddress,
            ContractAddress = CaContractAddress,
            MethodName = "SocialRecovery",
            DelegateeAddress = projectDelegate.DelegateeAddressList[selectIndex]
        });
        transactionFeeDelegationInfo.IsUnlimitedDelegate.ShouldBeFalse();
        transactionFeeDelegationInfo.Delegations["ELF"].ShouldBe(10000000000);
        return caAddress;
    }

    [Fact]
    public async Task AssignAndRemoveDelegatee()
    {
        var caAddress = await SetProjectDelegate();
        var projectHash = await CaContractStub.GetCaProjectDelegateHash.CallAsync(new Empty());
        var assignInfos = new RepeatedField<AssignDelegateInfo>();
        assignInfos.Add(new AssignDelegateInfo
        {
            IsUnlimitedDelegate = false,
            ContractAddress = CaContractAddress,
            MethodName = "AddManagerInfo",
            Delegations = {
                new Dictionary<string, long>
                {
                    ["ELF"] = 10000000000
                }
            },
        });
        await CaContractStub.AssignProjectDelegatee.SendAsync(new AssignProjectDelegateeInput
        {
            ProjectHash = projectHash,
            CaAddress = caAddress,
            AssignDelegateInfos = { assignInfos }
        });
        var output = await TokenContractStub.GetTransactionFeeDelegateeList.CallAsync(new GetTransactionFeeDelegateeListInput
        {
            DelegatorAddress = caAddress,
            ContractAddress = CaContractAddress,
            MethodName = "AddManagerInfo"
        });
        output.DelegateeAddresses.Count.ShouldBe(1);
        await CaContractStub.RemoveProjectDelegatee.SendAsync(new RemoveProjectDelegateeInput()
        {
            ProjectHash = projectHash,
            CaAddress = caAddress,
            DelegateTransactionList = { new DelegateTransaction
            {
                ContractAddress = CaContractAddress,
                MethodName = "AddManagerInfo"
            } }
        });
        output = await TokenContractStub.GetTransactionFeeDelegateeList.CallAsync(new GetTransactionFeeDelegateeListInput
        {
            DelegatorAddress = caAddress,
            ContractAddress = CaContractAddress,
            MethodName = "AddManagerInfo"
        });
        output.DelegateeAddresses.Count.ShouldBe(0);
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
        await Initiate();
        var secondaryDelegationFee = await CaContractStub.GetProjectDelegationFee.CallAsync(new Empty());
        secondaryDelegationFee.Amount.ShouldBe(0);
        await CaContractStub.SetProjectDelegationFee.SendAsync(new SetProjectDelegationFeeInput()
        {
            DelegationFee = new ProjectDelegationFee()
            {
                Amount = 10000000000
            }
        });

        var result =
            await CaContractStub.SetProjectDelegationFee.SendWithExceptionAsync(new SetProjectDelegationFeeInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }

    private async Task<Hash> CreateHolderOnly(DelegateInfo delegateInfo = null)
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
            },
            DelegateInfo = delegateInfo
        });
        
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        holderInfo.GuardianList.Guardians.First().IdentifierHash.ShouldBe(_guardian);
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = holderInfo.CaAddress
        });
        return holderInfo.CaHash;
    }

}