using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests : CAContractTestBase
{
    protected const string GuardianCount = "guardianCount";
    protected const string GuardianApprovedCount = "guardianApprovedCount";
    protected const string IfElse = "ifElse";

    [Fact]
    public async Task DefaultStrategyNode_Test()
    {
        var input = new ValidateStrategyInput()
        {
            StrategyNode = null,
            Variables =
            {
                [GuardianCount] = 3,
                [GuardianApprovedCount] = 3
            }
        };

        var output = await CaContractStub.ValidateStrategy.SendAsync(input);

        output.Output.BoolResult.ShouldBeTrue();
        //output.Output.StrategyOutput.Name.ShouldBeEquivalentTo(StrategyName.IfElse);
    }
}