using System.Linq;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty AddCreatorController(ControllerInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
        Assert(input != null && input.Address != null, "Invalid input");

        var controller = State.CreatorControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
        if (controller != null)
        {
            return new Empty();
        }
        
        State.CreatorControllers.Value.Controllers.Add(input!.Address);
        Context.Fire(new CreatorControllerAdded
        {
            Address = input.Address
        });

        return new Empty();
    }

    public override Empty RemoveCreatorController(ControllerInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
        Assert(input != null && input.Address != null, "Invalid input");

        var controller = State.CreatorControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
        if (controller == null)
        {
            return new Empty();
        }

        State.CreatorControllers.Value.Controllers.Remove(controller);
        
        Context.Fire(new CreatorControllerRemoved
        {
            Address = input!.Address
        });

        return new Empty();
    }

    public override ControllerOutput GetCreatorControllers(Empty input)
    {
        return new ControllerOutput
        {
            Addresses = { State.CreatorControllers.Value.Controllers }
        };
    }

    public override Empty AddServerController(ControllerInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
        Assert(input != null && input.Address != null, "Invalid input");

        var controller = State.ServerControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
        if (controller != null)
        {
            return new Empty();
        }
        
        State.ServerControllers.Value.Controllers.Add(input!.Address);
        Context.Fire(new ServerControllerAdded
        {
            Address = input.Address
        });

        return new Empty();
    }

    public override Empty RemoveServerController(ControllerInput input)
    {
        Assert(State.OrganizationAddress.Value == Context.Sender, "No permission.");
        Assert(input != null && input.Address != null, "Invalid input");

        var controller = State.ServerControllers.Value.Controllers.FirstOrDefault(c => c == input!.Address);
        if (controller == null)
        {
            return new Empty();
        }

        State.ServerControllers.Value.Controllers.Remove(controller);
        
        Context.Fire(new ServerControllerRemoved
        {
            Address = input!.Address
        });

        return new Empty();
    }

    public override ControllerOutput GetServerControllers(Empty input)
    {
        return new ControllerOutput
        {
            Addresses = { State.ServerControllers.Value.Controllers }
        };
    }

    public override Empty ChangeAdmin(AdminInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(input != null && input.Address != null, "Invalid input");

        if (State.Admin.Value == input!.Address)
        {
            return new Empty();
        }

        State.Admin.Value = input.Address;
        
        Context.Fire(new AdminChanged
        {
            Address = input.Address
        });

        return new Empty();
    }

    public override AdminOutput GetAdmin(Empty input)
    {
        return new AdminOutput
        {
            Address = State.Admin.Value
        };
    }

    public override Empty SetOrganizationAddress(AdminInput input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission");
        Assert(input != null && input.Address != null, "Invalid input");
        State.OrganizationAddress.Value = input?.Address;
        return new Empty();
    }

    public override AdminOutput GetOrganizationAddress(Empty input)
    {
        return new AdminOutput()
        {
            Address = State.OrganizationAddress.Value
        };
    }
}