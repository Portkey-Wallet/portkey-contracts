using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task SetContractDelegationFee_Succeed()
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
}