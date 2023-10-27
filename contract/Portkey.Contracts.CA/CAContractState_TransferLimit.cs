using AElf.Sdk.CSharp.State;
using AElf.Types;
using Google.Protobuf.Collections;

namespace Portkey.Contracts.CA;

public partial class CAContractState : ContractState
{
    public SingletonState<long> TokenInitialTransferLimit { get; set; }
    public SingletonState<TransferSecurityThresholdList> TransferSecurityThresholdList { get; set; }
    public MappedState<Hash, string, TransferLimit> TransferLimit { get; set; }
    public MappedState<Hash, string, TransferredAmount> DailyTransferredAmountMap { get; set; }
    public MappedState<string, TransferLimit> TokenDefaultTransferLimit { get; set; }
    public MappedState<Address, string, bool> ForbiddenForwardCallContractMethod { get; set; }
    public SingletonState<bool> CheckChainIdInSignatureEnabled { get; set; }
}