using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using AetherLink.Contracts.Consumer;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class  CAContractTests
{
    [Fact]
    public async Task CreateHolderWithReferralCode()
    {
        await Initiate();
        await InitTestVerifierServer();
        var verifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var opType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var id = _verifierServers[0].Id;
    
        var manager = new ManagerInfo
        {
            Address = User1Address,
            ExtraData = "123"
        };
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = _guardian,
                Type = GuardianType.OfEmail,
                VerificationInfo = new VerificationInfo
                {
                    Id = id,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verifyTime, _guardian, 0, salt, opType),
                    VerificationDoc =
                        $"{0},{_guardian.ToHex()},{verifyTime},{VerifierAddress.ToBase58()},{salt},{opType},{MainChainId}"
                }
            },
            ManagerInfo = manager,
            ReferralCode = "123",
            ProjectCode = "345"
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = _guardian
        });
        var invited = Invited.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(Invited)).NonIndexed);
        invited.CaHash.ShouldBe(holderInfo.CaHash);
        invited.ContractAddress.ShouldBe(CaContractAddress);
        invited.ReferralCode.ShouldBe("123");
        invited.ProjectCode.ShouldBe("345");
        invited.MethodName.ShouldBe("CreateCAHolder");
    }

    public async Task InitTestVerifierServers()
    {
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Gauss",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7") },
            VerifierId = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Portkey",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2mBnRTqXMb5Afz4CWM2QakLRVDfaq2doJNRNQT1MXoi2uc6Zy3") },
            VerifierId = Hash.LoadFromHex("0745df56b7a450d3a5d66447515ec2306b5a207277a5a82e9eb50488d19f5a37")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Minerva",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("3sWGDJhu5XDycTWXGa6r4qicYKbUyy6oZyRbRRDKGTiWTXwU4") },
            VerifierId = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "CryptoGuardian",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/CryptoGuardian.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2bWwpsN9WSc4iKJPHYL4EZX3nfxVY7XLadecnNMar1GdSb4hJz") },
            VerifierId = Hash.LoadFromHex("61bc9c43eb8d311c21b3fe082884875e4fd377f11aa8ac278ba4ffab4e1ba3c9")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "DokewCapital",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/DokewCapital.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2kqh5HHiL4HoGWqwBqWiTNLBnr8eAQBEJi3cjH1btPovTyGwej") },
            VerifierId = Hash.LoadFromHex("7550fcdf5f64a3e0572130822b29e4c5bb62ad226e8dba1f23a7bbf00a56cc72")
        });
    }
    
    [Fact]
    public async Task CreateHolderWithReferralCode2()
    {
        await Initiate();
        await InitTestVerifierServers();
    
        var manager = new ManagerInfo
        {
            Address = Address.FromBase58("7MPohXygSyPC1nj3abQQ1G5kP27rSy3PfYWVDszznjphtjAQ4"),
            ExtraData = "{\"transactionTime\":1722408923886,\"deviceInfo\":\"FhnR4caky62N/3msEMcB/MytONu3ru8UA0USyuLMy9FnHc384X+mk+ZzqMu7KCvX\",\"version\":\"2.0.0\"}"
        };
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("33cf3ee51f74564bfd58567cd2de2470e5123dfa66e6bacf7c55a6e45d7cbe8a"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("0745df56b7a450d3a5d66447515ec2306b5a207277a5a82e9eb50488d19f5a37"),
                    Signature = ByteString.CopyFromUtf8("uB129ec1hhpbUpJ8vtG0qKWg3MPQCURlo3pmYgUS/PEg4PSGhE/5bPQp4/iK3X9cerHGdoQudqflkKKHU1Yz0gA="),
                    VerificationDoc = "2,33cf3ee51f74564bfd58567cd2de2470e5123dfa66e6bacf7c55a6e45d7cbe8a,2024/07/31 06:35:15.424,2mBnRTqXMb5Afz4CWM2QakLRVDfaq2doJNRNQT1MXoi2uc6Zy3,66bc0fcf19fa0f4495dc2b275babdd8b,8,1931928,4f3b571b4776fef4bb806b7c6d94cd76957f0f3a01a6b6d2e84a7200298c5a2b"
                }
            },
            ManagerInfo = manager,
            ReferralCode = "123",
            ProjectCode = "345"
        });
        var caHolderErrorOccured = CAHolderErrorOccured.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderErrorOccured)).NonIndexed);
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("5dafb9d20e69ddaddb3bdf1e3213e377f1e28399a0fea8cc07cd099fc3abcb13")
        });
        var invited = Invited.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(Invited)).NonIndexed);
        invited.CaHash.ShouldBe(holderInfo.CaHash);
        invited.ContractAddress.ShouldBe(CaContractAddress);
        invited.ReferralCode.ShouldBe("123");
        invited.ProjectCode.ShouldBe("345");
        invited.MethodName.ShouldBe("CreateCAHolder");
    }
    
    [Fact]
    public async Task CreateHolderWithGoogleAccount()
    {
        await Initiate();
        await InitTestVerifierServer();
        const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e199269dcf1c401614ea98438a30ba34abd939ce237bd822ab02ebcbcf210126e28a8d022c2b8467046f18f46e8a5a17a5453ea5241f39dfc858c28dc43822a269e8200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Type = GuardianType.OfGoogle,
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs"
        });
        await CaContractStub.AddKidPublicKey.SendAsync(new KidPublicKeyInput
        {
            Type = GuardianType.OfGoogle,
            Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
            PublicKey = "zaUomGGU1qSBxBHOQRk5fF7rOVVzG5syHhJYociRyyvvMOM6Yx_n7QFrwKxW1Gv-YKPDsvs-ksSN5YsozOTb9Y2HlPsOXrnZHQTQIdjWcfUz-TLDknAdJsK3A0xZvq5ud7ElIrXPFS9UvUrXDbIv5ruv0w4pvkDrp_Xdhw32wakR5z0zmjilOHeEJ73JFoChOaVxoRfpXkFGON5ZTfiCoO9o0piPROLBKUtIg_uzMGzB6znWU8Yfv3UlGjS-ixApSltsXZHLZfat1sUvKmgT03eXV8EmNuMccrhLl5AvqKT6E5UsTheSB0veepQgX8XCEex-P3LCklisnen3UKOtLw"
        });
        var verifyTime = DateTime.UtcNow;
        var salt = "9d3a7022ff719f4aab51aa6462e3284c";
        var opType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        var verifierId = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9");
        var identifierHashStr = "e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f";
        var identifierHash = Hash.LoadFromHex(identifierHashStr);
        var managerAddress = Address.FromBase58("7FpQnakWib4b88Qzpw2wDEb5wyFNxPPivEy1Ztn5rd2vedXfi");
        var manager = new ManagerInfo
        {
            Address = managerAddress,
            ExtraData = "{\"transactionTime\":1719987197809,\"deviceInfo\":\"mVyu99uy84ljd8YWMN8wwHJTb4hkEmjeBpqWwYI44Q40Y9oju8vhvafww/pNOGi4\",\"version\":\"2.0.0\"}"
        };
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = identifierHash,
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = verifierId,
                    Signature = GenerateSignature(VerifierKeyPair, VerifierAddress, verifyTime, identifierHash, 0, salt, opType),
                    VerificationDoc =
                        $"{0},{identifierHash.ToHex()},{verifyTime},{VerifierAddress.ToBase58()},{salt},{opType},{MainChainId}"
                }
            },
            ManagerInfo = manager,
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = identifierHash
        });
        var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos[0].Address);
    }
    
    [Fact]
    public async Task<Hash> CreateHolderWithZkLogin()
    {
        await Initiate();
        // await InitTestVerifierServer();
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Gauss",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7") },
            VerifierId = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Portkey",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2mBnRTqXMb5Afz4CWM2QakLRVDfaq2doJNRNQT1MXoi2uc6Zy3") },
            VerifierId = Hash.LoadFromHex("0745df56b7a450d3a5d66447515ec2306b5a207277a5a82e9eb50488d19f5a37")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Minerva",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("3sWGDJhu5XDycTWXGa6r4qicYKbUyy6oZyRbRRDKGTiWTXwU4") },
            VerifierId = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "CryptoGuardian",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/CryptoGuardian.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2bWwpsN9WSc4iKJPHYL4EZX3nfxVY7XLadecnNMar1GdSb4hJz") },
            VerifierId = Hash.LoadFromHex("61bc9c43eb8d311c21b3fe082884875e4fd377f11aa8ac278ba4ffab4e1ba3c9")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "DokewCapital",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/DokewCapital.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2kqh5HHiL4HoGWqwBqWiTNLBnr8eAQBEJi3cjH1btPovTyGwej") },
            VerifierId = Hash.LoadFromHex("7550fcdf5f64a3e0572130822b29e4c5bb62ad226e8dba1f23a7bbf00a56cc72")
        });
        var manager = new ManagerInfo
        {
            Address = Address.FromBase58("19GP2HqWyNFczf71M5P93cbP7CoJMHiU9LYx9w4bk2LFhM1Pv"),
            ExtraData = "{\"transactionTime\":1721612262260,\"deviceInfo\":\"7cnulxkD5S2l809oJtNE3yng6pXWqeLzCAIMd+BKApgZ94hkW51Yl566M9mqUC81\",\"version\":\"2.0.0\"}"
        };
        const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e199269dcf1c401614ea98438a30ba34abd939ce237bd822ab02ebcbcf210126e28a8d022c2b8467046f18f46e8a5a17a5453ea5241f39dfc858c28dc43822a269e8200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Type = GuardianType.OfGoogle,
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs"
        });
        await CaContractStub.AddKidPublicKey.SendAsync(new KidPublicKeyInput
        {
            Type = GuardianType.OfGoogle,
            Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
            PublicKey = "zaUomGGU1qSBxBHOQRk5fF7rOVVzG5syHhJYociRyyvvMOM6Yx_n7QFrwKxW1Gv-YKPDsvs-ksSN5YsozOTb9Y2HlPsOXrnZHQTQIdjWcfUz-TLDknAdJsK3A0xZvq5ud7ElIrXPFS9UvUrXDbIv5ruv0w4pvkDrp_Xdhw32wakR5z0zmjilOHeEJ73JFoChOaVxoRfpXkFGON5ZTfiCoO9o0piPROLBKUtIg_uzMGzB6znWU8Yfv3UlGjS-ixApSltsXZHLZfat1sUvKmgT03eXV8EmNuMccrhLl5AvqKT6E5UsTheSB0veepQgX8XCEex-P3LCklisnen3UKOtLw"
        });
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("6b9ef910ee5f37b307b2320bc6b090af64a7accbb00f49fae5b8677d13a51276"),
                    Salt = "801fed43d8e940448297e0054cde0749",
                    Nonce = "3e59cecd0fa87632a2aba8334a67333a0943f1c620e8b87c5a6486dc76edb3bb",
                    ZkProof = "{\"pi_a\":[\"1835711353314891866773996467945488412122056560399852492499629420145170775603\",\"10929121428644391988093906032220514214996670629503418705298966308304083400106\",\"1\"],\"pi_b\":[[\"18840146228960295689846947870771810741659888162582722386683639248379595868363\",\"11894272382473924104688351934179005172676498238074801709706784611023997321944\"],[\"8515913670521307872158311121057301365057464770440093699290751666385314360789\",\"7313327600451314634196668630518506974337603850525245782560483249691296051706\"],[\"1\",\"0\"]],\"pi_c\":[\"12244886529014467607914084594279537498503442753395914603866890175886993868598\",\"7419086538599063374544143314983188234654827591882882187825948319833587492293\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "1835711353314891866773996467945488412122056560399852492499629420145170775603","10929121428644391988093906032220514214996670629503418705298966308304083400106","1" },
                        ZkProofPiB1 = { "18840146228960295689846947870771810741659888162582722386683639248379595868363","11894272382473924104688351934179005172676498238074801709706784611023997321944" },
                        ZkProofPiB2 = { "8515913670521307872158311121057301365057464770440093699290751666385314360789","7313327600451314634196668630518506974337603850525245782560483249691296051706" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "12244886529014467607914084594279537498503442753395914603866890175886993868598","7419086538599063374544143314983188234654827591882882187825948319833587492293","1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        AddManagerAddress = new AddManager
                        {
                            CaHash = Hash.Empty,
                            ManagerAddress = Address.FromBase58("19GP2HqWyNFczf71M5P93cbP7CoJMHiU9LYx9w4bk2LFhM1Pv"),
                            Timestamp = new Timestamp
                            {
                                Seconds = 1721203238,
                                Nanos = 912000000
                            }
                        }
                    }
                }
            },
            ManagerInfo = manager,
            ReferralCode = "",
            ProjectCode = ""
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f")
        });
        var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos.First().Address);
        return holderInfo.CaHash;
    }

    // [Fact]
    // public async Task AddOrUpdateVerifyingKey()
    // {
    //     const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
    //     const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e199269dcf1c401614ea98438a30ba34abd939ce237bd822ab02ebcbcf210126e28a8d022c2b8467046f18f46e8a5a17a5453ea5241f39dfc858c28dc43822a269e8200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
    //
    //     var addResult = await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
    //     {
    //         CircuitId = circuitId,
    //         VerifyingKey_ = verifyingKey,
    //         Description = "test"
    //     });
    //     var verifyingKeyAdded = VerifyingKeyAdded.Parser.ParseFrom(addResult.TransactionResult.Logs.First(e => e.Name == nameof(VerifyingKeyAdded)).NonIndexed);
    //     verifyingKeyAdded.CircuitId.ShouldBe(circuitId);
    //     verifyingKeyAdded.VerifyingKey.ShouldBe(verifyingKey);
    // }

    // [Fact]
    // public async Task GetVerifyingKey()
    // {
    //     const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
    //     const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e199269dcf1c401614ea98438a30ba34abd939ce237bd822ab02ebcbcf210126e28a8d022c2b8467046f18f46e8a5a17a5453ea5241f39dfc858c28dc43822a269e8200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
    //     var getResult = await CaContractStub.GetVerifyingKey.CallAsync(new StringValue
    //     {
    //         Value = circuitId
    //     });
    //     getResult.CircuitId.ShouldBe(circuitId);
    //     getResult.VerifyingKey_.ShouldBe(verifyingKey);
    // }

    [Fact]
    public async Task StartOracleDataFeedsTaskTest()
    {
        var setOracleAddressResult = await CaContractStub.SetOracleAddress.SendAsync(
            Address.FromBase58("21Fh7yog1B741yioZhNAFbs3byJ97jvBmbGAPPZKZpHHog5aEg"));
        var jwtIssuerResult = await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Type = GuardianType.OfFacebook,
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs"
        });
        var jwtIssuerCreated = JwtIssuerCreated.Parser.ParseFrom(jwtIssuerResult.TransactionResult.Logs.First(e => e.Name == nameof(JwtIssuerCreated)).NonIndexed);
        jwtIssuerCreated.Issuer.ShouldBe("https://accounts.google.com");
        jwtIssuerCreated.JwksEndpoint.ShouldBe("https://www.googleapis.com/oauth2/v3/certs");
        
        var taskStartedResult = await CaContractStub.StartOracleDataFeedsTask.SendAsync(new StartOracleDataFeedsTaskRequest
        {
            Type = GuardianType.OfFacebook,
            SubscriptionId = 2
        });
        var oracleDataFeedsTaskStarted = OracleDataFeedsTaskStarted.Parser.ParseFrom(taskStartedResult.TransactionResult.Logs.First(e => e.Name == nameof(OracleDataFeedsTaskStarted)).NonIndexed);
        oracleDataFeedsTaskStarted.SubscriptionId.ShouldBe(123456);
        oracleDataFeedsTaskStarted.RequestTypeIndex.ShouldBe(1);
    }
    
    [Fact]
    public async Task<Hash> CreateHolderWithZkLogin2()
    {
        await Initiate();
        await InitTestVerifierServer();
        
        var manager = new ManagerInfo
        {
            Address = Address.FromBase58("YanBhpryvqf9RcFVapi1vhu5fvCzoVeT8x5AbRBvTNZKsQJRf"),
            ExtraData = "{\"transactionTime\":1721699872051,\"deviceInfo\":\"I0tgT1k9YDMM2g/UQM38NXDemU/++UkaOUWcZZn77thJrMIZp7/A0jvHrelAXRsM\",\"version\":\"2.0.0\"}"
        };
        const string circuitId = "a6530155400942bd0c70cc9cb164a53aa2104cb6ee95da1454d085d28d9dd18f";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e199269dcf1c401614ea98438a30ba34abd939ce237bd822ab02ebcbcf210126e28a8d022c2b8467046f18f46e8a5a17a5453ea5241f39dfc858c28dc43822a269e8200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Type = GuardianType.OfGoogle,
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs"
        });
        await CaContractStub.AddKidPublicKey.SendAsync(new KidPublicKeyInput
        {
            Type = GuardianType.OfGoogle,
            Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
            PublicKey = "zaUomGGU1qSBxBHOQRk5fF7rOVVzG5syHhJYociRyyvvMOM6Yx_n7QFrwKxW1Gv-YKPDsvs-ksSN5YsozOTb9Y2HlPsOXrnZHQTQIdjWcfUz-TLDknAdJsK3A0xZvq5ud7ElIrXPFS9UvUrXDbIv5ruv0w4pvkDrp_Xdhw32wakR5z0zmjilOHeEJ73JFoChOaVxoRfpXkFGON5ZTfiCoO9o0piPROLBKUtIg_uzMGzB6znWU8Yfv3UlGjS-ixApSltsXZHLZfat1sUvKmgT03eXV8EmNuMccrhLl5AvqKT6E5UsTheSB0veepQgX8XCEex-P3LCklisnen3UKOtLw"
        });
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("317eee9c80733c7efbb819b7812152f137373baddb7a182480b193f754157127"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8"),
                    // Signature = ByteString.Empty,
                    // VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("317eee9c80733c7efbb819b7812152f137373baddb7a182480b193f754157127"),
                    Salt = "8a657d6edc6e47918d356a3c9d609b99",
                    Nonce = "b341f8925c6a9bd18e789f090330eb779119a65f488bdf4d5b7ab2ce722fefdd",
                    ZkProof = "{\"pi_a\":[\"13294961411453839773564491536752009589902690318016736666092624853678685513858\",\"3177631847785189443921604495165856561717745488619503118934586405578139020897\",\"1\"],\"pi_b\":[[\"6687268143658731035541380359358188679148785443443569205934982468118877185640\",\"3571780016678771042377565517376697140921986736637075542939660314033659748794\"],[\"7946856780937832434339602666246485932261916644913354845313434657065857510781\",\"8802135569246081247498242505336680242720989800015162014665977667697915241498\"],[\"1\",\"0\"]],\"pi_c\":[\"3269786326785973823691194504606638936778933640772042112500321347778674042237\",\"10264868247957140944313298993291548358821068494690194211940540651810829926738\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "13294961411453839773564491536752009589902690318016736666092624853678685513858","3177631847785189443921604495165856561717745488619503118934586405578139020897","1" },
                        ZkProofPiB1 = { "6687268143658731035541380359358188679148785443443569205934982468118877185640","3571780016678771042377565517376697140921986736637075542939660314033659748794" },
                        ZkProofPiB2 = { "7946856780937832434339602666246485932261916644913354845313434657065857510781","8802135569246081247498242505336680242720989800015162014665977667697915241498" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "3269786326785973823691194504606638936778933640772042112500321347778674042237","10264868247957140944313298993291548358821068494690194211940540651810829926738","1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        AddManagerAddress = new AddManager
                        {
                            CaHash = Hash.Empty,
                            ManagerAddress = Address.FromBase58("YanBhpryvqf9RcFVapi1vhu5fvCzoVeT8x5AbRBvTNZKsQJRf"),
                            Timestamp = new Timestamp
                            {
                                Seconds = 1721203238,
                                Nanos = 912000000
                            }
                        }
                    }
                }
            },
            ManagerInfo = manager,
            ReferralCode = "",
            ProjectCode = ""
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("317eee9c80733c7efbb819b7812152f137373baddb7a182480b193f754157127")
        });
        var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos.First().Address);
        return holderInfo.CaHash;
    }
    
    [Fact]
    public async Task<Hash> CreateHolderWithZkLogin3()
    {
        await Initiate();
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Gauss",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7") },
            VerifierId = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8")
        });
       
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Portkey",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("2mBnRTqXMb5Afz4CWM2QakLRVDfaq2doJNRNQT1MXoi2uc6Zy3") },
            VerifierId = Hash.LoadFromHex("0745df56b7a450d3a5d66447515ec2306b5a207277a5a82e9eb50488d19f5a37")
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Minerva",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("3sWGDJhu5XDycTWXGa6r4qicYKbUyy6oZyRbRRDKGTiWTXwU4") },
            VerifierId = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9")
        });
        
        var manager = new ManagerInfo
        {
            Address = Address.FromBase58("YanBhpryvqf9RcFVapi1vhu5fvCzoVeT8x5AbRBvTNZKsQJRf"),
            ExtraData = "{\"transactionTime\":1721699872051,\"deviceInfo\":\"I0tgT1k9YDMM2g/UQM38NXDemU/++UkaOUWcZZn77thJrMIZp7/A0jvHrelAXRsM\",\"version\":\"2.0.0\"}"
        };
        const string circuitId = "0999c81d5873bc7c3c5bc7e5d5e63be4d4ca91b77b45f9954b79e1d33499f25e";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e191968da0238ca69e0c34d60dac3a405087da1e8dc616efb15d5f11a78d8c6bc042d1f5500d204beb77cb818781594fd8e03f60c94a70bd5271e67087c7f5d60998200000000000000b9d5c90a2d66af4553592a75e174cd26cb96df1e8a3a8ada1bcf5e5b0af92f89d57146f59aaa6fbc8545a89c7239f0657395a904611efb35a6af5ad73ac84f05f492521492441937d15ce15eaf1f4ca7bf9750092540656c091be60fda017e0f16506844aaf8fe09a2c2ecb972f590770d7c5b092de869ab52c3d171915a4481d19f3b3f26445c4ab170a457fda87ffee6045b99efc105476a74ab2816aa262bd6f925026899ce3b993eb32e63f2bcc35de660e528bd23089d72bc37eec19d27a63ae4d0c5b9e4548d37d8de375415c52761f0889c670777e5928bf3b2885420fd4f3eaa7b7c6c61ee4d517a1450e2bab0afcf54eb23c26fc4cd519330842e94e978bc386576b72a15d26b7e6253695672ac27510fc04cf2f654469b0fee5fa3d31e48253e77e84eb54c08061de2f7e6d0f7593928d486c0ca528f21eb2464aeac1c33bb40229cf441ab139c5b1766b716f85a74d42f2b258bab04ae44a65e2e702531e17d0b530b94b64fa0d99157e572d75d3d6413ec9193e24966698633aeb3c4e1462f1081955e13327babd7aa7bdc71d15595e95bf7671b0fa1cf92842b4663e524f6bc47bf08534ce926298d3d8c5c9ca34274eec4b47c292c17affa95df3735872741a33a11f4e3a10baef57c91a124fd6b4addf76dd63de04b9a9f110e19dbd4ba85cbbffc46c67af99aa11e98ad97b4768e9808137b298bf1e379034a2222f2423117b551443725ab8cb401f47d7e2f5a4aedea3587acf597aa880433cd76d9b2368b822841199b7c0bc71dbd96b11dd46d89ee2ec15357e1ab749dc39f4ed765e520f05af4823158e3d31c8e711a755186bcabce0373bad2850295c08fb96d3783af13038094d9172f7579aefc245e28beeb0d521f4d55b8b29c01e8542fabddb6881026db25712ffcdc0b1a5069d57df9506e44a7bc7dfdc2be089a12fca4c7522e20eb6981910b1870ed1e859afdf59913a6ca3afb9b41ce88898855be268f9b7d0900362b6eb3013c1a5635c3df0006166fceec5faac2c9c227967fc6f62b3d2b51902d7516c5a548a213238fa869240607281b84debfbb789499a49665ab3a199353e656d399913add901ca5dd10895c37038337520a6cf10adfd4da0fa959850f1fc2d7e900816ed0331821a5a44a377bd38d54a6d6c63c1d714ad1709099ed91348791f19c0a9c117e25cd10bacdca3ecb97a8f890294696283c687bae5a8defa3b778fe3b8b20c9485dfa342b173661a425ed29c6950a1056c9ca0c7cb445b14d8a427d22839776d705f4e8f2a4d2a96390f3188b0769296dfd9c16303170590f2236cf38bc28e4d01b9def24f78f08e933adcb09fb56a6832e47ffce005524ae8a0d9a108cd1e9ead6b880689ddbf827067f136c46aa2d458c285815cd55e0136400bfc691747fec7266b94105c3b65fce953aac9285ac8eae30c37f017ed2ca359ad59453dc21c6da3a4f3d985a6d225030cb3ef47ca01a1a0aae68b24ee9ce06ec204a69d24394b2eea7c7e678fe5bef892f1080d4a02609be6c412c09c96fbd267b5e6cf31638eaac909001e33bb1edbfafec01d00dee0f46bdf1b6d1c7a7df3beab9b2384373af2ff6b71f56d7e9b4718ea00d15af03becdcb50b0b714a41863570aaa9b4c1fb27fc4f401b9138acac34ec1370e9881a169afdaf8eb41c01688b929f1a94bf59f20e0490f04ab0322f5a1b7ea678b9e8dc2f93d63514f8638f94067cf1a6011cf2b17c4fe13f2654e38d3702b6d8b154e6c4f9f8cf66c65c80df5c9f9f218d2e063fc1d94455b60a6636399ae78a365d9e073900227da1de339abb6b2fd449b931a251719fd75c29e9728572ffb9c0cececaa1909b5bf9c743756e7d6e54543b1bab8cbacefa4658388a1d659bea86d7a9d7146bab5ec4aaa061503292e69a51527d2f9c128a607e08c5ff8b62eacc171fd2d6d9c39ac6835e4b919519b20b87a655a23f04fa3812b8dc789fb41a79995cdc6fec7de6924f538d45654fc13b82c14e69b0ea64acbe167e396ab8580a8e4917647d4978066c9cafb121197a987b4422c679f6c638fb35d6482e82526a2c5945f2b24a091854bf001a4d10de23db161428b370e31a3fc84705b223223a2874d86421e4f394b636ea55e49931342b93d0ea93c9b5dfe9f840606e98f15dad24a71d9279c803eda114ad24b3fb32c1fb188a3c43f327495488c891cefa33109ee9aef3f648e757f8200d362d48ec2b2edca82259a89fb8769fdbe7a27ac53c6275f4d9b97a4f245f187451a55862416c620a117ebfc6cf09fcf6158968fdf5312f9264e4fa9524da797959d604f0871a57554dda15b1d6eadeecd1adfaf94eee6ec161732641464e46437e581c1c24f4e60739f42ca27a18b5bf068dbab5f3db1f1c10c40f9583f35e4f6eedbbbe403ee060fbc33990a1bffafd89ac2ab28b508881ea293a7d3ebc8949cb1b0866a61e2a397a4b1b77b0a7792b91da9065824ad39cf585d9dd9f5c969e56fb5d2e822ca8c9bced6755bf71021b8013b07c56a5fd2b8823e2c34ef12feb2cf05bf95d8e0a884b1a06f66de8f8b24ff5c22fa38242c8b9816bbcadd1d9658a660ec06adf8241c0c553a4c124eb38af49ca452a78449903709e6616ad1466f14a2e516a98a6183ef9245af6aa8f2d44131a5cb3e0eff9546b02a30f05ec1f34d040a825734dadc3c681e097cd07daff6b9a2320f6ad473ac7ac9156c8d8c6fddc4dbbb55331005897ace755a1f7eb11d3491c9dd160cee992860b59193cbc49f609d4c593f50ae17f100032928cf49edca08d2de7fdca17bdd825985d1170d554449520f16e4f7458b543b6e2ad92319421d6c203cef59ec3f30a9e7a13f80400d5ab407e3274886e2d6cb958cc6b28858093f1e9da4fb61db15326494cee47d7b415281798d91d32a98663815d1fd85dd8e7256bbd04fb1ff9511ba62cc4521d5beb99935ae4905e3685eb226a666592c0526452a9ae3dbfb190a592881d99f7ac5f51cc509db8da0b3b8e2ea317e09d495c0f76abbd560515078bb87a34a2c0ec582919c5c9ed1dfdd243b2c258e0b7315704012ed188e740c4eb9e0372e0a40268ccdc8f951fa35ec30d12e642ef78ba9fe955f70705de914f5921589066f35a461282924e4d9c70b59cfebd7234331823f3d6cc6915ffff558ec695b9491426bf81aa07a62b5e888edcd3db01601c8021bcc8bffe4796d837b243fd31f98ef85ec20396cfd544fd023b87bab90e4e7ae34080a404634ae577d209029e17c4ede107d00fc9c0c4d49d8f33962cae45211d65a49465cdebef13ce5e07b4e8694ab64cb987c80f5aba52ed713f5e5e1d08ba14bacc4b470c7d2ab41aaced8af11c8ab2410ff6ca34e7d07e5a79aa3c7461636fe7a792629d6a44604f378b65329fbad11f4c7888e4a0797279955e3b6f908c2704131bea74ba8ff2a09e2799c673199c5362e397a0b111be39e4861218b826d2421d69100af4c546279f356d6f7dd568b8dbd70a2cb857e16a87256499f8c12acdabf705d2ea380210f1adea9d2fca5d25c7db5d11c5c1f1350ee89cd4120d04d7d984a291e62d954db0a8c043b5bfc3a4d60b71d3ab1c077b79d759de3851a16721dcfb1cf077c178eb5b367736cc1ef82266ec295708a8f600fd9fd2011ed4b267e3c7383280642687dc110b03dadd9fddd4ed9439a13372cb620b85a08ca9ad3e488d40c8dcb55bb4c8615cd869207fab4b82aebda94782509ffb9171772a7c4c551e67b5372e8747f6ff3eaea833a241b955c12fb611d0e51d1090995a66eeb759cdc45ab1b6299df8187d4cdb43779dc507829a71d39f203d374d3173e479ae0a62ac73d12e6bc3d3ce66d315e4b66b9f212c44c8077cbe4d890a106c8dc6b183d3f64a6afdd74ddc8ab4ce8a2b0737ab76d278d101b87a37b06c80a20bc5aa5dc096341f6ff0d7d66131857d3d07871ddbc9b7f30d171594ca53782eb7386b7a838d28f0f8686460257f8023d819a46150294d391f91dce49ddd4abbf70db3546b8ca07df471c06f7c2934424d01d457e9b7b17b5d1fa822dd79717c40b70301c0a18ec440a12f73e9acbe399670b89793629d99a86a32a3add681593abb93d346a139619aa4eca69dead3bfaf7838168ea67cb67f57502ab30aa9a674b984f77ad724ae9239c0d3b8e375100048d4b6fedb9e885d225bbb189a786339dcfbfefb2d284c3654ec1e126f1d7501db1ac32bfaf70da52f380a435048e57fdae166cedef83c9dded33e9197c4837890e27fcdeea9621c52a507de2f911cffbc24d700f0bb332903c9f5cf51be170c786b08d7d8f58bef7b6b3adb38124afdb61e71eede009c66d8a50d1cfa66aa4afe5b026406fbf985c3d8e78ea03ab664969010bac42bd6a450bccf89b75e384e4cb35c958a6dd189655ce86e31c11ce9d673ddd0ab26d2a423b6acbd02b2b2dee7514af668554d5445abf27acbe0e28f6c194902250142275196c0ed149a147e1ea232527c7ddcc8ad218fab4d423d903a4e26f1faa015e9bddace217c3554b29a15d91292179773dd90008d526acf74030d0b9766cd6df005d2d02544c1310467ae05c17a3b95ea65dd6f473e5a68dbcdd06602051eb58d184c3c38fabb2375a95f0e1fd8f6823b0bee85489652c600892e30df0c8d02063af969be156544a048352f0513191704ecdbb322d5ba6e853d20ff9a4a6987a9e2fccd45d61ee9408d7c19681fd5a89ee2bb6c48e62985fb59911f3198f5e0764748349feadccea39073fd7197ec50cac97f08d8d60a720a211f0cf212066d231b59bf10ee325a2b0ead8beede4751f5ca282f57cbe1331f098e1414c54985af85223503ce0d28afd174d593c858c82129365022500a8f33d916fe8cdaa8c5eed6055747684a90bf4b59c7eb5c693feebbf5a63b6c91d4adca4069662d2ff29a1dccf26515353047891e5d7e41eee45816e91a03467a412b9e9073d5a324dae03397d4e3e70564ffc812e3fb6a3f667de06dfe31be3afabefbdc23c6873579b6b877c3fb2d368d0748d983278ec3a0588316482b9e70d1bac468dbd6eadca979b826251250eb106e682d5454e44ec69a12b273493cc1771cb5ea3192e1ab9b2a7e0bc4c917c71c5eb0f085a40e66daf6743efa8fa4a99be6188e963fa797a3d8b4a2a458e2592eac902b357fb33c59308716a14a104a84373c6b784d38967070d0c3337d1c349a4db08ae70a6234b876cb28b14279c93b71f1a9d2336f36d5ff19cd56ef3c811f2c76fa5cc49b8511af7f88612fe3727568cb00be8f0bf86aed77bb58da1a22e932da4c6aa666bade4032c72afc95ea693ceafcaeb834e8a974aada5053838fef994d312317f33a8a7807863b475a10fe1ed06931cec1933bab561f408be9848bb5c33a69df0025713fcb375dea24c06c99dfbb8c2da2110beb7ba6b79b437289a20f8bfc8229b958b99c7e453c95da0103dc3ccad1ff92456d8ebf11d4013bec978bc955baf0c9637a2915b8d06b6a84603a7cc4baf8aae442a0cc60ef8f9e60776a453d8471747a3adc07df6ad51a22cc15cc8efa0871426cadf68f23e5d93029f68847d4bcffa6c347ac39ded28043cc0c71211a701208478a73e5cabde4099713e15f4e5d463b02d7c6e91d7bdadf989bb948e769deef06d7754069b7ce15b3d7e2126f64164ab6b41abf1eca0a442740428cf87b221ae30cb1a419aecbc38f0e289bd5ac76844d13f5c07624929f3119d142ed94db41abeda4370cb91eae1942263d7341946452d0f5e88dd292315bccb323b34c36d71fb336425f905261ec305dd09a80c3f98a1f099056d34a9bfae0c0fa8551511e4ab998a96d8dfe11a4e6f3d2bd95c5edda8a746e52e2127";
        await CaContractStub.AddOrUpdateVerifyingKey.SendAsync(new VerifyingKey
        {
            CircuitId = circuitId,
            VerifyingKey_ = verifyingKey,
            Description = "test"
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Type = GuardianType.OfGoogle,
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs"
        });
        await CaContractStub.AddKidPublicKey.SendAsync(new KidPublicKeyInput
        {
            Type = GuardianType.OfGoogle,
            Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
            PublicKey = "zaUomGGU1qSBxBHOQRk5fF7rOVVzG5syHhJYociRyyvvMOM6Yx_n7QFrwKxW1Gv-YKPDsvs-ksSN5YsozOTb9Y2HlPsOXrnZHQTQIdjWcfUz-TLDknAdJsK3A0xZvq5ud7ElIrXPFS9UvUrXDbIv5ruv0w4pvkDrp_Xdhw32wakR5z0zmjilOHeEJ73JFoChOaVxoRfpXkFGON5ZTfiCoO9o0piPROLBKUtIg_uzMGzB6znWU8Yfv3UlGjS-ixApSltsXZHLZfat1sUvKmgT03eXV8EmNuMccrhLl5AvqKT6E5UsTheSB0veepQgX8XCEex-P3LCklisnen3UKOtLw"
        });
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                Type = GuardianType.OfGoogle,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("7cffb8aaa452a13a4d477375ef25bb40c570476a76ab41119a94d7db33c440a9"),
                    Signature = ByteString.Empty,
                    VerificationDoc = ""
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.LoadFromHex("61ede365d5fc4731f0e4631f360c920585915788f3ab1487cad65d738d670516"),
                    Salt = "4f8f7469d8d44351acf2055ab491a1ce",
                    Nonce = "3332b44a8f3ab7a46960cf78694757df03baf47fb93471686fe191734a77141d",
                    ZkProof = "{\"pi_a\":[\"13653499308465311314779681904135208898008255617934243379245733734939531085902\",\"8524727878079641865631718080395754120964110120366957073582010926658219780180\",\"1\"],\"pi_b\":[[\"18334343753232689027626120752218101962543940167578981953362331577829102038700\",\"21193665004857277374106867719745463881968202698147510401507989999797237533905\"],[\"6231647031167524356442788723802243477990558566624701270205005506539953777294\",\"2632410410715515694628601778518667621866966633328916138132231359437582197339\"],[\"1\",\"0\"]],\"pi_c\":[\"7395866727068505445227711086544101826091525908317162021966999480069819091479\",\"4041550074265117864636446247441465403515671461744228230057708860094595125205\",\"1\"],\"protocol\":\"groth16\"}",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "13653499308465311314779681904135208898008255617934243379245733734939531085902",
                            "8524727878079641865631718080395754120964110120366957073582010926658219780180",
                            "1" },
                        ZkProofPiB1 = { "18334343753232689027626120752218101962543940167578981953362331577829102038700",
                            "21193665004857277374106867719745463881968202698147510401507989999797237533905" },
                        ZkProofPiB2 = { "6231647031167524356442788723802243477990558566624701270205005506539953777294",
                            "2632410410715515694628601778518667621866966633328916138132231359437582197339" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "7395866727068505445227711086544101826091525908317162021966999480069819091479",
                            "4041550074265117864636446247441465403515671461744228230057708860094595125205",
                            "1" }
                    },
                    Issuer = "https://accounts.google.com",
                    Kid = "f2e11986282de93f27b264fd2a4de192993dcb8c",
                    CircuitId = circuitId,
                    NoncePayload = new NoncePayload
                    {
                        // AddManagerAddress = new AddManager
                        // {
                        //     CaHash = Hash.Empty,
                        //     ManagerAddress = Address.FromBase58("YanBhpryvqf9RcFVapi1vhu5fvCzoVeT8x5AbRBvTNZKsQJRf"),
                        //     Timestamp = new Timestamp
                        //     {
                        //         Seconds = 1721203238,
                        //         Nanos = 912000000
                        //     }
                        // }
                    }
                }
            },
            ManagerInfo = manager,
            ReferralCode = "",
            ProjectCode = ""
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f")
        });
        var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos.First().Address);
        return holderInfo.CaHash;
    }
    
    [Fact]
    public async Task CreateHolderWithTelegram()
    {
        await Initiate();
        // await InitTestVerifierServer();
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "Gauss",
            ImageUrl = "https://portkey-did.s3.ap-northeast-1.amazonaws.com/img/Gauss.png",
            EndPoints = { "http://10.10.32.98:15002" },
            VerifierAddressList = { Address.FromBase58("5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7") },
            VerifierId = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8")
        });
        var verifyTime = DateTime.UtcNow;
        var salt = Guid.NewGuid().ToString("N");
        var opType = Convert.ToInt32(OperationType.CreateCaholder).ToString();
        // var id = _verifierServers[0].Id;
    
        var manager = new ManagerInfo
        {
            Address = Address.FromBase58("2vYNGf6FbCDbgeqY88nPukvZ7hEBnorF1zgrSmCGXAWmQkLKmh"),
            ExtraData = "{\"transactionTime\":1721900516695,\"deviceInfo\":\"WovVCxEbI2KMQOOLqg/5H0dwruIol8WHe35xWRs29f+E7D2XL/iiKJHplQW/J5Mq\",\"version\":\"2.0.0\"}"
        };
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("017dd687bc648bbd1dcfc28ac34f2ab4a1d1d4b08500d8536d59b46d84ef72d1"),
                Type = GuardianType.OfTelegram,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8"),
                    Signature = ByteStringHelper.FromHexString("a2de07020467ff63e3eb92469a90d9b289febc71a661cfbdf86bb809732ebe2d1bae89d779dbde96ac3f2c50562e18dec5c51b8cc2e2dbf795902a66c5e9606200"),
                    VerificationDoc = "4,017dd687bc648bbd1dcfc28ac34f2ab4a1d1d4b08500d8536d59b46d84ef72d1,2024/07/25 09:41:56.645,5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7,c5e3ca304208084b966d2d5dc88139f2,1,1931928,9001df3b693ff20a1378e53399b3eb8188c2b26583d2fa8800b627d7ae65215d"
                }
            },
            ManagerInfo = manager,
            ReferralCode = "123",
            ProjectCode = "345"
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("017dd687bc648bbd1dcfc28ac34f2ab4a1d1d4b08500d8536d59b46d84ef72d1")
        });
        var invited = Invited.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(Invited)).NonIndexed);
        invited.CaHash.ShouldBe(holderInfo.CaHash);
        invited.ContractAddress.ShouldBe(CaContractAddress);
        invited.ReferralCode.ShouldBe("123");
        invited.ProjectCode.ShouldBe("345");
        invited.MethodName.ShouldBe("CreateCAHolder");
    }

    [Fact]
    public async Task TestHandleOracleFulfillment()
    {
        await Initiate();
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Issuer = "https://accounts.google.com",
            JwksEndpoint = "https://www.googleapis.com/oauth2/v3/certs",
            Type = GuardianType.OfGoogle
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Issuer = "https://appleid.apple.com",
            JwksEndpoint = "https://appleid.apple.com/auth/keys",
            Type = GuardianType.OfApple
        });
        await CaContractStub.AddOrUpdateJwtIssuer.SendAsync(new JwtIssuerAndEndpointInput
        {
            Issuer = "https://www.facebook.com",
            JwksEndpoint = "https://limited.facebook.com/.well-known/oauth/openid/jwks",
            Type = GuardianType.OfFacebook
        });
        await CaContractStub.AddKidPublicKey.SendAsync(new KidPublicKeyInput
        {
            Type = GuardianType.OfFacebook,
            Kid = "2536a84ba9d727cf0f8aac3d06c5777bb31ab6f6",
            PublicKey = "s8J70aIzklILWqLmqaz4I6VC9G4JRFW9yDYGq5rgAIKW_UGshZjqbEYwzznlS3R56BqfMBb0fVx537ubc93dHQfCROy6nOXDh4wg66tZyFSUWzM9Ys87B35EQ2RN0xdYzN4pB2V5dG_-RAyy3hJl2CKO21D_bkKkXHoWdkZgLLnrF09bPD_rVWOAEdoHgJIbcE_7yUePOT6Nsg1b61qGFHM7iqIXQq13GsIUUCFxwSvpYJ0FaOaLDa-Lpb1LspeYLCGb2lw8EjvQg1_PEmVv_GRwPtxEOFyirt8pgaByD0d6iZclTkZfensB-G7KWTjLPA_W3mBL2JGB-UJkN5Qpkw"
        });
        await CaContractStub.HandleOracleFulfillment.SendAsync(new HandleOracleFulfillmentInput
        {
            RequestId = Hash.LoadFromHex("1086a9a40093dc0a802deb830b9746343e9d023246037e41b61550f76508e19e"),
            RequestTypeIndex = 1,
            Response = ByteString.CopyFrom(Encoding.UTF8.GetBytes("{\"keys\":[{\"kid\":\"2536a84ba9d727cf0f8aac3d06c5777bb31ab6f6\",\"kty\":\"RSA\",\"alg\":\"RS256\",\"use\":\"sig\",\"n\":\"s8J70aIzklILWqLmqaz4I6VC9G4JRFW9yDYGq5rgAIKW_UGshZjqbEYwzznlS3R56BqfMBb0fVx537ubc93dHQfCROy6nOXDh4wg66tZyFSUWzM9Ys87B35EQ2RN0xdYzN4pB2V5dG_-RAyy3hJl2CKO21D_bkKkXHoWdkZgLLnrF09bPD_rVWOAEdoHgJIbcE_7yUePOT6Nsg1b61qGFHM7iqIXQq13GsIUUCFxwSvpYJ0FaOaLDa-Lpb1LspeYLCGb2lw8EjvQg1_PEmVv_GRwPtxEOFyirt8pgaByD0d6iZclTkZfensB-G7KWTjLPA_W3mBL2JGB-UJkN5Qpkw\",\"e\":\"AQAB\"},{\"kid\":\"c87192bdd4cc38bc02f6d45c6712ddd8daecc72d\",\"kty\":\"RSA\",\"alg\":\"RS256\",\"use\":\"sig\",\"n\":\"vE4yTlS8lhBPWd82Rpr9znyZbSIirkmbYIcyn34Zx8GpZe8_peXUBg4wex4TpqFrKNCiT5lHcaBiQcFe9CywryxOlVHmrq6ds9IEHR36swh3UwYt1L-YujV36VG-Ty6xgvNRmCcAfe-kV_ZZ2sXYpOJMO_fwxSSBwRUWR2hbzwmqpiJn6FT6P_eh-osE_Tr4BFd7bBBxpbyvqJF7CHdX51Lv6mOssYJeDje226LbuvcsoX77mPFa45R4K44JbBaSTanFpBzwdrdZtpF7HDQ-v1gFKMCcF86glyysPhG6C9WniMVlfDJ3q489wXKeQwJv207WOzWrLsQdM7NlsynPEw\",\"e\":\"AQAB\"}]}")),
            TraceId = HashHelper.ComputeFrom("https://www.facebook.com"),
            Err = ByteString.Empty
        });
    }
}