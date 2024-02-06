using System;
using System.Security.Policy;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using ZkVerifier;
using Hash = AElf.Types.Hash;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private const string IssuerName = "Google";

    private const string IssuerPubkey =
        "bb5494d4b7d52cf1c2a333311f6328e2580e11e3f3366d2d46078b7b357a7df02dd20ba75532f0ee89cb467aead3f2335bbc9647b424ae604bee34ca127e6efaa2a16f029f06cb48b3e6cc636664a75f209d3c4a2f1a12dad15ccc690f2cf822cec92e7a63208519e259aa0b7327a191ddeaa86125bd6fd50cbe406964e0d272d5923468f73fb8d11433b95684f00900166c59ce8c37c7e54960a763ca4909d224fdc024b40d14d7bb6ebd576eb855fff78efade75988a46483094bf71340c315c5834c7f5c5c34d3951655122476070a5938e904fd9d3f0559e16582fbd68655df86ca7d68d022de95fe2b1231a85db00012002a786531adc2256e35df6dc9b";

    private const string VerifyingKey =
        "987eb6f620cbd00941204ec4f6a81a46419373a821c8ffd9affca1291900631ffeca146164a8f8cada7dd266805f0f0d406158686ebab25caf020ec28a02c6073dbbf2228db69a59b85c97eced983f4189e8ecb6397838d0bea80eb50af98800edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e195a8c88cd805de66c9444599f40acddf1f919f688bce5021d6c053d1aa0c116012f5405ffa0c13b962196c463802425c52b03a6fd98f630e74c6cb47e5cfbef9642000000000000009c6ebeef893d0b589ea300c6315e6c619405c9540fe50a349ba5e6162869571b7763c5ed08a9ca3a22be657458d0abe287e1e90eeb090eafdaa015fe5b8fd3af5daaa2fdb341443764c35cdd224dea08328d679f1f3c51a924b8f2169c967f0cd8294c5a3eee3658a7ea37d03dabc896166e7511bd358b1103b954eb40b6322f30e5860d9355842fc24ecc6d9d552de4fd96b94b10a1637acaa345856c9b99965fef99948868fcf9371dade7eb884332e406b862f2ad3c707000dfc00afa759776504a0be7d95f263fc3eeb05182fe13e87a454de725234f00b09f066e67d4988b9cec1068f188de4b159c67e9179122a24f7cbb08a60e8d85d1f14b30cda59718097eb24e6848b5259007f899c6c9cfd74a6f80d83012ba9d8d81cf749b9f94c3678ff9c27b3819790a8f34aac6f7a1dcb8c64b0849e556907cf9b8bb03c814a83342e1a1ec91cfc09040cdac4860e4bf58eab54cf6683915385273bee047043fff7233f542b5d2e7f253441c5510feb73d6556f4e82cec7fa1aae962928590a89fd7e4ab8c0acec7e987934ccc82613cf27cc7877d4ffba7292964c5196b8b3c59d35f65bef53f79d8859d885c7812f3990c178d11972915db542a4e5491843462343bd1751c0e20600fadec3452bceabb40b16cbf690ec08d3136fc531901f1794a95c305f59c5eb0f08020133677631e6c63eba360e8210e0900011d198a33d592db0b3d1ca7de038df1c7beefc126fd54edde961b5ccacfd0e7c6a63a1f2541ab8a0f4b1a5fef017c61db38c07ce6293c60444833a3d3f559388322440da9d8023d27b01352484ecb3b584234aea7c4864c2a9636281a780f4ca72a88130e313f20ad8d1be5d7fcb0d8c3f19a7947e175276e9e4fdf2662dd69d40707a13dc6fbfa476630aed4d4765c16ed498193c13f101b7bc026c8cdf0e1e33224a72f49b1838a9faae596b3a5000f58a853c4357ec6e513c7c72aed53443b3f2799ebea44e164adc686e984e421bd2658aa7e2dcaed5e8f7679e49135645ba764036343868a918febf74f0582a67a3ac12fe45054be22897d1032ae3425d7957984eea1f42137dcb6e61b2bd0fcd999f70a1dda03eb5ba491b7d5c728a5d5ab0dad5170f3f89fc588a96fb9e352630ef658612b90cd70cef388c901e500567fd49a788657ef607b53faeeb3f2f3044ac411d74383312c3ec16d34c60d7380432791c7a9596772efa9f9488af5628731110da0ac5981381e2c50718e772354f1b7886450951b83e94b65218b12f49c33fec3fc5da0adf91180ac471841b00245200d7a7d36022bd3436e441ee98cfa2e15478dbdd484ddb8c75b5346a48b3005c517ccdf78088e0e205bb0ac382782934f64d634902926027e489ee6aced666e6623f2e13d7bddb461c0390afab9b66d018a190f4acc83e53b6b43cdd6149fe704875f4a251912f5ebbe60213893498d25b497b576f97a2ba9abbfa2f882cc3da1088bfb7e0681cc3df70b4f4be3101ac68ee75db080c60b1489bc74b83cd0b45697fffa8f54a41231e46f48fd240ded208cf24128d1ff5071085574d2488c517c9011d3940924f7ac57a4d9123c7a9ae81592f9e707cb8353476944895c52c7d483bfa83e6f242b867eda6f2f4e483c6028c5ddd82f61b6e96a9d841a176ca7691ee06f99bb78565dd1efc2962ccf825acc22d5a1af5c72a365e60f443c0307570e1ee665d20803ade104d1a982dace277f0ada2bbae8ded842bab4c80d2033b8935ccfd3f6a2eb4543b8a141eabcc11ce6ff080e88fd77794aabfeaf4b649c6687e30e943e1532ba877ba8ec58fcf83933600de41b1d39d697f0cdeb0580bdf72004d57cb34375c3e93ed9a6c8cd57fc6505e72898e0019de2273aa3d12bef1b82f246a5fbf87e1ad673679ebc8d43927221f43b9be4f5fdd3c295392e61d8b08e7d15ee1a673a72b2f7d550bcd790dafbb0abf05afd53269e272f77eac01045a9d7a57591ee749e32e503beeb087d85d422d8b20d3bd33ea81b69cbf948413113441fc46ded8012287fb616c50ce0644c26ad260700c3a84090f5012d55640321f863f611e32c8c86323b6447e9667302fcea4bf4a7295ad8a780e8556200f386e136cbd88d2c4c0f55bece8ec448ec97bdcb228d966b1ae1d6b90fd04d151827d23192a81ba92ecc6497ae4efe29816c9d3ff6f59c86790f9d2cef3c6512df054cd6b592f8b22ab99c6feec5b5769854628e982696d181edbd7af51c742fe0ad556719dc5d058af8323d5ad2e1ced663f6d651e7270c48cb754fc8c7dc54bea6ea5066168d83a1dbaec797269e2a27854b29efadca48f45810a5981a9487c3963e08aaa9c753625d6e1bf7e0c7715b742b499eec6024278661d81e3966c34b808958cd41dd4231b73a7c83b53b7952a7662ee23fc992dbe1762e407599e967a2a0dc4a1460b2696271c18bf9a4a2ff81033d091c3b5dcf159355f5d80b2f2416881f87f36250f8b698c54ed578ddba66ea2e843139f53bda78227f94e128bbacf2a55aca6aa45c57c1b8d37f22bdf1f43b33416e10154aaeb5b066197c7a10a0aa1ada15d121772458db8a2cceb2246f1989ae8034b0834ba046cc0b3b2aeb2e5f1a4509689e6f90247ba522a7750ec45dcfb8ccd6cbb96914094e6d22593c94d45a8431e43a878190cb76172c8352b1498210804dfd17d90c863f4e2f38ad834d5ba68786bcb8a6f23620f80cfda6a0897223c2e8cc65c816db7a183e8ed9a201f5052c550006e73b82e84b5aa7c68a03963589bbfa7211ebb94eb6ea67f0afa8c377537392cff65e6d387c706a132061e951a618e86fde840418dfb530c78fc62bb5f11bd7d02de011108af6c5292650aa5defc8681c6e665d64765a4b361e54beda792c256126fa7d20bd8d1196dbd8df7280de28df371c71676ff6119d858dfe4d89958facd30a83f85cbbc1f5e0f033e5bec0cbbf5776b6b5dc042c0216";

    private const string IdentifierHash = "7f0bdbbd5bc4c68c21afe63067d39bbc863432cec2c56b9d351cad89346a8b47";
    private const string SaltForZk = "a677999396dc49a28ad6c9c242719bb3";

    private const string ZkProof =
        "e4f43e941f23f1478ffd459a9f6ec97e60ad790467bb9ffca97d7865ac5df09953cbaf72d64482095954e1770b249de00e405b2c5ac47b601850cac0939749183f8447be0c6b3e44e7bb61100b1f6b0fac038ea4f56271c45f2a3ebe79a367034aa423bf11f4dc3ab21440dd6642255d4a50d843a3db42fc3fa79852adec062f";

    [Fact]
    public async Task ZkVerify_Flow_Test()
    {
        await InitializeContract();
        {
            var tx = await CaContractStub.AddZkIssuer.SendAsync(new IssuerPublicKeyEntry
            {
                IssuerName = IssuerName,
                IssuerPubkey = IssuerPubkey
            });
        }
        {
            var tx = await CaContractStub.SetZkVerifiyingKey.SendAsync(new StringValue
            {
                Value = VerifyingKey
            });
        }

        {
            await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
            {
                GuardianApproved = new GuardianInfo
                {
                    IdentifierHash = Hash.LoadFromHex(IdentifierHash),
                    ZkGuardianInfo = new ZkGuardianInfo
                    {
                        IdentifierHash = Hash.LoadFromHex(IdentifierHash),
                        Salt = SaltForZk,
                        IssuerName = IssuerName,
                        IssuerPubkey = IssuerPubkey,
                        Proof = ZkProof
                    }
                },
                ManagerInfo = new ManagerInfo
                {
                    Address = User1Address,
                    ExtraData = "123"
                }
            });
            var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
            {
                LoginGuardianIdentifierHash = Hash.LoadFromHex(IdentifierHash)
            });
        }
    }

    private async Task InitializeContract()
    {
        var verificationTime = DateTime.UtcNow;

        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress,
        });
        await CaContractStub.SetCreateHolderEnabled.SendAsync(new SetCreateHolderEnabledInput
        {
            CreateHolderEnabled = true
        });
        {
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName1,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress1 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName2,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress2 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName3,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress3 }
            });
            await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
            {
                Name = VerifierName4,
                ImageUrl = ImageUrl,
                EndPoints = { "127.0.0.1" },
                VerifierAddressList = { VerifierAddress4 }
            });
        }
        {
            var verifierServers = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
            _verifierId = verifierServers.VerifierServers[0].Id;
            _verifierId1 = verifierServers.VerifierServers[1].Id;
            _verifierId2 = verifierServers.VerifierServers[2].Id;
            _verifierId3 = verifierServers.VerifierServers[3].Id;
            _verifierId4 = verifierServers.VerifierServers[4].Id;
        }
    }
}