using System;
using System.Globalization;
using System.Linq;
using AElf;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo, string methodName)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");
        var docInfo = GetVerificationDoc(verificationDoc);
        var key = GetKeyFromVerificationDoc(docInfo);
        if (State.Switch.Value)
        {
            if (verifierDoc.Length != 6)
            {
                return false;
            }

            if (docInfo.OperationType == "0")
            {
                return false;
            }

            if (State.VerifierDocMap[key])
            {
                return false;
            }
        }
        else
        {
            if (verifierDoc.Length != 5)
            {
                return false;
            }
        }


        //Check expired time 1h.
        var verificationTime = DateTime.SpecifyKind(Convert.ToDateTime(docInfo.VerificationTime), DateTimeKind.Utc);
        if (verificationTime.ToTimestamp().AddHours(1) <= Context.CurrentBlockTime ||
            !int.TryParse(docInfo.GuardianType, out var type) ||
            (int)guardianInfo.Type != type ||
            guardianInfo.IdentifierHash != docInfo.IdentifierHash)
        {
            return false;
        }

        //Check verifier address and data.
        var verifierAddress = docInfo.VerifierAddress;
        var verificationInfo = guardianInfo.VerificationInfo;
        var verifierServer =
            State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v => v.Id == verificationInfo.Id);

        //Recovery verifier address.
        var data = HashHelper.ComputeFrom(verificationInfo.VerificationDoc);
        var publicKey = Context.RecoverPublicKey(verificationInfo.Signature.ToByteArray(),
            data.ToByteArray());
        var verifierAddressFromPublicKey = Address.FromPublicKey(publicKey);
        if (!State.Switch.Value)
        {
            return verifierServer != null && verifierAddressFromPublicKey == verifierAddress &&
                   verifierServer.VerifierAddresses.Contains(verifierAddress);
        }

        if (!(verifierServer != null && verifierAddressFromPublicKey == verifierAddress &&
              verifierServer.VerifierAddresses.Contains(verifierAddress)))
        {
            return false;
        }

        State.VerifierDocMap[key] = true;
        var operationTypeStr = docInfo.OperationType;
        var operationTypeName = typeof(OperationType).GetEnumName(Convert.ToInt32(operationTypeStr))?.ToLower();
        return operationTypeName == methodName;
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


    private VerificationDocInfo GetVerificationDoc(string doc)
    {
        var docs = doc.Split(",");
        var verificationDocInfo = new VerificationDocInfo
        {
            GuardianType = docs[0],
            IdentifierHash = Hash.LoadFromHex(docs[1]),
            VerificationTime = docs[2],
            VerifierAddress = Address.FromBase58(docs[3]),
            Salt = docs[4],
        };
        if (State.Switch.Value)
        {
            verificationDocInfo.OperationType = docs[5];
        }

        return verificationDocInfo;
    }

    private Hash GetKeyFromVerificationDoc(VerificationDocInfo verificationDoc)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(verificationDoc.VerificationTime),
            HashHelper.ComputeFrom(verificationDoc.VerifierAddress),
            HashHelper.ComputeFrom(verificationDoc.IdentifierHash));
    }


    private HolderInfo GetHolderInfoByCaHash(Hash caHash)
    {
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {caHash}");

        return holderInfo;
    }
}