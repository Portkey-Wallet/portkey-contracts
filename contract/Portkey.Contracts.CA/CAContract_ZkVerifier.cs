using CaInternal;
using Google.Protobuf.WellKnownTypes;
using ZkVerifier;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override Empty AddZkIssuer(IssuerPublicKeyEntry input)
    {
        // IMPORTANT TODO: This is only for POC, production use needs to have permission control.
        Assert(!string.IsNullOrEmpty(input.IssuerName), "Issuer name cannot be empty.");
        Assert(!string.IsNullOrEmpty(input.IssuerPubkey), "Issuer public key cannot be empty.");
        var existing = State.ZkIssuerMap[input.IssuerName];
        Assert(existing == null, "Issuer already exists.");
        var issuerList = State.ZkIssuerList.Value;
        issuerList.Issuers.Add(input.IssuerName);
        State.ZkIssuerMap[input.IssuerName] = new ZkIssuerPublicKeyList
        {
            PublicKeys = { input.IssuerPubkey }
        };
        return new Empty();
    }

    public override Empty AddZkIssuerPublicKey(IssuerPublicKeyEntry input)
    {
        // IMPORTANT TODO: This is only for POC, production use needs to have permission control.
        Assert(!string.IsNullOrEmpty(input.IssuerName), "Issuer name cannot be empty.");
        Assert(!string.IsNullOrEmpty(input.IssuerPubkey), "Issuer public key cannot be empty.");
        var existing = State.ZkIssuerMap[input.IssuerName];
        Assert(existing != null, "Issuer doesn't exist.");
        State.ZkIssuerMap[input.IssuerName].PublicKeys.Add(input.IssuerPubkey);
        return new Empty();
    }

    public override Empty RemoveZkIssuerPublicKey(IssuerPublicKeyEntry input)
    {
        // IMPORTANT TODO: This is only for POC, production use needs to have permission control.
        Assert(!string.IsNullOrEmpty(input.IssuerName), "Issuer name cannot be empty.");
        Assert(!string.IsNullOrEmpty(input.IssuerPubkey), "Issuer public key cannot be empty.");
        var existing = State.ZkIssuerMap[input.IssuerName];
        Assert(existing != null, "Issuer doesn't exist.");
        var state = State.ZkIssuerMap[input.IssuerName];
        state.PublicKeys.Remove(input.IssuerPubkey);
        State.ZkIssuerMap[input.IssuerName] = state;
        return new Empty();
    }

    public override Empty RemoveZkIssuer(StringValue input)
    {
        // IMPORTANT TODO: This is only for POC, production use needs to have permission control.
        Assert(!string.IsNullOrEmpty(input.Value), "Issuer name cannot be empty.");
        var existing = State.ZkIssuerMap[input.Value];
        Assert(existing != null, "Issuer doesn't exist.");
        State.ZkIssuerMap.Remove(input.Value);
        return new Empty();
    }

    public override Empty SetZkVerifiyingKey(StringValue input)
    {
        // IMPORTANT TODO: This is only for POC, production use needs to have permission control.
        State.ZkVerifiyingKey.Value = input;
        return new Empty();
    }
}