using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task SetContractDelegationFeeTest()
    {
        await CreateHolder();
        var contractDelegationFee = await CaContractStub.GetContractDelegationFee.CallAsync(new Empty());
        contractDelegationFee.DelegationFee.Amount.ShouldBe(10000000000);
        await CaContractStub.SetContractDelegationFee.SendAsync(new SetContractDelegationFeeInput
        {
            DelegationFee = new ContractDelegationFee
            {
                Amount = 20000000000
            }
        });
        contractDelegationFee = await CaContractStub.GetContractDelegationFee.CallAsync(new Empty());
        contractDelegationFee.DelegationFee.Amount.ShouldBe(20000000000);
    }
    
    [Fact]
    public async Task SetContractDelegationFeeTest_Fail_InvalidInput()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.SetContractDelegationFee.SendAsync(new SetContractDelegationFeeInput
        {
            DelegationFee = new ContractDelegationFee
            {
                Amount = 10000000000
            }
        });
        
        var result = await CaContractStub.SetContractDelegationFee.SendWithExceptionAsync(new SetContractDelegationFeeInput());
        result.TransactionResult.Error.ShouldContain("invalid input");
    }
}