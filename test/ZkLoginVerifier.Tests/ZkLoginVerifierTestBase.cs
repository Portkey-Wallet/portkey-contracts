using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace ZkLoginVerifier;

public class ZkLoginVerifierTestBase : DAppContractTestBase<ZkLoginVerifierTestAElfModule>
{
    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    protected Address ZkLoginVerifierContractAddress { get; set; }

    internal Groth16Verifier.Groth16VerifierContainer.Groth16VerifierStub ZkLoginVerifierStub { get; set; }

    protected ZkLoginVerifierTestBase()
    {
        // TokenContractStub = GetTokenContractTester(DefaultKeyPair);
        // TokenContractImplStub = GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress,
        //     DefaultKeyPair);
        ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
        var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
            new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(
                    File.ReadAllBytes(typeof(ZkLoginVerifier).Assembly.Location)),
                ContractOperation = new ContractOperation
                {
                    Deployer = DefaultAddress
                }
            }));

        ZkLoginVerifierContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        ZkLoginVerifierStub = GetZkWasmVerifierTester(DefaultKeyPair);
    }


    internal Groth16Verifier.Groth16VerifierContainer.Groth16VerifierStub GetZkWasmVerifierTester(ECKeyPair keyPair)
    {
        return GetTester<Groth16Verifier.Groth16VerifierContainer.Groth16VerifierStub>(ZkLoginVerifierContractAddress,
            keyPair);
    }

    // internal TokenContractImplContainer.TokenContractImplStub GetTokenContractTester(
    //     ECKeyPair keyPair)
    // {
    //     return GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress,
    //         keyPair);
    // }
    //
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress, keyPair);
    }
}