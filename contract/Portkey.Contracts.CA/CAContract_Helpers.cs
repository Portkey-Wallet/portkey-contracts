using System;
using System.Linq;
using AElf;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Enum = Google.Protobuf.WellKnownTypes.Enum;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");
        if (verifierDoc.Length != 6)
        {
            return false;
        }

        //Check expired time 1h.
        var verificationTime = DateTime.SpecifyKind(Convert.ToDateTime(verifierDoc[2]), DateTimeKind.Utc);
        if (verificationTime.ToTimestamp().AddHours(1) <= Context.CurrentBlockTime ||
            !int.TryParse(verifierDoc[0], out var type) ||
            (int)guardianInfo.Type != type ||
            guardianInfo.IdentifierHash != Hash.LoadFromHex(verifierDoc[1]))
        {
            return false;
        }

        //Check VerifierDoc is Verified.
        var verifierDocSaltMap = State.VerifierDocMap;

        var key = GetKeyFromVerificationDoc(verifierDoc);
        if (verifierDocSaltMap[key])
        {
            return false;
        }

        //Check verifier address and data.
        var verifierAddress = Address.FromBase58(verifierDoc[3]);
        var verificationInfo = guardianInfo.VerificationInfo;
        var verifierServer =
            State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v => v.Id == verificationInfo.Id);

        //Recovery verifier address.
        var data = HashHelper.ComputeFrom(verificationInfo.VerificationDoc);
        var publicKey = Context.RecoverPublicKey(verificationInfo.Signature.ToByteArray(),
            data.ToByteArray());
        var verifierAddressFromPublicKey = Address.FromPublicKey(publicKey);

        return verifierServer != null && verifierAddressFromPublicKey == verifierAddress &&
               verifierServer.VerifierAddresses.Contains(verifierAddress);
    }

    private bool IsGuardianExist(Hash caHash, GuardianInfo guardianInfo)
    {
        var satisfiedGuardians = State.HolderInfoMap[caHash].GuardianList.Guardians.FirstOrDefault(
            g => g.IdentifierHash == guardianInfo.IdentifierHash && g.Type == guardianInfo.Type &&
                 g.VerifierId == guardianInfo.VerificationInfo.Id
        );
        return satisfiedGuardians != null;
    }

    private bool IsValidHash(Hash hash)
    {
        return hash != null && !hash.Value.IsEmpty;
    }

    private string GetFromVerificationDoc(string[] verificationDoc, int index)
    {
        return verificationDoc[index];
    }


    private Hash GetKeyFromVerificationDoc(string[] verificationDoc)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(verificationDoc[2]),
            HashHelper.ComputeFrom(verificationDoc[3]),
            HashHelper.ComputeFrom(verificationDoc[1]));
    }


    private HolderInfo GetHolderInfoByCaHash(Hash caHash)
    {
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {caHash}");

        return holderInfo;
    }
}