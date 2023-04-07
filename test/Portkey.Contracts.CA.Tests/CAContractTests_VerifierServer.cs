using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests : CAContractTestBase
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
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1"}
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
    public async Task AddVerifierServerEndPointsTest_Failed_NotAdmin()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = User1Address
        });
        var result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(
            new AddVerifierServerEndPointsInput
            {
                Name = "test",
                EndPoints = {"127.0.0.1"}
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
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1"}
        });

        var inputWithSameName = new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.2"}
        };
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(inputWithSameName);

        var inputWithSameNameAndEndPoints = new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1"}
        };
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(inputWithSameNameAndEndPoints);
        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.ShouldContain("127.0.0.1");
        result.Result.VerifierServers[0].EndPoints.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddVerifierServerEndPointsTest_Failed_InvalidInput()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = ImageUrl,
            EndPoints = {"127.0.0.1"},
            VerifierAddressList = {VerifierAddress}
        });

        var inputWithNameNull = new AddVerifierServerEndPointsInput
        {
            EndPoints = {"127.0.0.2"},
            ImageUrl = ImageUrl,
            VerifierAddressList = {VerifierAddress1}
        };
        var result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(inputWithNameNull);
        result.TransactionResult.Error.ShouldContain("invalid input name");

        var inputWithEndPointsNull = new AddVerifierServerEndPointsInput
        {
            Name = "test1",
            ImageUrl = ImageUrl,
            VerifierAddressList = {VerifierAddress2}
        };
        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(inputWithEndPointsNull);
        result.TransactionResult.Error.ShouldContain("invalid input EndPoints");
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
                Id = new Hash()
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
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1", "127.0.0.2"}
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput()
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1", "127.0.0.2"}
        });
        var verifierServer = await CaContractStub.GetVerifierServers.CallAsync(new Empty());
        var id = verifierServer.VerifierServers[0].Id;
        var input = new RemoveVerifierServerEndPointsInput
        {
            Id = id,
            EndPoints = {"127.0.0.1"}
        };
        await CaContractStub.RemoveVerifierServerEndPoints.SendAsync(input);

        var inputWithNameNotExist = new RemoveVerifierServerEndPointsInput
        {
            Id = new Hash(),
            EndPoints = {"127.0.0.3"}
        };
        await CaContractStub.RemoveVerifierServerEndPoints.SendAsync(inputWithNameNotExist);

        var inputWithEndPointsNotExist = new RemoveVerifierServerEndPointsInput
        {
            Id = id,
            EndPoints = {"127.0.0.3"}
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

        var inputWithEndPointsNull = new RemoveVerifierServerEndPointsInput
        {
            Id = new Hash(),
            EndPoints = { }
        };
        var result = await CaContractStub.RemoveVerifierServerEndPoints.SendWithExceptionAsync(inputWithEndPointsNull);
        result.TransactionResult.Error.ShouldContain("invalid input EndPoints");
    }

    [Fact]
    public async Task RemoveVerifierServerTest_Succeed()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1"}
        });
        await CaContractStub.AddVerifierServerEndPoints.SendAsync(new AddVerifierServerEndPointsInput()
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1", "127.0.0.2"}
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
            Id = new Hash()
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
            Id = new Hash()
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
        
        result = await CaContractStub.AddVerifierServerEndPoints.SendWithExceptionAsync(new AddVerifierServerEndPointsInput
        {
            Name = "test",
            ImageUrl = "url",
            VerifierAddressList = { new Address() },
            EndPoints = { }
        });
        result.TransactionResult.Error.ShouldContain("invalid input endPoints");
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
            VerifierAddressList = {new Address()},
            EndPoints = {"127.0.0.1"}
        });

        var result = CaContractStub.GetVerifierServers.CallAsync(new Empty());
        result.Result.VerifierServers[0].EndPoints.ShouldContain("127.0.0.1");
    }
}