using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    [Fact]

    public async Task AddCAServerTest()
    {
        //set admin
        await CaContractStub.Initialize.SendAsync(new InitializeInput()
        {
            ContractAdmin = DefaultAccount.Address
        });
        var output = await CaContractStub.GetCAServers.CallAsync(new Empty());
        output.CaServers.Count.ShouldBe(0);
        //success
        await CaContractStub.AddCAServer.SendAsync( new AddCAServerInput()
        {
            Name = "Server1",
            EndPoints = "https://server1.io"
        });

        var list = await CaContractStub.GetCAServers.CallAsync(new Empty());
        var newServer = new CAServer()
        {
            Name = "Server1",
            EndPoint = "https://server1.io"
        };
        list.CaServers.ShouldContain(newServer);
        
        //server existed
        var txResult = await CaContractStub.AddCAServer.SendAsync( new AddCAServerInput()
        {
            Name = "Server1",
            EndPoints = "https://server1.io"
        });
        txResult.TransactionResult.Error.ShouldBe("");
        
        //name is null 
        var txExecutionResult = await CaContractStub.AddCAServer.SendWithExceptionAsync( new AddCAServerInput()
        {
            EndPoints = "https://server2.io"
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        
        //endpoint is null
        txExecutionResult = await CaContractStub.AddCAServer.SendWithExceptionAsync( new AddCAServerInput()
        {
            Name = "Server2"
        });
        txExecutionResult.TransactionResult.Error.ShouldContain("Invalid input.");
    }

    [Fact]
    public async Task RemoveCAServerTest()
    {
        //set admin
        await CaContractStub.Initialize.SendAsync(new InitializeInput()
        {
            ContractAdmin = DefaultAccount.Address
        });
        await CaContractStub.AddCAServer.SendAsync( new AddCAServerInput()
        {
            Name = "Server1",
            EndPoints = "https://server1.io"
        });
        //name not exist
        var txResult = await CaContractStub.RemoveCAServer.SendAsync(new RemoveCAServerInput()
        {
            Name = "Server2"
        });
        txResult.TransactionResult.Error.ShouldBe("");
        //success
        await CaContractStub.RemoveCAServer.SendAsync(new RemoveCAServerInput()
        {
            Name = "Server1"
        });
        var list = await CaContractStub.GetCAServers.CallAsync(new Empty());
        var newServer = new CAServer()
        {
            Name = "Server1",
            EndPoint = "https://server1.io"
        };
        list.CaServers.ShouldNotContain(newServer);
        
        //name is ""
        txResult = await CaContractStub.RemoveCAServer.SendWithExceptionAsync(new RemoveCAServerInput()
        {
            Name = ""
        });
        txResult.TransactionResult.Error.ShouldContain("Invalid input.");
    }
}