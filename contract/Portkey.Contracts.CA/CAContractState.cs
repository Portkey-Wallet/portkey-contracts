using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Portkey.Contracts.CA;

public partial class CAContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }

    // public SingletonState<Address> RegisterOrRecoveryController { get; set; }
    //
    // public SingletonState<Address> SetConfigController { get; set; }

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

    // public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
    // public MappedState<string, MethodFees> TransactionFees { get; set; }
    public SingletonState<ContractDelegationFee> ContractDelegationFee { get; set; }

    //
    public MappedState<Hash, bool> VerifierDocMap { get; set; }

    public SingletonState<bool> OperationTypeInSignatureEnabled { get; set; }

    public MappedState<int, Address> CAContractAddresses { get; set; }

    public SingletonState<long> TokenDefaultTransferLimit { get; set; }
    public MappedState<Hash, string, TransferLimit> TransferLimit { get; set; }
    public MappedState<Hash, string, TransferredAmount> DailyTransferredAmount { get; set; }
    public MappedState<string, long> DefaultTokenTransferLimit { get; set; }
    public MappedState<Address, string, bool> ForbiddenForwardCallContractMethod { get; set; }
}