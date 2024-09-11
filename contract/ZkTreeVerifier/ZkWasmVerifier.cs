using System.Collections.Generic;
using System.Linq;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.ZkTreeVerifier
{
    class VerifyingKey
    {
        public G1Point alfa1;
        public G2Point beta2;
        public G2Point gamma2;
        public G2Point delta2;
        public List<G1Point> IC;
    }

    public class ZkTreeVerifier : ZkTreeVerifierContainer.ZkTreeVerifierBase
    {
        public override BoolValue VerifyProof(VerifyProofInput input)
        {
            var proof = new Proof()
            {
                A = new G1Point()
                {
                    X = input.Proof.A.X,
                    Y = input.Proof.A.Y,
                },
                B = new G2Point()
                {
                    X = new Fp2()
                    {
                        A = input.Proof.B.X.First,
                        B = input.Proof.B.X.Second,
                    },
                    Y = new Fp2()
                    {
                        A = input.Proof.B.Y.First,
                        B = input.Proof.B.Y.Second,
                    }
                },
                C = new G1Point()
                {
                    X = input.Proof.C.X,
                    Y = input.Proof.C.Y,
                }
            };
            var inputValues = input.Input.Select(x => x.ToBigIntValue()).ToArray();


            var verified = Verify(inputValues, proof);
            return new BoolValue { Value = verified };
        }

        #region Private Methods

        private bool Verify(BigIntValue[] input, Proof proof)
        {
            var snarkScalarField = "21888242871839275222246405745257275088548364400416034343698204186575808495617"
                .ToBigIntValue();
            var vk = VerifyingKey();
            Assert(input.Length + 1 == vk.IC.Count, "verifier-bad-input");
            // Compute the linear combination vk_x
            var vkX = new G1Point
            {
                X = "0",
                Y = "0"
            };
            for (var i = 0; i < input.Length; i++)
            {
                Assert(input[i] < snarkScalarField, "verifier-gte-snark-scalar-field");
                vkX = Context.Addition(vkX, Context.ScalarMul(vk.IC[i + 1], input[i]));
            }

            vkX = Context.Addition(vkX, vk.IC[0]);
            return Context.PairingProd4(
                Context.Negate(proof.A), proof.B,
                vk.alfa1, vk.beta2,
                vkX, vk.gamma2,
                proof.C, vk.delta2
            );
        }

        private VerifyingKey VerifyingKey()
        {
            Fp2 MakeFp2(string x, string y) => new Fp2() { A = x, B = y };
            G1Point MakeG1(string x, string y) => new G1Point() { X = x, Y = y };

            G2Point MakeG2(string x1, string x2, string y1, string y2) =>
                new G2Point() { X = MakeFp2(x1, x2), Y = MakeFp2(y1, y2), };

            return new VerifyingKey
            {
                alfa1 = MakeG1(
                    "20491192805390485299153009773594534940189261866228447918068658471970481763042",
                    "9383485363053290200918347156157836566562967994039712273449902621266178545958"),
                beta2 = MakeG2("4252822878758300859123897981450591353533073413197771768651442665752259397132",
                    "6375614351688725206403948262868962793625744043794305715222011528459656738731",
                    "21847035105528745403288232691147584728191162732299865338377159692350059136679",
                    "10505242626370262277552901082094356697409835680220590971873171140371331206856"),
                gamma2 = MakeG2("11559732032986387107991004021392285783925812861821192530917403151452391805634",
                    "10857046999023057135944570762232829481370756359578518086990519993285655852781",
                    "4082367875863433681332203403145435568316851327593401208105741076214120093531",
                    "8495653923123431417604973247489272438418190587263600148770280649306958101930"),
                delta2 = MakeG2("11559732032986387107991004021392285783925812861821192530917403151452391805634",
                    "10857046999023057135944570762232829481370756359578518086990519993285655852781",
                    "4082367875863433681332203403145435568316851327593401208105741076214120093531",
                    "8495653923123431417604973247489272438418190587263600148770280649306958101930"),
                IC = new List<G1Point>()
                {
                    MakeG1("907082046166848403662755682318758048763333219052759262226888852664247719678",
                        "13772868673976322661276556815121724196712611456125880819435532265591384929117"),
                    MakeG1("14808146059947600581045955083782207443549812856960403909774034802573047162050",
                        "1086338950447616826289662789021021878021771857362357242781351280816191633621"),
                    MakeG1("4409293795953421913352017523563336444457550942519899190266670184661945314920",
                        "13002239337001261969473038829996565475753673331173499940341032884807981931927")
                }
            };
        }

        #endregion
    }
}