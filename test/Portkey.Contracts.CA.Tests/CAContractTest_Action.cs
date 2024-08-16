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
        // cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        // cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        // cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos[0].Address);
    }
    
    [Fact]
    public async Task<Hash> CreateHolderWithVerifyDoc()
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
            Address = Address.FromBase58("2ZZizSo73reZ1bGPVsnPCy4xdEvtQRkLbp6roKb3Zg7FPW1bRo"),
            ExtraData = "{\"transactionTime\":1723189542801,\"deviceInfo\":\"4HkZ1a5DK13vjLY3V7JDDW2kFc8SPeFFDQlL8gf5Bx7hMgQQX9aK0Fq+Grr0qswl\",\"version\":\"2.0.0\"}"
        };
        var createResult = await CaContractStub.CreateCAHolder.SendAsync(new CreateCAHolderInput
        {
            GuardianApproved = new GuardianInfo
            {
                IdentifierHash = Hash.LoadFromHex("b2e76ef854634a6c333c30be162072c4f01a7b945ca39026e4bdded57ecafe78"),
                Type = GuardianType.OfApple,
                VerificationInfo = new VerificationInfo
                {
                    Id = Hash.LoadFromHex("e8c0652f79ef46f4775135ab146708bb14e806844dde5a680e4be3f96d46b6b8"),
                    Signature = ByteStringHelper.FromHexString("b707947031b3f4ebb45b23f8dee8ecfc8e5244567c02c60af5575e09f519b0973d7fd7484b42ab87824d7f3b3420b7a46b05703f49bdd9b292caff880bd901d500"),
                    VerificationDoc = "3,b2e76ef854634a6c333c30be162072c4f01a7b945ca39026e4bdded57ecafe78,2024/08/09 07:45:42.084,5M5sG4v1H9cdB4HKsmGrPyyeoNBAEbj2moMarNidzp7xyVDZ7,d795b1acb254d14e9f473cba7239d390,1,1931928,7fa8c0a540a3c667530213dc5451d67854d4815842e2089fc7f974e4b83a8801"
                },
                ZkLoginInfo = new ZkLoginInfo
                {
                    IdentifierHash = Hash.Empty,
                    Salt = "",
                    Nonce = "",
                    ZkProof = "",
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  {  },
                        ZkProofPiB1 = {  },
                        ZkProofPiB2 = {  },
                        ZkProofPiB3 = {  },
                        ZkProofPiC = {  }
                    },
                    Issuer = "",
                    Kid = "",
                    CircuitId = "",
                    NoncePayload = new NoncePayload
                    {
                    }
                }
            },
            ManagerInfo = manager,
            ReferralCode = "",
            ProjectCode = ""
        });
        var holderInfo = await CaContractStub.GetHolderInfo.CallAsync(new GetHolderInfoInput
        {
            LoginGuardianIdentifierHash = Hash.LoadFromHex("b2e76ef854634a6c333c30be162072c4f01a7b945ca39026e4bdded57ecafe78")
        });
        var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos.First().Address);
        return holderInfo.CaHash;
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
    public async Task<Hash> CreateHolderWithZkLoginPoseidon()
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
        const string circuitId = "2146ef265bd943d9721f6d05b3d53210d56287f15dc6290eb2749f273d5798f3";
        const string verifyingKey = "c7e253d6dbb0b365b15775ae9f8aa0ffcc1c8cde0bd7a4e8c0b376b0d92952a444d2615ebda233e141f4ca0a1270e1269680b20507d55f6872540af6c1bc2424dba1298a9727ff392b6f7f48b3e88e20cf925b7024be9992d3bbfae8820a0907edf692d95cbdde46ddda5ef7d422436779445c5e66006a42761e1f12efde0018c212f3aeb785e49712e7a9353349aaf1255dfb31b7bf60723a480d9293938e196cc26d588900354f9884a91e3647789bf40b390d5c1195d26779f9a917ef1a0d78d680c86042d3d6ed17d8061ffc1aff43fb83a8ec320e5b539d4df54733a903720000000000000094c61d94dc92f1df324d9714111b6feac4403a8a6bd015e73caa5457d917f2807cb4f4026ad70e6b9bc26e3e2123325df3b4c1fd26f7874b3462ea2345effa9b193a1ca6869398fa23759e3686d65bcb5602ad82f157c988f1cc9a4cbdf18b2e116404aad9d8c0c47cbdbb4631b1749c226c7863a4d00d7c74c6c49832850a86cd7fd55aee5ba53d1dfe9cc673d0c62bc2595b381eb428eaae8dc7a8c6267e0fc043483e75b9da6a04753b31bec1b79b3e73ddd92109d01b2fc88193f8e05f8ee8c6f1740beaaab7f369af79ba7d40e438726e84a20fc6bb4cfeaee5d6a9149b63ad4870ca8ec1025013ebd7e6b8c03a38bf940a4e37a0074a95a61d2c4101a47bbdaed88a8b1ae6ae398bd99ff8229293badd292468cb7f1e720b78b6bd9415053c1174b5a206ca03a4413bb310c76d44c6791e7c1f49e54ef5b3b80e8be8019b817014a67f40a84668f25b2498b490b35243710f323c2b2fa52237a0f21c18e717239a8c57a90fbbe246627d4f3a4db6c523b82174bb1a298b18685b3b282ec8a73107d896c7b568ad3459a59cd03600fb52e9aba7d3967e3d0acc86b9be0dc3a30677c76d0ea1b1aa4337b29c22cc399dbb27a52509e41c9a5b167cfb3b96125d8d0fba149efdff598d37e3af8603b56bf8c2861db1c601ea06619f80bc8be62dbf3abe28ad8710e0e32e99698ea6dc55aef9b2458ef8f6a91254edea3426adf57f0daef00c698756269774f9ea608b5bdce8af0d70ae68f7f0f765bbb6a04c685a83644bf85de0223dcdbab9643075b3ff61fc197d0bd714a86a20daae1fcb5206ec2724c87998b00c521c3e6e2ef9f17b33432de8cb40fa9f07dd50112d9cd7ab70427f57e6c908fc39c3bd81ed632e8a0d6c5e3c109b8c4227497ba897cdc661869a5b5f3a70fe82ea11614bc391ba8ed27bc6c2187425b11954c56e079420e9cdbb0926ed8656ebe6ede8b634c5d84421a3863a19d95bf8f76a9f918e7fbac8a839d30ae49ca8d4cd564fe2f0d564592541c19a161352c3c12a38d4af7298767bb3446ed5adba34404f409f1b3ddaa2eb7a1a08b3a6848e15b4bcc22c068c0a8edb4708a4c65ad9845de9c72b9f0df394ec547a7618a19e26fd96c598bf551c9941cf72572a80f8306ee2a935f02cd6317331942f2c198be6d4631989f4f2a0561ae614018bae3a7bbbc8cd06527d0a2a7304734211af4416b364fb2cc694219c15305f8b4aa220219d4ab855da9381d58f3617cb94db9e584da270952cf23e8ca6efbd5c1809f9823bbe79fff186357e68a0a5772bec89fed496269593e92a8af93d9699aeac11d56c9ecaa96922698185358a79098c8d2a115c88a607a04e66674f1ed3e5af776aecbe0ae8555eeaae5776802ce04e6d5103dd8d1543ead7c86bc88b6b30759e0a131532aad3499b0511a9f90db30af78c5242e19b6180f22d53571590d1946828519aaf026a1567e939ad8ab152e73882279a2c2f564b4bb74046090b0b443c37366e99a2800cca7af370e1cacc9c28dcc5c4dc1e28bc22be3662d83b7c7a48bc4c6c0a06a9380d2e13b15569041599014a9119a3883e62a515b0609e055db50b19b9241de6d6cd92e72fc3517ebda78c3b6e49199223a02eaf352c3a83f0c1157ffbcf7eebefa7cc1261d4242cc023c3a77aac292c82f67883911754a4e59673ec1cf59cd9100159079fbd32c08950762f9a7b8e42a44717c7be0c7174eae8fd36f6d05f03b053b18b0fb3cd907aef86aa8258ab7eba8f71335df2bea953c64bce1164cd9874395b77f1bc49a936ef4e9eb761979832003ce343e2ba746877fc629addc422ab710571db9866fcd8e9240ca371ab54331fda6aa67a09ce92f5ca5b701b81292c0d7eed0eb37f4bab4ac947bc1481ef477c7e205b41e6063a0ccd34344863361fd995fe8b950589270549bbdadf1afefe8f85226c4e57415f8f2c412ec1d68b13d2c11a2da7181aa4e70b75fd5b27c8ca3e0d89dc49e8d04ce8b722532be82f12ac8b115741137326cc57f79e6d0e57cca27378b4b53c5723c2214c3feaea31a45913acae3a027e4b979e699f370b78a1ce9552c729e86da83849e4999677972142aad2639a92ac78500afed85b96ee05c2fb6e60255ab25c4f0b19378bf98040ab67d902468db4d4aac3d9d9fc0bd7faa44990c461453751ed703d057fe53fcd51215386b562c1a945455d3844affa1bd22f0a9c58b5d1d2cf7a4412cd9534c3f5052f467e806e339277a15e28af59b5a2e82c426bce372a6570f91714fcc82850a3ec67050b040df84027f93daff8db2a24a1c58a967d067693f0108aaa7065c1b8678c74cefb427d5f1abfd02b00b66b4347e9837ce7ab068f5becd1a06dca1d0d5c942ecb0a2d5e545b7dfc8a53e4fd67e01ee8767477441c04e947ffa73b8ccdedee78805ffddb6871a4a0a5cd69295f1c6173c50830062a1c31702ecda3901380140461498b27142b77380b3769124e90e12c2a6b0519a6f657c2e17d8e7a31986acb4ec37972b5d0d4eaa84e0ad2616a56c1e4d9cac35b832b87519b137627baf7c0dfaac77ac0a41b15a951d7ebb0d823d57a5bf7d22a4bd22780fccb28d4955fcaa0cf48883399c20901ef7619c21552f785be1f32fe9df7bb5212ea80eeffdb12e7ab9363eb730718074a7aef4e3d1c7a006dd9a022b197bd47a3765e9e7905ef23ffa771c842a6dd0f0c8eeb207d859ca9ff8523ff6ccca1684c195273df313bd70101e8e93b3b05241c41885653225cce163a143b281df59aca1b269dfe298133929f1c59474fa71527c9a59f84a92b83b9c44edb73afb6a8b6d5d547be0a215f598d037f9ba9b9153cc2aaacca6fdc242653fb7be0519296b709f5fb9343a7df01edbc7522385ba91e3af26126d443fa41670b48ac2bce5b262630ead3ada732800fce1169da08aa8abee5322cfb684db2e5fa3645c5af65f4ac9dcaa78da9a80708eacc9e51f1986e47a060135f01d5b02d86c589bdf1393cd29609ce46f4fe411f101a0d46218f6db159f86d9528ddb87e2e268647cca42dea02c2013097d5df48a2847e1977029be374ce044f3bcea1af5ded84c6d0d02ace4a2178a76be2ab274033d4527a184a62449be121ddaeba0af9f195f32a8443ed87acd51fc56e4e146b5b8649d311e8ba96c4eba92b2dfdad578e6b14a6e7bdd46cf7c4ff11408f953e42102f4727b895b5ab31d38a7c416d9c07fb3396d90553b1148468fd7fc09d260547e62b2d13c2b1f333f5566211033607c529b395ac70037031558e4ba1a9bc603f6edc1f8b8b39e4baf6ebdce02c3acf22ff779120cadaf31a15fda797d72a54e4d3990cea6c5bfe68ee6206b1d2eb5a211cdc2517ce155c71e18c4a84629ef6067d88027cfc774169eaf9a4a0cea0c2e07c26e795944485a975158e5d4e505076509e2dcd431a355cb47b2278d8a586d3b3d56a1f8e4f5707844e0d1ec09caefc7d682facd5bd6442cfa8725d2e4ee0149424102ebd4539a7d30924fe35916bfea9dc2a709b9ba28e0d9fee491b88131c9fe925ae9197db6f0e391eb36b617c004c4daa9aa40450cc00d3a6730ff1c0e243c413d99c372b7bad69cfaf60fcca8aef62ab68080a201624d9a33d830f55cdf9072985afa4f70d930a10c44e29968784c2afeb35fbd0b9afe0f50eedc2ad92c646448b6e7f3c2fbde672dbf0f273ef0f4f0931f51a9e19d8b24b46cd6f1b8ae002f1352bc57df93db6e7f016fbaea524938e006adb3ccca4fc8453c024a5aa3316c660698c88657c27217fb859c765796181bda12715d961910b173b1a737f4831216950ef0ca2736eed83b32759db906398e8a8ed10cf2b64f850b0fd3d5f3ef2e19a80be610e07ba7873450407cbb8ee959b53718bcd5e9bd16dcb5526cecd0ccb07a3c3cd3cfc2dc21b78aa43d398ec894edbb5f4c6670b074bf4673acd9e23d93f7fe187a83114fe7b6f7a719f210c9e06ea36e23987c6622d074b6d7ed9fa41fe3a99c0c8f044c1eb82f4287e95b41ce15d3d152133cb8f86377d897090020e2fe4faacf42e0e2f7d13dffef0941d94f087e00dee3bb14424dba2d8ad0d2ca1b9b49de7fad1ea9bf18b536dc4d7ce9c05b50eacf180d392260707fc5775bec9ce05d5054c4cf3a728450a3b30d875197062c11fbeee719580764f4d8f66f17afafb554834db82bb90c0d18ea935200ff8791463287fcf64073df16717a94aeb21b70ee5c82aede21f8b5f75db1c02b09939fd652b3a33353aaa2a72c20c9365a589b108e636dd71d65d61e04d1d460a8f985df3ef1875884e282d9d7f41866fe942ea89a69d51afebd0d9f3ffe8b80924e1ddc07f1449ef8552f8f346a1835970aa47c9a4054d702de467ab73a218943b0ecd5a082e621d9bef8500ff85e1f5e23d6598f3e67e7911f987ad9b698e18dc5258dde6bff404703998b6cad8195150c484a3daa30c0b4c8ff944b672cf97194eccecbc3f611c0523fb850375e99116a2fa3e930fac56970d6386748359a53af2a48ca211b412dbaa938bedfe3be309536859ca857792dcff674d350eda20af85e4cc39231acc21e70d38c74a2a7fcc465cd87edc41c06d08af96d5ea17a48e0f9a6d395002b074a71eccd30eb7fa589e29cd39152da3c93a55058eb405848fb8e1463673b85fd8f77ab4614460e1ad6eadd3c719736dd4a4d8f7f140c42a78836385721371d075f5322f9602978712a7f524c0688f7cdd28eefa93fa541aa080bea2533258f3269ccc5687760c1ae56b1b50a619fd878b0e742fb489ff93385f944db9bbcfb45babf73e1de73280f8d9052ba504b87a7c12ba49e73f968d801c1c364f556d45a85d8bec284ffbfe94a42f4e709d23645ec96be428edd89f651a8eb6b81f1171bbbebe89bb323046ababa90665c8d9ef0a145f6ab47298a715ab54fbc4068d4c44ef07b9d57d856672d68377147fe0444d64408af465552017dd53f233f3fa43b8907e205d2b7b4a388f3b1ef98bfa783c0144df1090f8804d38166ea9b9a4241167bbaed95edb06f5ead4e2871fdbc829cc7fdd0628422230fb9a0bf91f2a58a0691852f365c90264df31bb713fe0756e6fe9d14dfef61b5d68ed9e73f530e5cbbd66a68491ff62b5ad53ed67c2fae7daa7ed6b8fe89e1b";
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
            Kid = "4529c409f77a106fb67ee1a85d168fd2cfb7c0b7",
            PublicKey = "1crrYmsX8OVzrN9BTDD4RlVJDqSQIEbRby9ELqTmCpW1Qtt7y-pdmLPqlYG1ND5mprkTA83S7g_dcsxuV4wxK4_Vv5a8IBn86HfAX4VfCCOzqBYgACN6hlaffzPIWL1QA8yZ4w-D0fnN3xC5ULhtmtBG23qi__4yEo_FIY6irvbHrpRNI_-vjxFokm2X3ENP2ZOwgNhDIthwJo8l1KNbZa1riAJVcF86zWILQTy756hh8eH1Kt05wsGB3DeGPNV55zYv6sB2bzxARsVYAtCRJ8c28FYWwU8dCRJ70eJEmY4aKFOBO5g4fwYJlvMm9Le7qgAUH5-7wO52BayqXmqAOQ"
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
                    IdentifierHash = Hash.LoadFromHex("e5d12986c422e134e50057d702b11fdb5ee4d28d9e8418bf21b245a41d27cf5f"),
                    Salt = "9d3a7022ff719f4aab51aa6462e3284c",
                    Nonce = "3c0ab9bf6a78eba1c9d9fe3f981a47c27b581fb256889286725b1ec915a40d22",
                    ZkProof = "{\"pi_a\":[\"15410428057522242089988431889339252712080526510567711427846870901848537863238\",\"20089790999329802384757687171335911033318979626686007962106528973074379722245\",\"1\"],\"pi_b\":[[\"6751720525864235158652558660632217683960241404198847283128888771849769045809\",\"4400444751989915561865345016047056814117576004579189054232880960271488979640\"],[\"9915017570147477382054401387848723686781767260014286650313245519604218656247\",\"12698264773501288586748983391527249261005260809421838416990832662422589609754\"],[\"1\",\"0\"]],\"pi_c\":[\"1667547637419863986926423788031747996282137354836334286316466554835451723039\",\"10657168338151017688266690057860489590849634156404713100744859232960535885889\",\"1\"],\"protocol\":\"groth16\"}",
                    PoseidonIdentifierHash = "8925013748264870972389277816106291368786262941430798047098314223090922247527",
                    IdentifierHashType = IdentifierHashType.PoseidonHash,
                    ZkProofInfo = new ZkProofInfo
                    {
                        ZkProofPiA =  { "15410428057522242089988431889339252712080526510567711427846870901848537863238",
                            "20089790999329802384757687171335911033318979626686007962106528973074379722245",
                            "1" },
                        ZkProofPiB1 = { "6751720525864235158652558660632217683960241404198847283128888771849769045809",
                            "4400444751989915561865345016047056814117576004579189054232880960271488979640" },
                        ZkProofPiB2 = { "9915017570147477382054401387848723686781767260014286650313245519604218656247",
                            "12698264773501288586748983391527249261005260809421838416990832662422589609754" },
                        ZkProofPiB3 = { "1","0" },
                        ZkProofPiC = { "1667547637419863986926423788031747996282137354836334286316466554835451723039",
                            "10657168338151017688266690057860489590849634156404713100744859232960535885889",
                            "1" }
                    },
                    Issuer = "accounts.google.com",
                    Kid = "4529c409f77a106fb67ee1a85d168fd2cfb7c0b7",
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
        // var cAHolderCreated = CAHolderCreated.Parser.ParseFrom(createResult.TransactionResult.Logs.First(e => e.Name == nameof(CAHolderCreated)).NonIndexed);
        // cAHolderCreated.CaHash.ShouldBe(holderInfo.CaHash);
        // cAHolderCreated.CaAddress.ShouldBe(holderInfo.CaAddress);
        // cAHolderCreated.Manager.ShouldBe(holderInfo.ManagerInfos.First().Address);
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