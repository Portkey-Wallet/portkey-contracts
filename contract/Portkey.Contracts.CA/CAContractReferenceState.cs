using AElf.Contracts.Genesis;
using AElf.Contracts.MultiToken;
using Org.BouncyCastle.Asn1.X509;

namespace Portkey.Contracts.CA;

public partial class CAContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal BasicContractZeroContainer.BasicContractZeroReferenceState ZeroContract { get; set; }
    
    
}