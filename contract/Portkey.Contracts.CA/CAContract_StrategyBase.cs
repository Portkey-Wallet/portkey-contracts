using System;
using AElf.Sdk.CSharp;
using Google.Protobuf;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public abstract class Strategy
    {
        public abstract StrategyName StrategyName { get; }
        public abstract object Validate(IStrategyContext context);
        public abstract Strategy Parse(StrategyNode node);
        public abstract StrategyNode ToStrategyNode();

        protected void Assert(bool asserted, string message = "Assertion failed!")
        {
            if (!asserted) throw new AssertionException(message);
        }

        protected StrategyNode ByteStringToNode(ByteString byteString)
        {
            StrategyNode strategyNode = new StrategyNode();
            strategyNode.MergeFrom(byteString);
            return strategyNode;
        }

        public static StrategyNode DefaultStrategy()
        {
            return new IfElseStrategy()
            {
                IfCondition = new NotLargerThanStrategy()
                {
                    One = CAContractConstants.GuardianCount,
                    Two = 3
                },
                Than = new NotLessThanStrategy()
                {
                    One = CAContractConstants.GuardianApprovedCount,
                    Two = CAContractConstants.GuardianCount
                },
                Else = new NotLessThanStrategy()
                {
                    One = CAContractConstants.GuardianApprovedCount,
                    Two = new RatioOfCountCalculationStrategy()
                    {
                        One = CAContractConstants.GuardianCount,
                        Two = 6000
                    }
                }
            }.ToStrategyNode();
        }
    }

    public abstract class UnaryBooleanStrategy : Strategy
    {
        public Strategy One { get; set; }

        public override Strategy Parse(StrategyNode node)
        {
            One = StrategyFactory.Create(ByteStringToNode(node.Value[0]));
            return this;
        }

        public override StrategyNode ToStrategyNode()
        {
            return new StrategyNode()
            {
                Name = StrategyName,
                Type = { StrategyValueType.Strategy },
                Value = { One.ToStrategyNode().ToByteString() }
            };
        }
    }

    public abstract class BinaryBooleanStrategy : Strategy
    {
        public Strategy One { get; set; }
        public Strategy Two { get; set; }

        public override Strategy Parse(StrategyNode node)
        {
            One = StrategyFactory.Create(ByteStringToNode(node.Value[0]));
            Two = StrategyFactory.Create(ByteStringToNode(node.Value[1]));
            return this;
        }

        public override StrategyNode ToStrategyNode()
        {
            return new StrategyNode()
            {
                Name = StrategyName,
                Type = { StrategyValueType.Strategy, StrategyValueType.Strategy },
                Value =
                {
                    One.ToStrategyNode().ToByteString(),
                    Two.ToStrategyNode().ToByteString()
                }
            };
        }
    }

    public abstract class BinaryNumericStrategy : Strategy
    {
        // parameters can be a long, a string for variable, or an instance of NumericStrategy for more calculation.
        public object One { get; set; }
        public object Two { get; set; }

        public override Strategy Parse(StrategyNode node)
        {
            One = StrategyValueFactory.Create(node.Type[0], node.Value[0]);
            Two = StrategyValueFactory.Create(node.Type[1], node.Value[1]);
            return this;
        }

        public override StrategyNode ToStrategyNode()
        {
            ByteString byteString0 = MemberToByteString(One, out StrategyValueType type0);
            ByteString byteString1 = MemberToByteString(Two, out StrategyValueType type1);

            return new StrategyNode()
            {
                Name = StrategyName,
                Type = { type0, type1 },
                Value = { byteString0, byteString1 }
            };
        }

        protected ByteString MemberToByteString(object obj, out StrategyValueType type)
        {
            type = StrategyValueType.Strategy;
            // if (obj is long or int or string or BinaryNumericStrategy)
            if (obj is long or int)
            {
                type = StrategyValueType.Long;
                long value = 0;
                if (obj is int)
                {
                    value = (int)obj;
                }
                else
                {
                    value = (long)obj;
                }

                return new StrategyLongWrapper()
                {
                    Value = value
                }.ToByteString();
            }
            else if (obj is string)
            {
                type = StrategyValueType.Variable;
                return new StrategyStringWrapper()
                {
                    Value = (string)obj
                }.ToByteString();
            }
            else if (obj is BinaryNumericStrategy)
            {
                return ((BinaryNumericStrategy)obj).ToStrategyNode().ToByteString();
            }

            Assert(false, "obj should be one of a long, a string, or an instance of NumericStrategy.");
            return null; // untouchable line, just for get rid of red underline masked by IDE.
        }

        protected object ValidateStrategyMember(object obj, IStrategyContext context)
        {
            if (obj is long)
            {
                return obj;
            }
            else if (obj is int)
            {
                long value = (int)obj;
                return value;
            }
            else if (obj is string)
            {
                return context.AssignVariableAndToLong(obj);
            }
            else if (obj is BinaryNumericStrategy)
            {
                return ((BinaryNumericStrategy)obj).Validate(context);
            }

            Assert(false, "obj should be one of a long, a string, or an instance of NumericStrategy.");
            return null; // untouchable line, just for get rid of red underline masked by IDE.
        }
    }

    public abstract class BinaryNumericCompareStrategy : BinaryNumericStrategy
    {
        protected abstract Func<long, long, bool> Compare { get; }

        public override object Validate(IStrategyContext context)
        {
            return Compare((long)ValidateStrategyMember(One, context),
                (long)ValidateStrategyMember(Two, context));
        }
    }

    public abstract class BinaryNumericCalculateStrategy : BinaryNumericStrategy
    {
        protected abstract Func<long, long, long> Calculate { get; }

        public override object Validate(IStrategyContext context)
        {
            return Calculate((long)ValidateStrategyMember(One, context),
                (long)ValidateStrategyMember(Two, context));
        }
    }
}