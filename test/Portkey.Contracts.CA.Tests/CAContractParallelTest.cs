using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Standards.ACS3;
using AElf.Types;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class  CAContractTests
{
    [Fact]
    public async Task ACS2_GetResourceInfo_ManagerForwardCall_Test()
    {
        await SetManagerForwardCallParallel_Test();
        await TokenContractStub.SetTransactionFeeDelegations.SendAsync(new SetTransactionFeeDelegationsInput
        {
            DelegatorAddress = Accounts[0].Address,
            Delegations =
            {
                new Dictionary<string, long>
                {
                    [CAContractConstants.ELFTokenSymbol] = 100000000
                }
            }
        });
        var transaction = GenerateCaTransaction(Accounts[0].Address, nameof(CaContractStub.ManagerForwardCall),
            GetManagerForwardCallInput()
        );

        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result.NonParallelizable.ShouldBeFalse();
        result.WritePaths.Count.ShouldBeGreaterThan(0);
        
        await CaContractStub.SetManagerForwardCallParallelInfo.SendAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
                IsParallel = false 
            });
        var result1 = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result1.NonParallelizable.ShouldBeTrue();
        
        var transaction1 = GenerateCaTransaction(Accounts[0].Address, nameof(CaContractStub.AddManagerInfo),
            new AddManagerInfoInput()
        );
        var result2 = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction1);
        result2.NonParallelizable.ShouldBeTrue();
    }

    [Fact]
    public async Task SetManagerForwardCallParallel_Test()
    {
        await Initiate();
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = Accounts[1].Address
        });
        var result = await CaContractStubManagerInfo1.SetManagerForwardCallParallelInfo.SendWithExceptionAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
                IsParallel = true
            });
        ;
        result.TransactionResult.Error.ShouldContain("No permission");

        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendWithExceptionAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                MethodName = "transfer"
            });
        result.TransactionResult.Error.ShouldContain("Invalid input.");
        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendWithExceptionAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
            });
        result.TransactionResult.Error.ShouldContain("Invalid input.");
        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
            });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
                IsParallel = true
            });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.TransferFrom),
                IsParallel = true
            });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        
        result = await CaContractStub.SetManagerForwardCallParallelInfo.SendAsync(
            new SetManagerForwardCallParallelInfoInput
            {
                ContractAddress = VerifierAddress,
                MethodName = nameof(AuthorizationContractContainer.AuthorizationContractStub.Approve),
                IsParallel = true
            });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    [Fact]
    public async Task GetManagerForwardCallParallel_Test()
    {
        var result = await CaContractStub.GetManagerForwardCallParallelInfo.CallAsync(
            new GetManagerForwardCallParallelInfoInput()
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
            });
        result.IsParallel.ShouldBe(false);
        await SetManagerForwardCallParallel_Test();
        result = await CaContractStub.GetManagerForwardCallParallelInfo.CallAsync(
            new GetManagerForwardCallParallelInfoInput()
            {
                ContractAddress = TokenContractAddress,
                MethodName = nameof(TokenContractStub.Transfer),
            });
        result.IsParallel.ShouldBe(true);
    }

    private async Task Init()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
    }


    private ManagerForwardCallInput GetManagerForwardCallInput()
    {
        return new ManagerForwardCallInput
        {
            CaHash = new Hash(),
            ContractAddress = TokenContractAddress,
            MethodName = nameof(TokenContractStub.Transfer),
            Args = new TransferInput
            {
                To = DefaultAddress,
                Symbol = "ELF",
                Amount = 100,
            }.ToByteString(),
            GuardiansApproved = { new GuardianInfo
            {
                VerificationInfo = new VerificationInfo
                {
                    Signature = ByteStringHelper.FromHexString(CryptoHelper.SignWithPrivateKey(
                        DefaultKeyPair.PrivateKey, HashHelper.ComputeFrom("AAA").ToByteArray()).ToHex())
                }
            } }
        };
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