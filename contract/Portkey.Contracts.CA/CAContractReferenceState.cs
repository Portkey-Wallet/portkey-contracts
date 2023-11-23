using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal TokenContractImplContainer.TokenContractImplReferenceState TokenContractImpl{ get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    
    
}