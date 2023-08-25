using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public class AddStrategy : BinaryBooleanStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.And;
        }

        public override object Validate(IStrategyContext context)
        {
            return (bool)One.Validate(context) && (bool)Two.Validate(context);
        }
    }

    public class OrStrategy : BinaryBooleanStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.Or;
        }

        public override object Validate(IStrategyContext context)
        {
            return (bool)One.Validate(context) || (bool)Two.Validate(context);
        }
    }

    public class NotStrategy : UnaryBooleanStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.Not;
        }

        public override object Validate(IStrategyContext context)
        {
            return !(bool)One.Validate(context);
        }
    }

    public class IfElseStrategy : Strategy
    {
        public Strategy IfCondition { get; set; }
        public Strategy Than { get; set; }
        public Strategy Else { get; set; }

        public override StrategyName StrategyName
        {
            get => StrategyName.IfElse;
        }

        public override object Validate(IStrategyContext context)
        {
            return ((bool)IfCondition.Validate(context)) ? (bool)Than.Validate(context) : (bool)Else.Validate(context);
        }

        public override Strategy Parse(StrategyNode node)
        {
            IfCondition = StrategyFactory.Create(ByteStringToNode(node.Value[0]));
            Than = StrategyFactory.Create(ByteStringToNode(node.Value[1]));
            Else = StrategyFactory.Create(ByteStringToNode(node.Value[2]));
            return this;
        }

        public override StrategyNode ToStrategyNode()
        {
            return new StrategyNode()
            {
                Name = StrategyName,
                Type = { StrategyValueType.Strategy, StrategyValueType.Strategy, StrategyValueType.Strategy },
                Value =
                {
                    IfCondition.ToStrategyNode().ToByteString(),
                    Than.ToStrategyNode().ToByteString(),
                    Else.ToStrategyNode().ToByteString()
                }
            };
        }
    }

    public class LargerThanStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.LargerThan;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one > two;
        }
    }

    public class NotLargerThanStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.NotLargerThan;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one <= two;
        }
    }

    public class LessThanStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.LessThan;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one < two;
        }
    }

    public class NotLessThanStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.NotLessThan;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one >= two;
        }
    }

    public class EqualStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.Equal;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one == two;
        }
    }

    public class NotEqualStrategy : BinaryNumericCompareStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.NotEqual;
        }

        protected override Func<long, long, bool> Compare
        {
            get => (one, two) => one != two;
        }
    }

    public class RatioOfCountCalculationStrategy : BinaryNumericCalculateStrategy
    {
        public override StrategyName StrategyName
        {
            get => StrategyName.RatioByTenThousand;
        }

        protected override Func<long, long, long> Calculate
        {
            get => (one, two) => one * two / CAContractConstants.TenThousand + 1;
        }
    }

    public override ValidateStrategyOutput ValidateStrategy(ValidateStrategyInput input)
    {
        Assert(input != null, "input cannot be null!");
        var context = new StrategyContext()
        {
            Variables = new Dictionary<string, long>()
        };
        if (input.Variables != null)
        {
            foreach (var (variableName, value) in input.Variables)
            {
                context.Variables.Add(variableName, value);
            }
        }

        var output = new ValidateStrategyOutput();
        var strategyNode = input.StrategyNode ?? Strategy.DefaultStrategy();

        var strategy = StrategyFactory.Create(strategyNode);
        // var strategy = Strategy.DefaultStrategy();
        if (strategy is BinaryNumericCalculateStrategy)
        {
            output.Int64Result = (long)strategy.Validate(context);
        }
        else
        {
            output.BoolResult = (bool)strategy.Validate(context);
        }

        // output.StrategyOutput = strategy.ToStrategyNode();
        output.StrategyOutput = strategyNode;

        return output;
    }
}