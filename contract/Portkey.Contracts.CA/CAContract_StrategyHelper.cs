using System.Collections.Generic;
using AElf.Sdk.CSharp;
using Google.Protobuf;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public class StrategyFactory
    {
        public static Strategy Create(StrategyNode node)
        {
            switch (node.Name)
            {
                case StrategyName.And:
                    return new AddStrategy().Parse(node);
                case StrategyName.Or:
                    return new OrStrategy().Parse(node);
                case StrategyName.Not:
                    return new NotStrategy().Parse(node);
                case StrategyName.IfElse:
                    return new IfElseStrategy().Parse(node);
                case StrategyName.LargerThan:
                    return new LargerThanStrategy().Parse(node);
                case StrategyName.NotLargerThan:
                    return new NotLargerThanStrategy().Parse(node);
                case StrategyName.LessThan:
                    return new LessThanStrategy().Parse(node);
                case StrategyName.NotLessThan:
                    return new NotLessThanStrategy().Parse(node);
                case StrategyName.Equal:
                    return new EqualStrategy().Parse(node);
                case StrategyName.NotEqual:
                    return new NotEqualStrategy().Parse(node);
                case StrategyName.RatioByTenThousand:
                    return new RatioOfCountCalculationStrategy().Parse(node);
                default:
                    return null;
            }
        }
    }

    public class StrategyValueFactory
    {
        public static object Create(StrategyValueType type, ByteString byteString)
        {
            switch (type)
            {
                case StrategyValueType.Long:
                    StrategyLongWrapper longWrapper = new StrategyLongWrapper();
                    longWrapper.MergeFrom(byteString);
                    return longWrapper.Value;
                case StrategyValueType.Variable:
                    StrategyStringWrapper stringWrapper = new StrategyStringWrapper();
                    stringWrapper.MergeFrom(byteString);
                    return stringWrapper.Value;
                case StrategyValueType.Strategy:
                    StrategyNode strategyNode = new StrategyNode();
                    strategyNode.MergeFrom(byteString);
                    return StrategyFactory.Create(strategyNode);
                default:
                    return null;
            }
        }
    }

    public interface IStrategyContext
    {
        public long AssignVariableAndToLong(object obj);
    }


    public class StrategyContext : IStrategyContext
    {
        public Dictionary<string, long> Variables { get; set; }

        private bool TryAssignVariable(string variableName, ref long value)
        {
            return Variables.TryGetValue(variableName, out value);
        }

        private bool TryParse(string valueString, ref long value)
        {
            return long.TryParse(valueString, out value);
        }

        public long AssignVariableAndToLong(object obj)
        {
            long value = 0;
            if (obj is int || obj is long)
            {
                return (long)obj;
            }
            else if (obj is string)
            {
                string str = (string)obj;
                if (TryAssignVariable(str, ref value))
                {
                    return value;
                }

                if (TryParse(str, ref value))
                {
                    return value;
                }
            }

            Assert(false, "A string here should be a variable name or a numeric string");
            return 0; // untouchable line, just for get rid of red underline masked by IDE.
        }

        protected void Assert(bool asserted, string message = "Assertion failed!")
        {
            if (!asserted) throw new AssertionException(message);
        }
    }
}