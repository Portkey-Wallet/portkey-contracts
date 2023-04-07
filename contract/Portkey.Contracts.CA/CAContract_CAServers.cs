using System.Linq;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty AddCAServer(AddCAServerInput input)
    {
        Assert(State.ServerControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        // Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Name) && !string.IsNullOrEmpty(input.EndPoints), "Invalid input.");
        State.CaServerList.Value ??= new CAServerList();
        var existServer = State.CaServerList.Value.CaServers.FirstOrDefault(s => s.Name == input.Name);
        if (existServer != null)
        {
            existServer.EndPoint = input.EndPoints;
        }
        else
        {
            State.CaServerList.Value.CaServers.Add(new CAServer
            {
                Name = input.Name,
                EndPoint = input.EndPoints
            });
        }

        Context.Fire(new CAServerAdded
        {
            CaSeverAdded = new CAServer
            {
                Name = input.Name,
                EndPoint = input.EndPoints
            }
        });
        return new Empty();
    }

    public override Empty RemoveCAServer(RemoveCAServerInput input)
    {
        Assert(State.ServerControllers.Value.Controllers.Contains(Context.Sender), "No permission");
        // Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.Name), "Invalid input.");
        var existServer = State.CaServerList.Value.CaServers.FirstOrDefault(s => s.Name == input.Name);
        if (existServer == null) return new Empty();
        State.CaServerList.Value.CaServers.Remove(existServer);
        Context.Fire(new CAServerRemoved
        {
            CaServerRemoved = existServer
        });
        return new Empty();
    }

    public override GetCAServersOutput GetCAServers(Empty input)
    {
        var caServerList = State.CaServerList.Value;

        if (caServerList != null && caServerList.CaServers != null)
        {
            return new GetCAServersOutput
            {
                CaServers = { caServerList.CaServers }
            };
        }

        return new GetCAServersOutput();
    }
}