using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Portkey.Contracts.CA;

public partial class CAContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }

    /// <summary>
    /// Login Guardian identifier hash  -> Verifier Id -> HolderInfo Hash
    /// multiple Login Guardian to one HolderInfo Hash
    /// </summary>
    public MappedState<Hash, Hash, Hash> LoginGuardianMap { get; set; }

    /// <summary>
    /// Login Guardian identifier hash -> HolderInfo Hash
    /// multiple Login Guardian to one HolderInfo Hash
    /// </summary>
    public MappedState<Hash, Hash> GuardianMap { get; set; }

    /// <summary>
    /// HolderInfo Hash -> HolderInfo
    /// All CA contracts
    /// </summary>
    public MappedState<Hash, HolderInfo> HolderInfoMap { get; set; }

    public SingletonState<Address> Admin { get; set; }
    public SingletonState<ControllerList> CreatorControllers { get; set; }
    public SingletonState<ControllerList> ServerControllers { get; set; }

    /// <summary>
    ///  Verifier list
    /// </summary>
    public SingletonState<VerifierServerList> VerifiersServerList { get; set; }

    /// <summary>
    ///  CAServer list
    /// only on MainChain
    /// </summary>
    public SingletonState<CAServerList> CaServerList { get; set; }

    //
    public MappedState<Hash, bool> VerifierDocMap { get; set; }

    public SingletonState<long> TokenInitialTransferLimit { get; set; }
    public SingletonState<TransferSecurityThresholdList> TransferSecurityThresholdList { get; set; }
    public MappedState<Hash, string, TransferLimit> TransferLimit { get; set; }
    public MappedState<Hash, string, TransferredAmount> DailyTransferredAmountMap { get; set; }
    public MappedState<string, TransferLimit> TokenDefaultTransferLimit { get; set; }
    public MappedState<Address, string, bool> ForbiddenForwardCallContractMethod { get; set; }
    public SingletonState<ProjectDelegationFee> ProjectDelegationFee { get; set; }
    public MappedState<Hash, ProjectDelegateInfo> ProjectDelegateInfo { get; set; }
    public SingletonState<Hash> CaProjectDelegateHash { get; set; }
    public MappedState<Address, string, bool> ManagerForwardCallParallelMap { get; set; }
    public SingletonState<WhitelistTransactions> DelegateWhitelistTransactions { get; set; }
    
    /// <summary>
    /// Check if the switch for operation details exists in 'Signature',
    /// it will be removed in the next version.
    /// </summary>
    public SingletonState<bool> CheckOperationDetailsInSignatureEnabled { get; set; }
    
    /// <summary>
    /// The Guardian is added directly to the non-registered chain.
    /// identifier hash  -> HolderInfo Hash
    /// </summary>
    public MappedState<Hash, Hash> PreCrossChainSyncHolderInfoMarks { get; set; }
    
}