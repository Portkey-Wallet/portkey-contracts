using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
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
}