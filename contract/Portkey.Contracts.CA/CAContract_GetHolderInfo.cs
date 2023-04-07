namespace Portkey.Contracts.CA;

public partial class CAContract
{
    public override GetHolderInfoOutput GetHolderInfo(GetHolderInfoInput input)
    {
        Assert(input != null, "input cannot be null!");
        // CaHash and loginGuardian cannot be invalid at same time.
        Assert(
            input!.CaHash != null || CheckHashInput(input.LoginGuardianIdentifierHash),
            $"CaHash is null, or loginGuardianIdentifierHash is empty: {input.CaHash}, {input.LoginGuardianIdentifierHash}");

        var output = new GetHolderInfoOutput();

        var caHash = input.CaHash ?? State.GuardianMap[input.LoginGuardianIdentifierHash];
        Assert(caHash != null,
            $"Not found ca_hash by a the loginGuardianIdentifierHash {input.LoginGuardianIdentifierHash}");
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null,
            $"Holder is not found");
        output.CaHash = caHash;
        output.ManagerInfos.AddRange(holderInfo?.ManagerInfos.Clone());

        output.CaAddress = Context.ConvertVirtualAddressToContractAddress(output.CaHash);
        output.GuardianList =
            holderInfo?.GuardianList == null ? new GuardianList() : holderInfo.GuardianList.Clone();

        return output;
    }
}