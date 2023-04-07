using AElf.Contracts.MultiToken;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
}