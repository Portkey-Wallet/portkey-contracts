using AElf.Contracts.Configuration;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;
using AetherLink.Contracts.Oracle;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    internal TokenContractImplContainer.TokenContractImplReferenceState TokenContract { get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal ConfigurationContainer.ConfigurationReferenceState ConfigurationContract { get; set; }
    
    internal OracleContractContainer.OracleContractReferenceState OracleContract { get; set; }
}