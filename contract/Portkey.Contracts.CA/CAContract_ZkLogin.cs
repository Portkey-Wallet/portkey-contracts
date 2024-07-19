using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Types;
using Groth16.Net;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private const int CircomBigIntN = 121;
    private const int CiromBigIntK = 17;

    private bool CheckZkLoginVerifierAndData(GuardianInfo guardianInfo, Hash caHash)
    {
        if (!DoCheckZkLoginBasicParams(guardianInfo, caHash))
        {
            return false;
        }
        
        //check public key
        var publicKey = State.IssuerPublicKeysByKid[guardianInfo.Type][guardianInfo.ZkLoginInfo.Kid];
        if (publicKey is null or "")
        {
            return false;
        }
        
        //check verifying key
        var verifyingKey = State.CircuitVerifyingKeys[guardianInfo.ZkLoginInfo.CircuitId];
        if (verifyingKey == null)
        {
            return false;
        }

        return VerifyZkProof(guardianInfo.ZkLoginInfo, verifyingKey.VerifyingKey_, publicKey);
    }

    private bool DoCheckZkLoginBasicParams(GuardianInfo guardianInfo, Hash caHash)
    {
        //check the caHash
        if (caHash == null || caHash.Equals(Hash.Empty))
        {
            return false;
        }
        // check guardian type
        if (!IsZkLoginSupported(guardianInfo.Type))
        {
            return false;
        }
        if (guardianInfo.ZkLoginInfo == null)
        {
            return false;
        }
        //check circuit id
        if (State.CircuitVerifyingKeys[guardianInfo.ZkLoginInfo.CircuitId] == null)
        {
            return false;
        }
        // check jwt issuer
        if (State.OidcProviderAdminData[guardianInfo.Type].Issuer == null
            || !State.OidcProviderAdminData[guardianInfo.Type].Issuer.Equals(guardianInfo.ZkLoginInfo.Issuer))
        {
            return false;
        }
        // check nonce
        if (string.IsNullOrWhiteSpace(guardianInfo.ZkLoginInfo.Nonce))
        {
            return false;
        }
        //check nonce wasn't used before
        State.ZkNonceListByCaHash[caHash] ??= new ZkNonceList();
        if (State.ZkNonceListByCaHash[caHash].Nonce.Contains(guardianInfo.ZkLoginInfo.Nonce))
        {
            return false;
        }
        else
        {
            State.ZkNonceListByCaHash[caHash].Nonce.Add(guardianInfo.ZkLoginInfo.Nonce);
        }
    
        // check nonce = sha256(nonce_payload.to_bytes())
        var noncePayload = guardianInfo.ZkLoginInfo.NoncePayload.AddManagerAddress.Timestamp.Seconds +
                           guardianInfo.ZkLoginInfo.NoncePayload.AddManagerAddress.ManagerAddress.ToBase58();
        if (!guardianInfo.ZkLoginInfo.Nonce.Equals(GetSha256Hash(noncePayload.GetBytes())))
        {
            return false;
        }

        return true;
    }

    private bool VerifyZkProof(ZkLoginInfo zkLoginInfo, string verifyingKey, string pubkey)
    {
        var pubkeyChunks = Decode(pubkey)
            .ToChunked(CircomBigIntN, CiromBigIntK)
            .Select(HexToBigInt).ToList();
        
        var nonceInInts = zkLoginInfo.Nonce.ToCharArray().Select(b => ((int)b).ToString()).ToList();
        var saltInInts = zkLoginInfo.Salt.HexStringToByteArray().Select(b => b.ToString()).ToList();
        
        var publicInputs = PreparePublicInputs(zkLoginInfo, nonceInInts, pubkeyChunks, saltInInts);
        var (piA, piB, piC) = PrepareZkProof(zkLoginInfo);
        return Verifier.VerifyBn254(verifyingKey, publicInputs, new RapidSnarkProof
        {
            PiA = piA,
            PiB = piB,
            PiC = piC
        });
    }

    private static (List<string>, List<List<string>>, List<string>) PrepareZkProof(ZkLoginInfo zkLoginInfo)
    {
        var piB = new List<List<string>> {new(), new(), new()};
        piB[0].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB1.ToList());
        piB[1].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB2.ToList());
        piB[2].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB3.ToList());
        return (zkLoginInfo.ZkProofInfo.ZkProofPiA.ToList(), piB, zkLoginInfo.ZkProofInfo.ZkProofPiC.ToList());
    }

    private List<string> PreparePublicInputs(ZkLoginInfo zkLoginInfo, List<string> nonceInInts, List<string> pubkeyChunks, List<string> saltInInts)
    {
        var publicInputs = new List<string>();
        publicInputs.AddRange(ToPublicInput(zkLoginInfo.IdentifierHash.ToHex()));
        publicInputs.AddRange(nonceInInts);
        publicInputs.AddRange(pubkeyChunks);
        publicInputs.AddRange(saltInInts);
        return publicInputs;
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
    }

    private static bool IsValidGuardianSupportZkLogin(GuardianInfo guardianInfo)
    {
        if (guardianInfo == null)
        {
            return false;
        }
    
        return guardianInfo.ZkLoginInfo != null
               && guardianInfo.ZkLoginInfo.Nonce is not (null or "")
               && guardianInfo.ZkLoginInfo.Kid is not (null or "")
               && guardianInfo.ZkLoginInfo.ZkProof is not (null or "")
               && guardianInfo.ZkLoginInfo.Issuer is not (null or "")
               && guardianInfo.ZkLoginInfo.Salt is not (null or "")
               && guardianInfo.ZkLoginInfo.NoncePayload is not null;
    }
    
    public static bool IsValidZkOidcInfoSupportZkLogin(ZkLoginInfo zkLoginInfo)
    {
        if (zkLoginInfo == null)
        {
            return false;
        }
    
        return zkLoginInfo.Nonce is not (null or "")
               && zkLoginInfo.Kid is not (null or "")
               && zkLoginInfo.ZkProof is not (null or "")
               && zkLoginInfo.Issuer is not (null or "")
               && zkLoginInfo.Salt is not (null or "")
               && zkLoginInfo.NoncePayload is not null;
    }
    
    public static bool IsZkLoginSupported(GuardianType type)
    {
        return GuardianType.OfGoogle.Equals(type) || GuardianType.OfApple.Equals(type) ||
               GuardianType.OfFacebook.Equals(type);
    }

    public static bool CanZkLoginExecute(GuardianInfo guardianInfo)
    {
        return IsZkLoginSupported(guardianInfo.Type) && IsValidZkOidcInfoSupportZkLogin(guardianInfo.ZkLoginInfo);
    }

    public static bool IsValidGuardianType(GuardianType type)
    {
        return GuardianType.OfGoogle.Equals(type)
               || GuardianType.OfApple.Equals(type)
               || GuardianType.OfFacebook.Equals(type)
               || GuardianType.OfEmail.Equals(type)
               || GuardianType.OfTelegram.Equals(type)
               || GuardianType.OfTwitter.Equals(type);
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
}