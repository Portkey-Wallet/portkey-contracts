using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Groth16.Net;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private const int CircomBigIntN = 121;
    private const int CiromBigIntK = 17;
    
    public bool CheckZkLoginVerifierAndData(GuardianInfo guardianInfo)
    {
        // check guardian type
        if (!IsZkLoginSupported(guardianInfo.Type))
        {
            return false;
        }
        if (guardianInfo.ZkOidcInfo == null)
        {
            return false;
        }
        //check circuit id
        if (State.CircuitVerifyingKeys[guardianInfo.ZkOidcInfo.CircuitId] == null
            || State.JwtIssuers[guardianInfo.Type] == null)
        {
            return false;
        }
        // check jwt issuer
        if (!State.JwtIssuers[guardianInfo.Type].Equals(guardianInfo.ZkOidcInfo.Issuer))
        {
            return false;
        }
        // check nonce
        if (string.IsNullOrWhiteSpace(guardianInfo.ZkOidcInfo.Nonce))
        {
            return false;
        }
        //check nonce wasn't used before
        State.ZkNonceList.Value ??= new ZkNonceList();
        if (State.ZkNonceList.Value.Nonce.Contains(guardianInfo.ZkOidcInfo.Nonce))
        {
            return false;
        }
        else
        {
            State.ZkNonceList.Value.Nonce.Add(guardianInfo.ZkOidcInfo.Nonce);
        }
    
        // check nonce = sha256(nonce_payload.to_bytes())
        if (!guardianInfo.ZkOidcInfo.Nonce.Equals(GetSha256Hash(guardianInfo.ZkOidcInfo.NoncePayload.AddManagerAddress.ToByteArray())))
        {
            return false;
        }
    
        var publicKey = State.IssuerPublicKeysByKid[guardianInfo.Type][guardianInfo.ZkOidcInfo.Kid];
        if (publicKey is null or "")
        {
            return false;
        }
        var circuitId = new StringValue
        {
            Value = guardianInfo.ZkOidcInfo.CircuitId
        };
        var verifyingKey = GetVerifyingKey(circuitId);
        if (verifyingKey == null)
        {
            return false;
        }

        return VerifyZkProof(guardianInfo.ZkOidcInfo.ZkProof, guardianInfo.ZkOidcInfo.Nonce,
            guardianInfo.ZkOidcInfo.IdentifierHash.ToHex(), guardianInfo.ZkOidcInfo.Salt, verifyingKey.VerifyingKey_, publicKey);
    }
    
    private bool VerifyZkProof(string proof, string nonce, string identifierHash, string salt, string verifyingKey, string pubkey)
    {
        var pubkeyChunks = Decode(pubkey)
            .ToChunked(CircomBigIntN, CiromBigIntK)
            .Select(HexToBigInt).ToList();
        var proofDto = InternalRapidSnarkProofRepr.Parser.ParseJson(proof);
        var nonceInInts = nonce.ToCharArray().Select(b => ((int)b).ToString()).ToList();
        // var nonceInInts = Encoding.UTF8.GetBytes(nonce).Select(b => b.ToString()).ToList();
        var saltInInts = salt.HexStringToByteArray().Select(b => b.ToString()).ToList();
    
        // var publicInputs = new List<List<string>>
        // {
        //     ToPublicInput(identifierHash),
        //     nonceInInts, pubkeyChunks, saltInInts
        // }.SelectMany(n => n).ToList();
        var publicInputs = new List<string>();
        publicInputs.AddRange(ToPublicInput(identifierHash));
        publicInputs.AddRange(nonceInInts);
        publicInputs.AddRange(pubkeyChunks);
        publicInputs.AddRange(saltInInts);
        var piB = new List<List<string>>();
        for (var i = 0; i < proofDto.PiB.Count; i++)
        {
            piB[i].AddRange(proofDto.PiB[i].PiB.ToList());
        }
        return Verifier.VerifyBn254(verifyingKey, publicInputs, new RapidSnarkProof
        {
            PiA = proofDto.PiA.ToList(),
            PiB = piB,
            PiC = proofDto.PiC.ToList(),
        });
    }
    
    private byte[] Decode(string input)
    {
        var output = input;
        output = output.Replace('-', '+'); // 62nd char of encoding
        output = output.Replace('_', '/'); // 63rd char of encoding
        switch (output.Length % 4) // Pad with trailing '='s
        {
            case 0:
                break; // No pad chars in this case
            case 2:
                output += "==";
                break; // Two pad chars
            case 3:
                output += "=";
                break; // One pad char
        }
        var converted = Convert.FromBase64String(output); // Standard base64 decoder
        return converted;
    }
    
    //Sha256Hash
    private List<string> ToPublicInput(string identifierHash)
    {
        return identifierHash.HexStringToByteArray().Select(b => b.ToString()).ToList();
    }
    
    private static string HexToBigInt(byte[] hex)
    {
        return HexHelper.ConvertBigEndianToDecimalString(hex);
        // return BigInteger.Parse(hexString, NumberStyles.HexNumber).ToString();
    }
    
    public static bool IsValidGuardianSupportZkLogin(GuardianInfo guardianInfo)
    {
        if (guardianInfo == null)
        {
            return false;
        }
    
        return guardianInfo.ZkOidcInfo != null
               && guardianInfo.ZkOidcInfo.Nonce is not (null or "")
               && guardianInfo.ZkOidcInfo.Kid is not (null or "")
               && guardianInfo.ZkOidcInfo.ZkProof is not (null or "")
               && guardianInfo.ZkOidcInfo.Issuer is not (null or "")
               && guardianInfo.ZkOidcInfo.Salt is not (null or "")
               && guardianInfo.ZkOidcInfo.NoncePayload is not null;
    }
    
    public static bool IsValidZkOidcInfoSupportZkLogin(ZkJwtAuthInfo zkOidcInfo)
    {
        if (zkOidcInfo == null)
        {
            return false;
        }
    
        return zkOidcInfo.Nonce is not (null or "")
               && zkOidcInfo.Kid is not (null or "")
               && zkOidcInfo.ZkProof is not (null or "")
               && zkOidcInfo.Issuer is not (null or "")
               && zkOidcInfo.Salt is not (null or "")
               && zkOidcInfo.NoncePayload is not null;
    }
    
    public static bool IsZkLoginSupported(GuardianType type)
    {
        return GuardianType.OfGoogle.Equals(type) || GuardianType.OfApple.Equals(type) ||
               GuardianType.OfFacebook.Equals(type);
    }
    
    private static string GetSha256Hash(byte[] input)
    {
        return HashHelper.ComputeFrom(input).ToHex();
    }
    
    public Hash GetOneVerifierFromServers()
    {
        var verifiers = State.VerifiersServerList.Value.VerifierServers;
        return verifiers[0].Id;
    }
        
    private bool CheckVerifierSignatureAndData(GuardianInfo guardianInfo, string methodName, Hash caHash = null,
        string operationDetails = null)
    {
        var verifierDocLength = GetVerificationDocLength(guardianInfo.VerificationInfo.VerificationDoc);

        if (verifierDocLength != 7 && verifierDocLength != 8)
        {
            return false;
        }

        return CheckVerifierSignatureAndDataWithCreateChainId(guardianInfo, methodName, caHash, operationDetails);
    }

    private bool CheckVerifierSignatureAndDataWithCreateChainId(GuardianInfo guardianInfo, string methodName,
        Hash caHash, string operationDetails = null)
    {
        //[type,guardianIdentifierHash,verificationTime,verifierAddress,salt,operationType,createChainId<,operationHash>]
        var verificationDoc = guardianInfo.VerificationInfo.VerificationDoc;
        if (verificationDoc == null || string.IsNullOrWhiteSpace(verificationDoc))
        {
            return false;
        }

        var verifierDoc = verificationDoc.Split(",");

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

        var operationTypeStr = docInfo.OperationType;
        var operationTypeName = typeof(OperationType).GetEnumName(Convert.ToInt32(operationTypeStr))?.ToLower();
        if (operationTypeName != methodName)
        {
            return false;
        }

        //Check verifier address and data.
        var verifierAddress = docInfo.VerifierAddress;
        var verificationInfo = guardianInfo.VerificationInfo;
        var verifierServer =
            State.VerifiersServerList.Value.VerifierServers.FirstOrDefault(v => v.Id == verificationInfo.Id);
        if (verifierServer == null || !verifierServer.VerifierAddresses.Contains(verifierAddress))
        {
            return false;
        }

        var verifierAddressFromPublicKey =
            RecoverVerifierAddress(verificationInfo.VerificationDoc, verificationInfo.Signature);
        if (verifierAddressFromPublicKey != verifierAddress)
        {
            verifierAddressFromPublicKey =
                RecoverVerifierAddress($"{verificationInfo.VerificationDoc},{operationDetails}",
                    verificationInfo.Signature);
        }
        if (verifierAddressFromPublicKey != verifierAddress)
        {
            return false;
        }

        if (!CheckVerifierSignatureOperationDetail(operationTypeName, verifierDoc, operationDetails))
        {
            return false;
        }
        State.VerifierDocMap[key] = true;

        if (operationTypeName == nameof(OperationType.AddGuardian).ToLower() &&
            !CheckOnCreateChain(State.HolderInfoMap[caHash]))
        {
            return true;
        }

        //After verifying the contents of the operation,it is not necessary to verify the 'ChainId'
        if (verifierDoc.Length >= 8 &&
            ((operationTypeName == nameof(OperationType.CreateCaholder).ToLower() && caHash != null) ||
             operationTypeName == nameof(OperationType.SocialRecovery).ToLower()))
        {
            return true;
        }

        return int.Parse(verifierDoc[6]) == Context.ChainId;
    }

    private bool CheckOnCreateChain(HolderInfo holderInfo)
    {
        if (holderInfo.GuardianList == null || holderInfo.GuardianList.Guardians == null ||
            holderInfo.GuardianList.Guardians.Count == 0)
        {
            return false;
        }

        return holderInfo.CreateChainId == Context.ChainId || holderInfo.CreateChainId == 0;
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

    private static bool IsOverDay(Timestamp lastDayTime, Timestamp nowDayTime)
    {
        return lastDayTime == null || DateTime.Parse(lastDayTime.ToDateTime().ToString("yyyy-MM-dd")) <
            DateTime.Parse(nowDayTime.ToDateTime().ToString("yyyy-MM-dd"));
    }

    private TokenInfo GetTokenInfo(string symbol)
    {
        return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
    }

    private GetBalanceOutput GetTokenBalance(string symbol, Address cAddress)
    {
        return State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = symbol,
            Owner = cAddress
        });
    }

    private void SetProjectDelegator(Address delegatorAddress)
    {
        if (!IsValidHash(State.CaProjectDelegateHash.Value))
        {
            return;
        }

        var projectDelegateInfo = State.ProjectDelegateInfo[State.CaProjectDelegateHash.Value];
        if (projectDelegateInfo == null || projectDelegateInfo.DelegateeHashList.Count == 0)
        {
            return;
        }

        State.ProjectDelegationFee.Value ??= new ProjectDelegationFee()
        {
            Amount = CAContractConstants.DefaultProjectDelegationFee
        };

        var delegations = new Dictionary<string, long>
        {
            [CAContractConstants.ELFTokenSymbol] = State.ProjectDelegationFee.Value.Amount
        };
        // Randomly select a delegatee based on the address
        var selectIndex = (int)Math.Abs(delegatorAddress.ToByteArray().ToInt64(true) %
                                        projectDelegateInfo.DelegateeHashList.Count);
        State.TokenContract.SetTransactionFeeDelegations.VirtualSend(projectDelegateInfo.DelegateeHashList[selectIndex],
            new SetTransactionFeeDelegationsInput
            {
                DelegatorAddress = delegatorAddress,
                Delegations =
                {
                    delegations
                }
            });
    }

    private bool IsValidInviteCode(string code)
    {
        return !string.IsNullOrWhiteSpace(code) && code.Length <= CAContractConstants.ReferralCodeLength;
    }

    private Address RecoverVerifierAddress(string verificationDoc, ByteString signature)
    {
        var data = HashHelper.ComputeFrom(verificationDoc);
        var publicKey = Context.RecoverPublicKey(signature.ToByteArray(),
            data.ToByteArray());
        return Address.FromPublicKey(publicKey);
    }

    private int GetVerificationDocLength(string verificationDoc)
    {
        return string.IsNullOrWhiteSpace(verificationDoc) ? 0 : verificationDoc.Split(",").Length;
    }

    /// <summary>
    /// State.CheckOperationDetailsInSignatureEnabled==true: must verify the Hash value of operationDetails
    /// verifierDoc.Length >= 8: only verify registration and social recovery
    /// </summary>
    /// <param name="operationTypeName"></param>
    /// <param name="verifierDoc"></param>
    /// <param name="operationDetails"></param>
    /// <returns></returns>
    private bool CheckVerifierSignatureOperationDetail(string operationTypeName, string[] verifierDoc,
        string operationDetails)
    {
        if (State.CheckOperationDetailsInSignatureEnabled.Value
            || verifierDoc.Length >= 8)
        {
            if (verifierDoc.Length < 8 || string.IsNullOrWhiteSpace(verifierDoc[7]) ||
                string.IsNullOrWhiteSpace(operationDetails))
            {
                return false;
            }

            return verifierDoc[7] == HashHelper.ComputeFrom(operationDetails).ToHex();
        }

        return true;
    }
}