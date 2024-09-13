using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using Groth16Verifier;
using Shouldly;
using Xunit;

namespace ZkLoginVerifier;

public class ZkLoginVerifierTest : ZkLoginVerifierTestBase
{
    [Fact]
    public async Task ZkWasmVerifier_Verify_Test()
    {
        var result = await ZkLoginVerifierStub.VerifyProof.SendAsync(GetInput());
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("-1")]
    [InlineData("21888242871839275222246405745257275088548364400416034343698204186575808495617")]
    public async Task ZkWasmVerifier_Verify_InvalidStrings_Test(string invalidString)
    {
        async Task RunTest(Func<VerifyProofInput, VerifyProofInput> tweak)
        {
            var input = GetInput();
            var result = await ZkLoginVerifierStub.VerifyProof.SendWithExceptionAsync(tweak(input));
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        // ReSharper disable once ComplexConditionExpression
        List<Action<VerifyProofInput, string>> tweaks = new List<Action<VerifyProofInput, string>>()
        {
            (input, value) => input.Proof.A.X = value,
            (input, value) => input.Proof.A.Y = value,
            (input, value) => input.Proof.B.X.First = value,
            (input, value) => input.Proof.B.X.Second = value,
            (input, value) => input.Proof.B.Y.First = value,
            (input, value) => input.Proof.B.Y.Second = value,
            (input, value) => input.Proof.C.X = value,
            (input, value) => input.Proof.C.Y = value,
            (input, value) => input.Input[0] = value,
        };
        foreach (var tweak in tweaks)
        {
            await RunTest(input =>
            {
                tweak(input, invalidString);
                return input;
            });
        }
    }

    [Fact]
    public async Task ZkWasmVerifier_Verify_InvalidProof_Test()
    {
        var input = GetInput();
        input.Proof.A.X = input.Proof.A.X.Replace("1", "2");
        var result = await ZkLoginVerifierStub.VerifyProof.SendWithExceptionAsync(input);
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
    }

    private VerifyProofInput GetInput()
    {
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
}