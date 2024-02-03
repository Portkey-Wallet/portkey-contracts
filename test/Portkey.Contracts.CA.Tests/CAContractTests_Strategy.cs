using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
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

    [Fact]
    public async Task StrategyHelper_Test()
    {
        var notStrategy = new CAContract.NotStrategy();
        notStrategy.Parse(CAContract.Strategy.DefaultStrategy());

        var addStrategy = new CAContract.AddStrategy
        {
            One = new CAContract.NotLargerThanStrategy()
            {
                One = CAContractConstants.GuardianCount,
                Two = 3
            },
            Two = new CAContract.NotLessThanStrategy()
            {
                One = CAContractConstants.GuardianApprovedCount,
                Two = 2
            }
        };
        var node = addStrategy.ToStrategyNode();
        var strategy = CAContract.StrategyFactory.Create(node);
        bool res = (bool)strategy.Validate(new CAContract.StrategyContext()
        {
            Variables = new Dictionary<string, long>
            {
                {"guardianCount", 3},
                {"guardianApprovedCount", 3}
            }
        });
        res.ShouldBeTrue();
        
        res = (bool)strategy.Validate(new CAContract.StrategyContext()
        {
            Variables = new Dictionary<string, long>
            {
                {"guardianCount", 3},
                {"guardianApprovedCount", 1}
            }
        });
        res.ShouldBeFalse();
        
        var orStrategy = new CAContract.OrStrategy()
        {
            One = new CAContract.NotLargerThanStrategy()
            {
                One = CAContractConstants.GuardianCount,
                Two = 3
            },
            Two = new CAContract.NotLessThanStrategy()
            {
                One = CAContractConstants.GuardianApprovedCount,
                Two = 3
            }
        };
        node = orStrategy.ToStrategyNode();
        strategy = CAContract.StrategyFactory.Create(node);
        res = (bool)strategy.Validate(new CAContract.StrategyContext()
        {
            Variables = new Dictionary<string, long>
            {
                {"guardianCount", 4},
                {"guardianApprovedCount", 1}
            }
        });
        res.ShouldBeFalse();
        res = (bool)strategy.Validate(new CAContract.StrategyContext()
        {
            Variables = new Dictionary<string, long>
            {
                {"guardianCount", 4},
                {"guardianApprovedCount", 3}
            }
        });
        res.ShouldBeTrue();
        
    }

    private async Task ValidateStrategyShouldBe(bool expected, ValidateStrategyInput input)
    {
        var output = await CaContractStub.ValidateStrategy.SendAsync(input);
        output.Output.BoolResult.ShouldBe(expected);
    }
    
    
}