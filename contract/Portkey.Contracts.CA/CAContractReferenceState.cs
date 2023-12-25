using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    internal TokenContractImplContainer.TokenContractImplReferenceState TokenContract { get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    
    
}