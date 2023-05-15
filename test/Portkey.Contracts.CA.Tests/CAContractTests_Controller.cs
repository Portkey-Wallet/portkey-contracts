using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Portkey.Contracts.CA;

public partial class CAContractTests
{
    private async Task Initiate()
    {
        await CaContractStub.Initialize.SendAsync(new InitializeInput
        {
            ContractAdmin = DefaultAddress
        });
    }

    [Fact]
    public async Task AddCreatorControllerTests()
    {
        await Initiate();
        
        var result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(1);
        
        await CaContractStub.AddCreatorController.SendAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(2);
        result.Addresses.Last().ShouldBe(User1Address);
        
        await CaContractStub.AddCreatorController.SendAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddCreatorControllerTests_Fail()
    {
        await Initiate();
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        
        var result = await CaContractUser1Stub.AddCreatorController.SendWithExceptionAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await CaContractStub.AddCreatorController.SendWithExceptionAsync(new ControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task RemoveCreatorControllerTests()
    {
        await Initiate();
        
        var result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(1);
        
        await CaContractStub.RemoveCreatorController.SendAsync(new ControllerInput
        {
            Address = DefaultAddress
        });
        
        result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(0);
        
        await CaContractStub.RemoveCreatorController.SendAsync(new ControllerInput
        {
            Address = DefaultAddress
        });
        
        result = await CaContractStub.GetCreatorControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(0);
    }
    
    [Fact]
    public async Task RemoveCreatorControllerTests_Fail()
    {
        await Initiate();
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        
        var result = await CaContractUser1Stub.RemoveCreatorController.SendWithExceptionAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await CaContractStub.RemoveCreatorController.SendWithExceptionAsync(new ControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task AddServerControllerTests()
    {
        await Initiate();
        
        var result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(1);
        
        await CaContractStub.AddServerController.SendAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(2);
        result.Addresses.Last().ShouldBe(User1Address);
        
        await CaContractStub.AddServerController.SendAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddServerControllerTests_Fail()
    {
        await Initiate();
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        
        var result = await CaContractUser1Stub.AddServerController.SendWithExceptionAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await CaContractStub.AddServerController.SendWithExceptionAsync(new ControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task RemoveServerControllerTests()
    {
        await Initiate();
        
        var result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(1);
        
        await CaContractStub.RemoveServerController.SendAsync(new ControllerInput
        {
            Address = DefaultAddress
        });
        
        result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(0);
        
        await CaContractStub.RemoveServerController.SendAsync(new ControllerInput
        {
            Address = DefaultAddress
        });
        
        result = await CaContractStub.GetServerControllers.CallAsync(new Empty());
        result.Addresses.Count.ShouldBe(0);
    }
    
    [Fact]
    public async Task RemoveServerControllerTests_Fail()
    {
        await Initiate();
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        
        var result = await CaContractUser1Stub.RemoveServerController.SendWithExceptionAsync(new ControllerInput
        {
            Address = User1Address
        });
        
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await CaContractStub.RemoveServerController.SendWithExceptionAsync(new ControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task ChangeAdminTests()
    {
        await Initiate();
        
        var result = await CaContractStub.GetAdmin.CallAsync(new Empty());
        result.Address.ShouldBe(DefaultAddress);
        
        await CaContractStub.ChangeAdmin.SendAsync(new AdminInput
        {
            Address = DefaultAddress
        });

        await CaContractStub.ChangeAdmin.SendAsync(new AdminInput
        {
            Address = User1Address
        });
        
        result = await CaContractStub.GetAdmin.CallAsync(new Empty());
        result.Address.ShouldBe(User1Address);
    }

    [Fact]
    public async Task ChangeAdminTests_Fail()
    {
        await Initiate();
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 1000000000000,
            Symbol = "ELF",
            To = User1Address
        });
        
        var result = await CaContractUser1Stub.ChangeAdmin.SendWithExceptionAsync(new AdminInput
        {
            Address = User1Address
        });
        
        result.TransactionResult.Error.ShouldContain("No permission");
        
        result = await CaContractStub.ChangeAdmin.SendWithExceptionAsync(new AdminInput());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
}