using AElf.Sdk.CSharp.State;
using CaInternal;
using Google.Protobuf.WellKnownTypes;
using ZkVerifier;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    public MappedState<string, ZkIssuerPublicKeyList> ZkIssuerMap { get; set; }
    public SingletonState<ZkIssuerList> ZkIssuerList { get; set; }
    public SingletonState<StringValue> ZkVerifiyingKey { get; set; }
}