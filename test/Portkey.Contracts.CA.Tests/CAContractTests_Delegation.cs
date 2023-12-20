using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task SetSecondaryDelegationFeeTest()
    {
        await CreateHolder();
        var secondaryDelegationFee = await CaContractStub.GetSecondaryDelegationFee.CallAsync(new Empty());
        secondaryDelegationFee.Amount.ShouldBe(10000000000);
        await CaContractStub.SetSecondaryDelegationFee.SendAsync(new SetSecondaryDelegationFeeInput
        {
            DelegationFee = new SecondaryDelegationFee
            {
                Amount = 20000000000
            }
        });
        secondaryDelegationFee = await CaContractStub.GetSecondaryDelegationFee.CallAsync(new Empty());
        secondaryDelegationFee.Amount.ShouldBe(20000000000);
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
}