using System.IO;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Genesis;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Org.BouncyCastle.Asn1.X509;
using Volo.Abp.Threading;

namespace Portkey.Contracts.CA;

public class CAContractTestBase : DAppContractTestBase<CAContractTestModule>
{
    //internal ParliamentContractImplContainer.ParliamentContractImplStub ParliamentContractStub;
    internal CAContractContainer.CAContractStub CaContractStub { get; set; }
    internal CAContractContainer.CAContractStub CaContractStubManagerInfo1 { get; set; }
    
    internal CAContractContainer.CAContractStub CaContractUser1Stub { get; set; }
    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
    
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    
    
    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;
    protected ECKeyPair User1KeyPair => Accounts[1].KeyPair;
    protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
    protected ECKeyPair User3KeyPair => Accounts[3].KeyPair;
    protected ECKeyPair VerifierKeyPair => Accounts[4].KeyPair;
    protected ECKeyPair VerifierKeyPair1 => Accounts[5].KeyPair;
    protected ECKeyPair VerifierKeyPair2 => Accounts[6].KeyPair;
    protected ECKeyPair VerifierKeyPair3 => Accounts[7].KeyPair;
    protected ECKeyPair VerifierKeyPair4 => Accounts[8].KeyPair;
    protected Address User1Address => Accounts[1].Address;
    protected Address User2Address => Accounts[2].Address;
    protected Address User3Address => Accounts[3].Address;
    protected Address VerifierAddress => Accounts[4].Address;
    protected Address VerifierAddress1  => Accounts[5].Address;
    protected Address VerifierAddress2 => Accounts[6].Address;
    protected Address VerifierAddress3 => Accounts[7].Address;
    protected Address VerifierAddress4 => Accounts[8].Address;
    
    protected Address CaContractAddress { get; set; }

    protected CAContractTestBase()
    {
        TokenContractStub = GetTokenContractTester(DefaultKeyPair);
        ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
        var result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(CAContract).Assembly.Location))
        }));

        CaContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        CaContractStub = GetCaContractTester(DefaultKeyPair);
        CaContractStubManagerInfo1 = GetCaContractTester(User1KeyPair);
        CaContractUser1Stub = GetCaContractTester(User1KeyPair);
        //ParliamentContractStub = GetParliamentContractTester(DefaultKeyPair);
        
    }

    
    internal CAContractContainer.CAContractStub GetCaContractTester(ECKeyPair keyPair)
    {
        return GetTester<CAContractContainer.CAContractStub>(CaContractAddress,
            keyPair);
    }
    // internal ParliamentContractImplContainer.ParliamentContractImplStub GetParliamentContractTester(
    //     ECKeyPair keyPair)
    // {
    //     return GetTester<ParliamentContractImplContainer.ParliamentContractImplStub>(ParliamentContractAddress,
    //         keyPair);
    // }
    internal TokenContractContainer.TokenContractStub GetTokenContractTester(
        ECKeyPair keyPair)
    {
        return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress,
            keyPair);
    }
    
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
            keyPair);
    }
    

}