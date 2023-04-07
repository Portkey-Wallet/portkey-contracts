using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]
    public async Task AddVerifierServerEndPointsTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        });
        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.ShouldContain("127.0.0.1");
        result.Result.VerifierServers[0].EndPoints.Count.ShouldBe(1);

        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        });
        result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddVerifierServerEndPointsTest_Fail_NotAdmin()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = User1Address
        });
        var result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { "127.0.0.1" }
            });
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task AddVerifierServerEndPointsTest_Succeed()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        });

        var inputWithSameName = new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.2" }
        };
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(inputWithSameName);

        var inputWithSameNameAndEndPoints = new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        };
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(inputWithSameNameAndEndPoints);
        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.ShouldContain("127.0.0.1");
        result.Result.VerifierServers[0].EndPoints.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddVerifierServerEndPointsTest_SameVerifierAddress()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { User1Address },
            EndPoints = { "127.0.0.1" }
        });

        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { User1Address },
            EndPoints = { "127.0.0.2" }
        });

        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { User2Address },
            EndPoints = { "127.0.0.1" }
        });

        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { User1Address },
            EndPoints = { "127.0.0.1" }
        });

        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers.Count.ShouldBe(1);
        result.Result.VerifierServers.First().EndPoints.Count.ShouldBe(2);
        result.Result.VerifierServers.First().VerifierAddresses.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddVerifierServerEndPointsTest_Failed_InvalidInput()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        var result =
            await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
                new AddVerifierServerEndPointsInput());
        result.TransactionResult.Error.ShouldContain("invalid input name");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = ""
            });
        result.TransactionResult.Error.ShouldContain("invalid input name");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test"
            });
        result.TransactionResult.Error.ShouldContain("invalid input endPoints");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { }
            });
        result.TransactionResult.Error.ShouldContain("invalid input endPoints");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { "127.0.0.1" }
            });
        result.TransactionResult.Error.ShouldContain("invalid input imageUrl");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { "127.0.0.1" },
                ImageUrl = ""
            });
        result.TransactionResult.Error.ShouldContain("invalid input imageUrl");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { "127.0.0.1" },
                ImageUrl = "image"
            });
        result.TransactionResult.Error.ShouldContain("invalid input verifierAddressList");

        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = { "127.0.0.1" },
                ImageUrl = "image",
                VerifierAddressList = { }
            });
        result.TransactionResult.Error.ShouldContain("invalid input verifierAddressList");
    }

    [Fact]
    public async Task RemoveVerifierServerEndPointsTest_Failed_NotAdmin()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = User1Address
        });
        var result = await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(
            new RemoveVerifierServerEndPointsInput
            {
                Id = Hash.Empty
            });
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task RemoveVerifierServerEndPointsTest_Succeed()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput()
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1", "127.0.0.2" }
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput()
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1", "127.0.0.2" }
        });
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var input = new RemoveVerifierServerEndPointsInput
        {
            Id = id,
            EndPoints = { "127.0.0.1" }
        };
        await CaContractStub.RemoveVerifierServerEndPoints.SendAsync(input);

        var inputWithNameNotExist = new RemoveVerifierServerEndPointsInput
        {
            Id = Hash.Empty,
            EndPoints = { "127.0.0.3" }
        };
        await CaContractStub.RemoveVerifierServerEndPoints.SendAsync(inputWithNameNotExist);

        var inputWithEndPointsNotExist = new RemoveVerifierServerEndPointsInput
        {
            Id = id,
            EndPoints = { "127.0.0.3" }
        };
        await CaContractStub.RemoveVerifierServerEndPoints.SendAsync(inputWithEndPointsNotExist);
        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers.Count.ShouldBe(1);
        ;
    }

    [Fact]
    public async Task RemoveVerifierServerEndPointsTest_Failed_InvalidInput()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });

        var result =
            await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(
                new RemoveVerifierServerEndPointsInput());
        result.TransactionResult.Error.ShouldContain("invalid input id");

        result = await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(
            new RemoveVerifierServerEndPointsInput
            {
                Id = new Hash()
            });
        result.TransactionResult.Error.ShouldContain("invalid input id");

        result = await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(
            new RemoveVerifierServerEndPointsInput
            {
                Id = Hash.Empty
            });
        result.TransactionResult.Error.ShouldContain("invalid input endPoints");

        result = await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(
            new RemoveVerifierServerEndPointsInput
            {
                Id = Hash.Empty,
                EndPoints = { }
            });
        result.TransactionResult.Error.ShouldContain("invalid input endPoints");
    }

    [Fact]
    public async Task RemoveVerifierServerTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });

        await CaContractStub.RemoveVerifierServer.SendAsync(new RemoveVerifierServerInput
        {
            Id = Hash.Empty
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput()
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1", "127.0.0.2" }
        });
        
        await CaContractStub.RemoveVerifierServer.SendAsync(new RemoveVerifierServerInput
        {
            Id = HashHelper.ComputeFrom("test")
        });
        
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var input = new RemoveVerifierServerInput
        {
            Id = id
        };
        await CaContractStub.RemoveVerifierServer.SendAsync(input);
        
        var inputWithNameNotExist = new RemoveVerifierServerInput
        {
            Id = Hash.Empty
        };
        await CaContractStub.RemoveVerifierServer.SendAsync(inputWithNameNotExist);
        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveVerifierServerTest_Failed_NotAdmin()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = User1Address
        });
        var result = await CaContractStub.RemoveVerifierServer.SendWithExceptionAsync(new RemoveVerifierServerInput
        {
            Id = Hash.Empty
        });
        result.TransactionResult.Error.ShouldContain("No permission");
    }

    [Fact]
    public async Task RemoveVerifierServerTest_Failed_InvalidInput()
    {
        await AddVerifierServerEndPointsTest();

        var inputWithNameNull = new RemoveVerifierServerInput();
        var result = await CaContractStub.RemoveVerifierServer.SendWithExceptionAsync(inputWithNameNull);
        result.TransactionResult.Error.ShouldContain("invalid input id");
    }

    [Fact]
    public async Task GetVerifierServersTest()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { "127.0.0.1" }
        });

        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.ShouldContain("127.0.0.1");
    }
}