using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Standards.ACS12;
using AElf.Standards.ACS2;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override ResourceInfo GetResourceInfo(Transaction txn)
    {
        switch (txn.MethodName)
        {
            case nameof(ManagerForwardCall):
            {
                var args = ManagerForwardCallInput.Parser.ParseFrom(txn.Params);
                if (!State.ManagerForwardCallParallelMap[args.ContractAddress][args.MethodName])
                {
                    return new ResourceInfo { NonParallelizable = true };
                }

                var transaction = new Transaction
                {
                    From = Context.ConvertVirtualAddressToContractAddress(args.CaHash, Context.Self),
                    To = args.ContractAddress,
                    MethodName = args.MethodName,
                    Params = args.Args,
                };
                var resource = Context.Call<ResourceInfo>(txn.From, args.ContractAddress, nameof(GetResourceInfo),
                    transaction.ToByteString());
                // add fee path
                AddPathForTransactionFee(resource, txn.From.ToBase58(), txn.MethodName);
                AddPathForTransactionFeeFreeAllowance(resource, txn.From);
                AddPathForDelegatees(resource, txn.From, txn.To, txn.MethodName);
                return resource;
            }
            default:
                return new ResourceInfo { NonParallelizable = true };
        }
    }

    private void AddPathForDelegatees(ResourceInfo resourceInfo, Address from, Address to, string methodName)
    {
        var delegateeList = new List<string>();
        //get and add first-level delegatee list
        delegateeList.AddRange(GetDelegateeList(from, to, methodName));
        if (delegateeList.Count <= 0) return;
        var secondDelegateeList = new List<string>();
        //get and add second-level delegatee list
        foreach (var delegateeAddress in delegateeList.Select(Address.FromBase58))
        {
            //delegatee of the first-level delegate is delegator of the second-level delegate
            secondDelegateeList.AddRange(GetDelegateeList(delegateeAddress, to, methodName));
        }

        delegateeList.AddRange(secondDelegateeList);
        foreach (var delegatee in delegateeList.Distinct())
        {
            AddPathForTransactionFee(resourceInfo, delegatee, methodName);
            AddPathForTransactionFeeFreeAllowance(resourceInfo, Address.FromBase58(delegatee));
        }
    }

    private void AddPathForTransactionFee(ResourceInfo resourceInfo, string from, string methodName)
    {
        var symbols = GetTransactionFeeSymbols(methodName);
        var primaryTokenSymbol = State.TokenContract.GetPrimaryTokenSymbol.Call(new Empty()).Value;
        if (!symbols.Contains(primaryTokenSymbol))
            symbols.Add(primaryTokenSymbol);
        var paths = symbols.Select(symbol => GetPath(State.TokenContract.Value, "Balances", from, symbol));
        foreach (var path in paths)
        {
            if (resourceInfo.WritePaths.Contains(path)) continue;
            resourceInfo.WritePaths.Add(path);
        }
    }

    private void AddPathForTransactionFeeFreeAllowance(ResourceInfo resourceInfo, Address from)
    {
        var getTransactionFeeFreeAllowancesConfigOutput =
            State.TokenContract.GetTransactionFeeFreeAllowancesConfig.Call(new Empty());
        if (getTransactionFeeFreeAllowancesConfigOutput != null)
        {
            foreach (var symbol in getTransactionFeeFreeAllowancesConfigOutput.Value.Select(config => config.Symbol))
            {
                resourceInfo.WritePaths.Add(GetPath(State.TokenContract.Value, "TransactionFeeFreeAllowances",
                    from.ToBase58(), symbol));
                resourceInfo.WritePaths.Add(GetPath(State.TokenContract.Value,
                    "TransactionFeeFreeAllowancesLastRefreshTimes", from.ToBase58(), symbol));

                var path = GetPath(State.TokenContract.Value, "TransactionFeeFreeAllowancesConfigMap", symbol);
                if (!resourceInfo.ReadPaths.Contains(path))
                {
                    resourceInfo.ReadPaths.Add(path);
                }
            }
        }
    }

    private ScopedStatePath GetPath(Address address, params string[] parts)
    {
        return new ScopedStatePath
        {
            Address = address,
            Path = new StatePath
            {
                Parts =
                {
                    parts
                }
            }
        };
    }

    private List<string> GetTransactionFeeSymbols(string methodName)
    {
        var actualFee = GetActualFee(methodName);
        var symbols = new List<string>();
        if (actualFee.Fees != null)
        {
            symbols = actualFee.Fees.Select(fee => fee.Symbol).Distinct().ToList();
        }

        if (!actualFee.IsSizeFeeFree)
        {
            var sizeFeeSymbols = GetSizeFeeSymbols().SymbolsToPayTxSizeFee;

            foreach (var sizeFee in sizeFeeSymbols)
            {
                if (!symbols.Contains(sizeFee.TokenSymbol))
                    symbols.Add(sizeFee.TokenSymbol);
            }
        }
        return symbols;
    }


    private UserContractMethodFees GetActualFee(string methodName)
    {
        var UserContractMethodFeeKey = "UserContractMethodFee";
        //configuration_key:UserContractMethod_contractAddress_methodName
        var spec = State.ConfigurationContract.GetConfiguration.Call(new StringValue
        {
            Value = $"{UserContractMethodFeeKey}_{Context.Self}_{methodName}"
        });
        var fee = new UserContractMethodFees();
        if (!spec.Value.IsNullOrEmpty())
        {
            fee.MergeFrom(spec.Value);
            return fee;
        }

        //If special key is null,get the normal fee set by the configuration contract.
        //configuration_key:UserContractMethod
        var value = State.ConfigurationContract.GetConfiguration.Call(new StringValue
        {
            Value = UserContractMethodFeeKey
        });
        if (value.Value.IsNullOrEmpty())
        {
            return new UserContractMethodFees();
        }

        fee.MergeFrom(value.Value);
        return fee;
    }

    private SymbolListToPayTxSizeFee GetSizeFeeSymbols()
    {
        var symbolListToPayTxSizeFee = State.TokenContract.GetSymbolsToPayTxSizeFee.Call(new Empty());
        return symbolListToPayTxSizeFee;
    }


    private List<string> GetDelegateeList(Address delegator, Address to, string methodName)
    {
        var delegateeList = new List<string>();
        var allDelegatees = State.TokenContract.GetTransactionFeeDelegateeList.Call(
            new GetTransactionFeeDelegateeListInput
            {
                DelegatorAddress = delegator,
                ContractAddress = to,
                MethodName = methodName
            }).DelegateeAddresses;

        if (allDelegatees == null || allDelegatees.Count == 0)
        {
            allDelegatees = State.TokenContract.GetTransactionFeeDelegatees.Call(new GetTransactionFeeDelegateesInput
            {
                DelegatorAddress = delegator
            }).DelegateeAddresses;
        }
        if (allDelegatees != null)
        {
            delegateeList.AddRange(allDelegatees.Select(address => address.ToBase58()));
        }

        return delegateeList;
    }


    public override Empty SetManagerForwardCallParallelInfo(SetManagerForwardCallParallelInfoInput input)
    {
        Assert(State.Admin.Value.Equals(Context.Sender), "No permission.");
        Assert(!string.IsNullOrWhiteSpace(input.MethodName), "Invalid input.");
        State.ManagerForwardCallParallelMap[input.ContractAddress][input.MethodName] = input.IsParallel;
        return new Empty();
    }

    public override GetManagerForwardCallParallelInfoOutput GetManagerForwardCallParallelInfo(
        GetManagerForwardCallParallelInput input)
    {
        return new GetManagerForwardCallParallelInfoOutput
        {
            ContractAddress = input.ContractAddress,
            MethodName = input.MethodName,
            IsParallel = State.ManagerForwardCallParallelMap[input.ContractAddress][input.MethodName]
        };
    }
}