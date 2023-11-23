using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS2;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override ResourceInfo GetResourceInfo(Transaction txn)
    {
       switch (txn.MethodName)
        {
            case nameof(CreateCAHolder):
            {
                var args = CreateCAHolderInput.Parser.ParseFrom(txn.Params);
                Hash holderId =
                    HashHelper.ConcatAndCompute(txn.GetHash(), HashHelper.ComputeFrom(Context.ChainId.ToBytes()));
                var resource = new ResourceInfo
                {
                    WritePaths =
                    {
                        GetPath(nameof(CAContractState.HolderInfoMap),holderId.ToHex()),
                        GetPath(nameof(CAContractState.GuardianMap),args.GuardianApproved.IdentifierHash.ToHex()),
                        GetPath(nameof(CAContractState.LoginGuardianMap),args.GuardianApproved.IdentifierHash.ToHex(),args.GuardianApproved.VerificationInfo.Id.ToHex()),
                        GetPath(nameof(CAContractState.VerifierDocMap),GetVerifierId(args.GuardianApproved))
                    },
                    ReadPaths =
                    {
                        GetPath(nameof(CAContractState.CreatorControllers)),
                        GetPath(nameof(CAContractState.ContractDelegationFee)),
                        GetPath(nameof(CAContractState.VerifiersServerList)),
                        GetPath(nameof(CAContractState.SecondaryDelegationFee)),
                    }
                };
                // add Delegatees path
                AddPathForDelegateesMap(resource,args.ManagerInfo.Address);
                var caAddress = Context.ConvertVirtualAddressToContractAddress(holderId);
                AddPathForDelegateesMap(resource,caAddress);
                // add fee path
                AddPathForTransactionFee(resource, txn.From.ToString(), txn.MethodName);
                AddPathForDelegatees(resource, txn.From, txn.To, txn.MethodName);
                AddPathForTransactionFeeFreeAllowance(resource, txn.From);
                return resource;
            }

            case nameof(SyncHolderInfo):
            {
                var args = SyncHolderInfoInput.Parser.ParseFrom(txn.Params);
                var originalTransaction = Transaction.Parser.ParseFrom(args.VerificationTransactionInfo.TransactionBytes);
                var transactionInput =
                    ValidateCAHolderInfoWithManagerInfosExistsInput.Parser.ParseFrom(originalTransaction.Params);
                var resource = new ResourceInfo
                {
                    WritePaths =
                    {
                        GetPath(nameof(CAContractState.HolderInfoMap),transactionInput.CaHash.ToHex()),
                        GetPath(nameof(CAContractState.HolderInfoMap),transactionInput.CaHash.ToHex()),
                    },
                    ReadPaths =
                    {
                        GetPath(nameof(CAContractState.CAContractAddresses)),
                        GetPath(nameof(CAContractState.ContractDelegationFee)),
                        GetPath(nameof(CAContractState.CheckChainIdInSignatureEnabled)),
                        GetPath(nameof(CAContractState.SecondaryDelegationFee)),
                    }
                };
                // add fee path
                AddPathForTransactionFee(resource, txn.From.ToString(), txn.MethodName);
                AddPathForDelegatees(resource, txn.From, txn.To, txn.MethodName);
                AddPathForTransactionFeeFreeAllowance(resource, txn.From);
                // add Delegators path
                var holderId = transactionInput.CaHash;
                var holderInfo = State.HolderInfoMap[holderId] ?? new HolderInfo { CreatorAddress = Context.Sender };
                var managerInfosToAdd = ManagerInfosExcept(transactionInput.ManagerInfos, holderInfo.ManagerInfos);
                var managerInfosToRemove = ManagerInfosExcept(holderInfo.ManagerInfos, transactionInput.ManagerInfos);
                foreach (var managerInfo in managerInfosToAdd)
                {
                    AddPathForDelegateesMap(resource,managerInfo.Address);
                }
                foreach (var managerInfo in managerInfosToRemove)
                {
                    AddPathForDelegateesMap(resource,managerInfo.Address);
                }
                AddPathForUpdateSecondaryDelegatee(resource,holderId, holderInfo);
                addPathForGuardianMap(transactionInput, holderId, resource);
                return resource;
            }
            
            case nameof(SocialRecovery):
            {
                var args = SocialRecoveryInput.Parser.ParseFrom(txn.Params);
                var resource = new ResourceInfo
                {
                    WritePaths =
                    {
                        GetPath(nameof(CAContractState.HolderInfoMap),State.GuardianMap[args.LoginGuardianIdentifierHash].ToHex()),
                    },
                    ReadPaths =
                    {
                        GetPath(nameof(CAContractState.GuardianMap),args.LoginGuardianIdentifierHash.ToHex()),
                        GetPath(nameof(CAContractState.ContractDelegationFee)),
                        GetPath(nameof(CAContractState.VerifiersServerList)),
                        GetPath(nameof(CAContractState.SecondaryDelegationFee))
                    }
                };
                foreach (var guardian in args.GuardiansApproved)
                {
                    resource.WritePaths.Add(  GetPath(nameof(CAContractState.VerifierDocMap),GetVerifierId(guardian)));
                }
                // add fee path
                AddPathForTransactionFee(resource, txn.From.ToString(), txn.MethodName);
                AddPathForDelegatees(resource, txn.From, txn.To, txn.MethodName);
                AddPathForTransactionFeeFreeAllowance(resource, txn.From);
                // add Delegators path
                var holderId = State.GuardianMap[args.LoginGuardianIdentifierHash];
                var holderInfo = State.HolderInfoMap[holderId];
                AddPathForUpdateSecondaryDelegatee(resource,holderId, holderInfo);
                AddPathForDelegateesMap(resource,args.ManagerInfo.Address);
                return resource;
            }

            default:
                return new ResourceInfo { NonParallelizable = true };
        }
    }

    private void addPathForGuardianMap(ValidateCAHolderInfoWithManagerInfosExistsInput transactionInput, Hash holderId,
        ResourceInfo resource)
    {
        if (transactionInput.NotLoginGuardians != null)
        {
            foreach (var notLoginGuardian in transactionInput.NotLoginGuardians)
            {
                if (State.GuardianMap[notLoginGuardian] == holderId)
                {
                    resource.WritePaths.Add(GetPath(nameof(CAContractState.GuardianMap), notLoginGuardian.ToHex()));
                }
            }
        }
    }

    private void AddPathForUpdateSecondaryDelegatee( ResourceInfo resource, Hash holderId,HolderInfo holderInfo)
    {
        var caAddress = Context.ConvertVirtualAddressToContractAddress(holderId);
        if (IfCaHasSecondaryDelegatee(caAddress)) return ;
        foreach (var managerInfo in holderInfo.ManagerInfos)
        {
            AddPathForDelegateesMap(resource, managerInfo.Address);
        }
        AddPathForDelegateesMap(resource, caAddress);
    }

    private string GetVerifierId(GuardianInfo guardianInfo)
    {
        return HashHelper.ComputeFrom(guardianInfo.VerificationInfo.Signature.ToByteArray()).ToHex();
    }

    private ScopedStatePath GetPath(Address address,params string[] parts)
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
    private ScopedStatePath GetPath(params string[] parts)
    {
        return GetPath(Context.Self,parts);
    }
    
    private void AddPathForTransactionFee(ResourceInfo resourceInfo, string from, string methodName)
    {
        var symbols = GetTransactionFeeSymbols(methodName);
        var primaryTokenSymbol = State.TokenContract.GetPrimaryTokenSymbol.Call(new Empty()).Value;
        if (!symbols.Contains(primaryTokenSymbol))
            symbols.Add(primaryTokenSymbol);
        var paths = symbols.Select(symbol => GetPath(State.TokenContract.Value,"Balances", from, symbol));
        foreach (var path in paths)
        {
            if (resourceInfo.WritePaths.Contains(path)) continue;
            resourceInfo.WritePaths.Add(path);
        }
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
        var methodFees = State.TokenContractImpl.GetMethodFee.Call(new StringValue{Value = methodName});
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

    private List<string> GetDelegateeList(Address delegator, Address to, string methodName)
    {
        var delegateeList = new List<string>();
        var allDelegatees = State.TokenContractImpl.GetTransactionFeeDelegateeList.Call(
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

    private void AddPathForTransactionFeeFreeAllowance(ResourceInfo resourceInfo, Address from)
    {
        var symbols = State.TokenContractImpl.GetTransactionFeeFreeAllowancesConfig.Call(new Empty());
        if (symbols != null)
        {
            foreach (var symbol in symbols.Value.Select(config=>config.Symbol))
            {
                resourceInfo.WritePaths.Add(GetPath(State.TokenContract.Value,"TransactionFeeFreeAllowances",
                    from.ToBase58(), symbol));
                resourceInfo.WritePaths.Add(GetPath(State.TokenContract.Value,
                    "TransactionFeeFreeAllowancesLastRefreshTimes", from.ToBase58(), symbol));

                var path = GetPath(State.TokenContract.Value,"TransactionFeeFreeAllowancesConfigMap", symbol);
                if (!resourceInfo.ReadPaths.Contains(path))
                {
                    resourceInfo.ReadPaths.Add(path);
                }
            }
        }
    }
    //SetDelegator
    private void AddPathForDelegateesMap(ResourceInfo resourceInfo, Address delegatorAddress)
    {
        resourceInfo.WritePaths.Add(GetPath(State.TokenContract.Value,"TransactionFeeDelegateesMap",
            delegatorAddress.ToBase58()));
    }

}