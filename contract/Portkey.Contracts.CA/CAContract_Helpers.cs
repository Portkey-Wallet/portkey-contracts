using System;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private bool CheckVerifierSignatureAndDataCompatible(GuardianInfo guardianInfo, string methodName)
    {
        if (State.OperationTypeInSignatureEnabled.Value)
        {
            return CheckVerifierSignatureAndData(guardianInfo, methodName);
        }

        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");
        return verifierDoc.Length switch
        {
            5 => CheckVerifierSignatureAndData(guardianInfo),
            6 => CheckVerifierSignatureAndData(guardianInfo, methodName),
            7 => CheckVerifierSignatureAndData(guardianInfo, methodName),
            _ => false
        };
    }


    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo, string methodName)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt,operationType]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");

        if (verifierDoc.Length != 6 && verifierDoc.Length != 7)
        {
            return false;
        }

        var docInfo = GetVerificationDoc(verificationDoc);

        if (docInfo.OperationType == "0")
        {
            return false;
        }

        var key = HashHelper.ComputeFrom(guardianInfo.VerificationInfo.Signature.ToByteArray());
        if (State.VerifierDocMap[key])
        {
            return false;
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
        if (verifierServer == null)
        {
            var verifierId = State.VerifierIdMap[verificationInfo.Id];
            if (IsValidHash(verifierId))
            {
                verifierServer = 
                    State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v => v.Id == verifierId);
            }
        }

        //Recovery verifier address.
        var data = HashHelper.ComputeFrom(verificationInfo.VerificationDoc);
        var publicKey = Context.RecoverPublicKey(verificationInfo.Signature.ToByteArray(),
            data.ToByteArray());
        var verifierAddressFromPublicKey = Address.FromPublicKey(publicKey);


        if (verifierServer == null || verifierAddressFromPublicKey != verifierAddress ||
            !verifierServer.VerifierAddresses.Contains(verifierAddress))
        {
            return false;
        }

        key = HashHelper.ComputeFrom(guardianInfo.VerificationInfo.Signature.ToByteArray());
        State.VerifierDocMap[key] = true;
        var operationTypeStr = docInfo.OperationType;
        var operationTypeName = typeof(OperationType).GetEnumName(Convert.ToInt32(operationTypeStr))?.ToLower();
        if (operationTypeName != methodName)
        {
            return false;
        }

        if (verifierDoc.Length == 7)
        {
            if (methodName != nameof(OperationType.Approve).ToLower() && methodName != nameof(OperationType.ModifyTransferLimit).ToLower())
            {
                return int.Parse(verifierDoc[6]) == Context.ChainId;
            }
        }
        return true;
    }


    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");
        if (verifierDoc.Length != 5)
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

    private void ValidateOperationType(OperationType type)
    {
        Assert(!string.IsNullOrWhiteSpace(typeof(OperationType).GetEnumName(Convert.ToInt32(type))),
            $"The OperationType: {type} does not exist");
    }

    private class VerificationDocInfo
    {
        public string GuardianType { get; set; }
        public Hash IdentifierHash { get; set; }
        public string VerificationTime { get; set; }
        public Address VerifierAddress { get; set; }
        public string Salt { get; set; }
        public string OperationType { get; set; }
    }

    private VerificationDocInfo GetVerificationDoc(string doc)
    {
        var docs = doc.Split(",");
        return new VerificationDocInfo
        {
            GuardianType = docs[0],
            IdentifierHash = Hash.LoadFromHex(docs[1]),
            VerificationTime = docs[2],
            VerifierAddress = Address.FromBase58(docs[3]),
            Salt = docs[4],
            OperationType = docs[5]
        };
    }


    private HolderInfo GetHolderInfoByCaHash(Hash caHash)
    {
        var holderInfo = State.HolderInfoMap[caHash];
        Assert(holderInfo != null, $"CA holder is null.CA hash:{caHash}");
        Assert(holderInfo!.GuardianList != null, $"No guardians found in this holder by caHash: {caHash}");

        return holderInfo;
    }

    private string GetSaltFromVerificationDoc(string verificationDoc)
    {
        return verificationDoc.Split(",")[4];
    }

    private static bool IsValidSymbolChar(char character)
    {
        return (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9');
    }

    private static bool IsOverDay(string lastDayTime, string nowDayTime)
    {
        return string.IsNullOrEmpty(lastDayTime) || DateTime.Parse(lastDayTime) < DateTime.Parse(nowDayTime);
    }

    private static string GetCurrentBlockTimeString(Timestamp currentBlockTime)
    {
        return currentBlockTime.ToDateTime().ToString("yyyy-MM-dd");
    }
    
    private TokenInfo GetTokenInfo(string symbol)
    {
        return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
    }
}