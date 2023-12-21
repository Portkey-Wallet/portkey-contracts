using System.Collections.Generic;
using System.Linq;
using AElf.Contracts.MultiToken;
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
                if (State.ManagerForwardCallParallelMap[args.ContractAddress][args.MethodName])
                {
                    return new ResourceInfo { NonParallelizable = true };
                }

                var transaction = new Transaction
                {
                    From = Context.ConvertVirtualAddressToContractAddress(args.CaHash, args.ContractAddress),
                    To = args.ContractAddress,
                    MethodName = args.MethodName,
                    Params = args.Args,
                };
                var resource = Context.Call<ResourceInfo>(txn.From, args.ContractAddress, "GetResourceInfo",
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
        var symbols = State.TokenContract.GetTransactionFeeFreeAllowancesConfig.Call(new Empty());
        if (symbols != null)
        {
            foreach (var symbol in symbols.Value.Select(config => config.Symbol))
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
        var symbols = GetMethodsFeeSymbols(methodName);
        var sizeFeeSymbols = GetSizeFeeSymbols().SymbolsToPayTxSizeFee;

        foreach (var sizeFee in sizeFeeSymbols)
        {
            if (!symbols.Contains(sizeFee.TokenSymbol))
                symbols.Add(sizeFee.TokenSymbol);
        }

        return symbols;
    }

    private List<string> GetMethodsFeeSymbols(string methodName)
    {
        var symbols = new List<string>();
        var methodFees = State.TokenContract.GetMethodFee.Call(new StringValue { Value = methodName });
        if (methodFees != null)
        {
            foreach (var methodFee in methodFees.Fees)
            {
                if (!symbols.Contains(methodFee.Symbol) && methodFee.BasicFee > 0)
                    symbols.Add(methodFee.Symbol);
            }

            if (methodFees.IsSizeFeeFree)
            {
                return symbols;
            }
        }

        return symbols;
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
            });

        if (allDelegatees != null)
        {
            delegateeList.AddRange(allDelegatees.DelegateeAddresses.Select(address => address.ToBase58()));
        }

        return delegateeList;
    }


    public override Empty SetManagerForwardCallParallel(SetManagerForwardCallParallelInfoInput input)
    {
        Assert(State.ServerControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        Assert(!string.IsNullOrWhiteSpace(input.MethodName), "Invalid input.");
        State.ManagerForwardCallParallelMap[input.ContractAddress][input.MethodName] = input.IsParallel;
        return new Empty();
    }

    public override BoolValue IsManagerForwardCallParallel(GetManagerForwardCallParallelInput input)
    {
        return new BoolValue { Value = State.ManagerForwardCallParallelMap[input.ContractAddress][input.MethodName] };
    }
}