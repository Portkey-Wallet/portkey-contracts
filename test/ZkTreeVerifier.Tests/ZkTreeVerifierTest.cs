using System.Threading.Tasks;
using AElf.Contracts.ZkTreeVerifier;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Shouldly;
using Xunit;

namespace AElf.Contracts.ZkWasmVerifier;

public class ZkTreeVerifierTest : ZkTreeVerifierTestBase
{
    private readonly IBlockchainService _blockchainService;
    private readonly ISmartContractAddressNameProvider _smartContractAddressNameProvider;
    private readonly ISmartContractAddressService _smartContractAddressService;

    public ZkTreeVerifierTest()
    {
        _blockchainService = GetRequiredService<IBlockchainService>();
        _smartContractAddressService = GetRequiredService<ISmartContractAddressService>();
        _smartContractAddressNameProvider = GetRequiredService<ISmartContractAddressNameProvider>();
    }

    [Fact]
    public async Task ZkWasmVerifier_Verify_Test()
    {
        var result = await ZkTreeVerifierStub.VerifyProof.SendAsync(GetInput());
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }
    //
    // [Fact]
    // public async Task ZkWasmVerifier_Verify_InvalidProof_Test()
    // {
    //     var input = GetInput();
    //     var invalidProofValue0 = input.Proof[0].Replace("1", "2");
    //     input.Proof[0] = invalidProofValue0;
    //     var result = await ZkTreeVerifierStub.Verify.SendWithExceptionAsync(input);
    //     result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
    // }

    private VerifyProofInput GetInput()
    {
        return new VerifyProofInput()
        {
            Proof = new VerifyProofInput.Types.Proof()
            {
                A = new VerifyProofInput.Types.G1Point()
                {
                    X = "2153331856662499996051076538230819642090593302283438685828141729152928861078",
                    Y = "20495207103405864582312617952509144095207901726588009706007940484803667662299",
                },
                B = new VerifyProofInput.Types.G2Point()
                {
                    X = new VerifyProofInput.Types.Fp2()
                    {
                        First = "4475848984507884668196060555892482119410285219809702955546309039288380061988",
                        Second = "9215979386460804819323730247391475374617444390304016703370063557678299436332",
                    },
                    Y = new VerifyProofInput.Types.Fp2()
                    {
                        First = "10465752260795552834687152738361855405389559549012910153299409915142428605006",
                        Second = "8951322989288772561314521456384540742686009828350707317833032068265017301495",
                    }
                },
                C = new VerifyProofInput.Types.G1Point()
                {
                    X = "7823756623231971347686901814320384247149959497568781520063439511302207204505",
                    Y = "14035346942917238856823019345400021595833683521501157999758950235096666725973",
                }
            },
            Input =
            {
                "15140580706175849125291604972365990054046394712485534721779098889584978970799",
                "3376217485850898635623259527345037392492599525789973564439384597968607340178"
            },
        };
    }
/*
 *
 *{
       "option": 3,
       "nullifierHash": "15140580706175849125291604972365990054046394712485534721779098889584978970799",
       "root": "3376217485850898635623259527345037392492599525789973564439384597968607340178",
       "proof_a": [
           "2153331856662499996051076538230819642090593302283438685828141729152928861078",
           "20495207103405864582312617952509144095207901726588009706007940484803667662299"
       ],
       "proof_b": [
           [
               "4475848984507884668196060555892482119410285219809702955546309039288380061988",
               "9215979386460804819323730247391475374617444390304016703370063557678299436332"
           ],
           [
               "10465752260795552834687152738361855405389559549012910153299409915142428605006",
               "8951322989288772561314521456384540742686009828350707317833032068265017301495"
           ]
       ],
       "proof_c": [
           "7823756623231971347686901814320384247149959497568781520063439511302207204505",
           "14035346942917238856823019345400021595833683521501157999758950235096666725973"
       ]
   }
 *
 *
 */
}