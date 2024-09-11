using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.ZkWasmVerifier;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace AElf.Contracts.ZkTreeVerifier;

public class ZkTreeVerifierTestBase : DAppContractTestBase<ZkTreeVerifierTestAElfModule>
{
    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    protected Address ZkTreeVerifierContractAddress { get; set; }

    internal ZkTreeVerifierContainer.ZkTreeVerifierStub ZkTreeVerifierStub { get; set; }

    protected ZkTreeVerifierTestBase()
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
                    File.ReadAllBytes(typeof(Contracts.ZkTreeVerifier.ZkTreeVerifier).Assembly.Location)),
                ContractOperation = new ContractOperation
                {
                    Deployer = DefaultAddress
                }
            }));

        ZkTreeVerifierContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        ZkTreeVerifierStub = GetZkWasmVerifierTester(DefaultKeyPair);
    }


    internal ZkTreeVerifierContainer.ZkTreeVerifierStub GetZkWasmVerifierTester(ECKeyPair keyPair)
    {
        return GetTester<ZkTreeVerifierContainer.ZkTreeVerifierStub>(ZkTreeVerifierContractAddress,
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