using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Groth16Verifier;
using Shouldly;
using Xunit;

namespace ZkLoginVerifier;

public class ZkLoginVerifierTest : ZkLoginVerifierTestBase
{
    private readonly IBlockchainService _blockchainService;
    private readonly ISmartContractAddressNameProvider _smartContractAddressNameProvider;
    private readonly ISmartContractAddressService _smartContractAddressService;

    public ZkLoginVerifierTest()
    {
        _blockchainService = GetRequiredService<IBlockchainService>();
        _smartContractAddressService = GetRequiredService<ISmartContractAddressService>();
        _smartContractAddressNameProvider = GetRequiredService<ISmartContractAddressNameProvider>();
    }

    [Fact]
    public async Task ZkWasmVerifier_Verify_Test()
    {
        var result = await ZkLoginVerifierStub.VerifyProof.SendAsync(GetInput());
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    private VerifyProofInput GetInput()
    {
        //Proof { a: (16064021502655363062307076179816697937770799734579837498288388162959372583383, 12973084627773479797045060130877176311884431024711139604973176441013311927375),
        // b: (QuadExtField(17124651168636736929005707359493830139234581476966753683014956092886080848289 + 1459173893442487269346264988243139918693811493637734689501671336622948201526 * u),
        // QuadExtField(5473121819594615640600034118100895293193497130433541397089347450537738829028 + 11444737826320342343660155709904867160956772485138252432744478951797315457649 * u)),
        // c: (9014147212694279758170903928442121478064363641278215246968145895676996954261, 4271620201795580805854651719595920892456981172662813165340109650396545100186) }
        // 
        // 
        
        /**
         *
         *
    let a = [
        "16064021502655363062307076179816697937770799734579837498288388162959372583383",
        "12973084627773479797045060130877176311884431024711139604973176441013311927375",
    ];

    
    var b = [
        [
            "1459173893442487269346264988243139918693811493637734689501671336622948201526",
            "17124651168636736929005707359493830139234581476966753683014956092886080848289",
        ],
        [

            "11444737826320342343660155709904867160956772485138252432744478951797315457649",
            "5473121819594615640600034118100895293193497130433541397089347450537738829028",
        ],
    ];
    let c = [
        "9014147212694279758170903928442121478064363641278215246968145895676996954261",
        "4271620201795580805854651719595920892456981172662813165340109650396545100186",
    ];
         * 
         */

        return new VerifyProofInput()
        {
            Proof = new VerifyProofInput.Types.Proof()
            {
                A = new VerifyProofInput.Types.G1Point()
                {
                    X = "16064021502655363062307076179816697937770799734579837498288388162959372583383",
                    Y = "12973084627773479797045060130877176311884431024711139604973176441013311927375",
                },
                B = new VerifyProofInput.Types.G2Point()
                {
                    X = new VerifyProofInput.Types.Fp2()
                    {
                        Second = "17124651168636736929005707359493830139234581476966753683014956092886080848289",
                        First = "1459173893442487269346264988243139918693811493637734689501671336622948201526",
                    },
                    Y = new VerifyProofInput.Types.Fp2()
                    {
                        Second = "5473121819594615640600034118100895293193497130433541397089347450537738829028",
                        First = "11444737826320342343660155709904867160956772485138252432744478951797315457649",
                    }
                },
                C = new VerifyProofInput.Types.G1Point()
                {
                    X = "9014147212694279758170903928442121478064363641278215246968145895676996954261",
                    Y = "4271620201795580805854651719595920892456981172662813165340109650396545100186",
                }
            },
            Input =
            {
                "9340168379609132233074617967082586477056958824754337733208830122770402483169",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "52",
                "50",
                "3396532581541178759",
                "11518649075776660972",
                "13994161743993522955",
                "13040869997369887280",
                "4570896913005535976",
                "1027177842286799110",
                "6001978221510562549",
                "13152883047887600260",
                "6588277067349496318",
                "1229598054536742939",
                "11104061911487177454",
                "15331301469399195864",
                "2434671501873975668",
                "6535610617082804952",
                "7031934839429873932",
                "10163503620231877779",
                "5523480602185365631",
                "16814607822183144887",
                "9632407321005741151",
                "13687261942011534404",
                "17826414245406600220",
                "10853064316492095923",
                "2736415924800035168",
                "1924553967013368021",
                "316381747969222733",
                "12941287917034874638",
                "10580317614191146173",
                "16764449645036038561",
                "6181051206592381282",
                "11993479122801132857",
                "5625281005480684031",
                "13894132723959269276",
                "166",
                "119",
                "153",
                "147",
                "150",
                "220",
                "73",
                "162",
                "138",
                "214",
                "201",
                "194",
                "66",
                "113",
                "155",
                "179"
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