using System.Linq;
using System.Threading.Tasks;
using AElf.Standards.ACS1;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task SetMethodFeeTest()
    {
        await CreateHolder();
        await CaContractStub.SetMethodFee.SendAsync(new MethodFees
        {
            MethodName = "test",
            Fees =
            {
                new MethodFee
                {
                    Symbol = "ELF",
                    BasicFee = 100
                }
            },
            IsSizeFeeFree = true
        });

        var result = await CaContractStub.GetMethodFee.CallAsync(new StringValue
        {
            Value = "test"
        });
        
        result.Fees.Count.ShouldBe(1);
        result.Fees.First().Symbol.ShouldBe("ELF");
        result.Fees.First().BasicFee.ShouldBe(100);
        result.IsSizeFeeFree.ShouldBe(true);
    }
    
    [Fact]
    public async Task SetMethodFeeTest_Fail_InvalidToken()
    {
        var result = await CaContractStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
        {
            MethodName = "test",
            Fees =
            {
                new MethodFee
                {
                    Symbol = "TEST",
                    BasicFee = 100
                }
            },
            IsSizeFeeFree = true
        });
        
        result.TransactionResult.Error.ShouldContain("Token is not found.");
        
        result = await CaContractStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
        {
            MethodName = "test",
            Fees =
            {
                new MethodFee
                {
                    Symbol = "ELF",
                    BasicFee = -1
                }
            },
            IsSizeFeeFree = true
        });
        
        result.TransactionResult.Error.ShouldContain("Invalid amount.");
    }
    
    [Fact]
    public async Task SetMethodFeeTest_Fail_NotPermission()
    {
        await CreateHolder();
        var result = await CaContractUser1Stub.SetMethodFee.SendWithExceptionAsync(new MethodFees
        {
            MethodName = "test",
            Fees =
            {
                new MethodFee
                {
                    Symbol = "TEST",
                    BasicFee = 100
                }
            },
            IsSizeFeeFree = true
        });
        
        result.TransactionResult.Error.ShouldContain("Token is not found.");
    }
    
    [Fact]
    public async Task ChangeMethodFeeControllerTest()
    {
        await CreateHolder();
        var result = await CaContractUser1Stub.SetMethodFee.SendWithExceptionAsync(new MethodFees
        {
            MethodName = "test",
            Fees =
            {
                new MethodFee
                {
                    Symbol = "ELF",
                    BasicFee = 100
                }
            },
            IsSizeFeeFree = true
        });

        result.TransactionResult.Error.ShouldContain("Unauthorized to set method fee.");

        await CaContractStub.ChangeMethodFeeController.SendAsync(new AuthorityInfo
        {
            ContractAddress = CaContractAddress,
            OwnerAddress = User1Address
        });

        var res = await CaContractUser1Stub.GetMethodFeeController.CallAsync(new Empty());
        res.OwnerAddress.ShouldBe(User1Address);
    }

    [Fact]
    public async Task ChangeMethodFeeControllerTest_Fail_InvalidInput()
    {
        await CreateHolder();
        var result = await CaContractStub.ChangeMethodFeeController.SendWithExceptionAsync(new AuthorityInfo());
        result.TransactionResult.Error.ShouldContain("Invalid OwnerAddress.");
    }
    
    [Fact]
    public async Task ChangeMethodFeeControllerTest_Fail_NoPermission()
    {
        await CreateHolder();
        var result = await CaContractUser1Stub.ChangeMethodFeeController.SendWithExceptionAsync(new AuthorityInfo
        {
            OwnerAddress = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Unauthorized behavior.");
    }
}