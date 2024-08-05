using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Groth16.Net;

namespace Portkey.Contracts.CA;

public partial class CAContract
{
    private const int CircomBigIntN = 64;
    private const int CircomBigIntK = 32;

    private bool CheckZkLoginVerifierAndData(GuardianInfo guardianInfo, Hash caHash)
    {
        if (!DoCheckZkLoginBasicParams(guardianInfo, caHash))
        {
            return false;
        }
        
        //check public key
        var publicKey = State.PublicKeysChunksByKid[guardianInfo.Type][guardianInfo.ZkLoginInfo.Kid];
        if (publicKey is null || (publicKey.PublicKey is (null or "")))
        {
            return false;
        }
        
        //check verifying key
        var verifyingKey = State.CircuitVerifyingKeys[guardianInfo.ZkLoginInfo.CircuitId];
        if (verifyingKey == null)
        {
            return false;
        }

        return VerifyZkProof(guardianInfo.Type, guardianInfo.ZkLoginInfo, verifyingKey.VerifyingKey_, publicKey.PublicKey);
    }

    private bool DoCheckZkLoginBasicParams(GuardianInfo guardianInfo, Hash caHash)
    {
        //check the caHash
        if (caHash == null || caHash.Equals(Hash.Empty))
        {
            return false;
        }
        //check circuit id
        if (State.CircuitVerifyingKeys[guardianInfo.ZkLoginInfo.CircuitId] == null)
        {
            return false;
        }
        //check identifier hash
        if (guardianInfo.ZkLoginInfo.IdentifierHashType == IdentifierHashType.Sha256Hash)
        {
            if (!guardianInfo.IdentifierHash.Equals(guardianInfo.ZkLoginInfo.IdentifierHash))
            {
                return false;
            }
        }
        else
        {
            if (!guardianInfo.PoseidonIdentifierHash.Equals(guardianInfo.ZkLoginInfo.PoseidonIdentifierHash))
            {
                return false;
            }
        }
        // check jwt issuer
        if (State.OidcProviderData[guardianInfo.Type].Issuer == null
            || !State.OidcProviderData[guardianInfo.Type].Issuer.Equals(guardianInfo.ZkLoginInfo.Issuer))
        {
            return false;
        }
        // check nonce
        if (string.IsNullOrWhiteSpace(guardianInfo.ZkLoginInfo.Nonce))
        {
            return false;
        }
        //check nonce wasn't used before
        var currentTime =  Context.CurrentBlockTime;
        var nonces = InitZkNonceInfos(caHash, currentTime);
        if (nonces.Contains(guardianInfo.ZkLoginInfo.Nonce))
        {
            return false;
        }
        else
        {
            State.ZkNonceInfosByCaHash[caHash].ZkNonceInfos.Add(new ZkNonceInfo
            {
                Nonce = guardianInfo.ZkLoginInfo.Nonce,
                Datetime = DateTime.SpecifyKind(currentTime.ToDateTime(), DateTimeKind.Utc).ToString()
            });
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

    private List<string> InitZkNonceInfos(Hash caHash, Timestamp currentTime)
    {
        State.ZkNonceInfosByCaHash[caHash] ??= new ZkNonceList();
        //clear overdue nonces, prevent the nonce data growing all the time
        foreach (var zkNonceInfo in State.ZkNonceInfosByCaHash[caHash].ZkNonceInfos)
        {
            var nonceDatetime = DateTime.SpecifyKind(Convert.ToDateTime(zkNonceInfo.Datetime), DateTimeKind.Utc);
            if (nonceDatetime.ToTimestamp().AddDays(1) <= currentTime)
            {
                State.ZkNonceInfosByCaHash[caHash].ZkNonceInfos.Remove(zkNonceInfo);
            }
        }

        return State.ZkNonceInfosByCaHash[caHash].ZkNonceInfos.Select(nonce => nonce.Nonce).ToList();
    }

    private bool VerifyZkProof(GuardianType type, ZkLoginInfo zkLoginInfo, string verifyingKey, string pubkey)
    {
        var pubkeyChunks = GetPublicKeyChunks(type, zkLoginInfo.Kid, pubkey);
        
        var nonceInInts = zkLoginInfo.Nonce.ToCharArray().Select(b => ((int)b).ToString()).ToList();
        var saltInInts = zkLoginInfo.Salt.HexStringToByteArray().Select(b => b.ToString()).ToList();
        
        var publicInputs = PreparePublicInputs(zkLoginInfo, nonceInInts, pubkeyChunks, saltInInts);
        PrepareZkProof(zkLoginInfo, out var piA, out var piB, out var piC);
        return Verifier.VerifyBn254(verifyingKey, publicInputs, new RapidSnarkProof
        {
            PiA = piA,
            PiB = piB,
            PiC = piC
        });
    }

    private static void PrepareZkProof(ZkLoginInfo zkLoginInfo,
        out List<string> piA, out List<List<string>> piB, out List<string> piC)
    {
        piA = zkLoginInfo.ZkProofInfo.ZkProofPiA.ToList();
        piB = new List<List<string>> {new(), new(), new()};
        piB[0].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB1.ToList());
        piB[1].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB2.ToList());
        piB[2].AddRange(zkLoginInfo.ZkProofInfo.ZkProofPiB3.ToList());
        piC = zkLoginInfo.ZkProofInfo.ZkProofPiC.ToList();
    }

    private List<string> PreparePublicInputs(ZkLoginInfo zkLoginInfo, List<string> nonceInInts, List<string> pubkeyChunks, List<string> saltInInts)
    {
        var publicInputs = new List<string>();
        publicInputs.AddRange(ToPublicInput(zkLoginInfo.IdentifierHashType, zkLoginInfo.IdentifierHash.ToHex()));
        publicInputs.AddRange(nonceInInts);
        publicInputs.AddRange(pubkeyChunks);
        publicInputs.AddRange(saltInInts);
        return publicInputs;
    }

    private List<string> GetPublicKeyChunks(GuardianType type, string kid, string publicKey)
    {
        var zkPublicKeyInfo = State.PublicKeysChunksByKid[type][kid];
        if (zkPublicKeyInfo != null && zkPublicKeyInfo.PublicKey.Equals(publicKey)
            && zkPublicKeyInfo.PublicKeyChunks != null && zkPublicKeyInfo.PublicKeyChunks.Count > 0)
        {
            return zkPublicKeyInfo.PublicKeyChunks.ToList();
        }

        return GeneratePublicKeyChunks(publicKey).ToList();
    }

    private void SetPublicKeyAndChunks(GuardianType type, string kid, string publicKey)
    {
        if (State.PublicKeysChunksByKid[type][kid] != null
            && State.PublicKeysChunksByKid[type][kid].PublicKey.Equals(publicKey)
            && State.PublicKeysChunksByKid[type][kid].PublicKeyChunks != null
            && State.PublicKeysChunksByKid[type][kid].PublicKeyChunks.Count == CircomBigIntK)
        {
            return;
        }

        State.PublicKeysChunksByKid[type][kid] = new ZkPublicKeyInfo()
        {
            Kid = kid,
            PublicKey = publicKey,
            PublicKeyChunks = { GeneratePublicKeyChunks(publicKey) }
        };
    }

    private RepeatedField<string> GeneratePublicKeyChunks(string pubkey)
    {
        var result = new RepeatedField<string>();
        result.AddRange(Decode(pubkey)
            .ToChunked(CircomBigIntN, CircomBigIntK)
            .Select(HexToBigInt));
        return result;
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

    private List<string> ToPublicInput(IdentifierHashType identifierHashType, string identifierHash)
    {
        if (IdentifierHashType.PoseidonHash.Equals(identifierHashType))
        {
            return PoseidonHashToPublicInput(identifierHash);
            
        }
        return Sha256HashToPublicInput(identifierHash);
    }
    
    //Sha256 Hash
    private List<string> Sha256HashToPublicInput(string identifierHash)
    {
        return identifierHash.HexStringToByteArray().Select(b => b.ToString()).ToList();
    }
    
    //poseidon Hash
    private List<string> PoseidonHashToPublicInput(string identifierHash)
    {
        return new List<string>{ identifierHash };
    }
    
    private static string HexToBigInt(byte[] hex)
    {
        return HexHelper.ConvertBigEndianToDecimalString(hex);
    }
    
    public static bool IsValidZkOidcInfoSupportZkLogin(ZkLoginInfo zkLoginInfo)
    {
        return zkLoginInfo is not null 
               && zkLoginInfo.Nonce is not (null or "")
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

    private static bool CanZkLoginExecute(GuardianInfo guardianInfo)
    {
        return guardianInfo is not null && IsZkLoginSupported(guardianInfo.Type) && IsValidZkOidcInfoSupportZkLogin(guardianInfo.ZkLoginInfo);
    }
    
    private static bool CanGuardianZkLoginExecute(Guardian guardian)
        {
            return guardian is not null && IsZkLoginSupported(guardian.Type) && IsValidZkOidcInfoSupportZkLogin(guardian.ZkLoginInfo);
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
    
    private bool CheckZkParams(GuardianInfo guardianInfo)
    {
        var supportedZk = CanZkLoginExecute(guardianInfo);
        if (!supportedZk)
            return false;
        Assert(guardianInfo.ZkLoginInfo.ZkProofInfo != null, "the zkProofInfo is null");
                
        var zkProofPiA = guardianInfo.ZkLoginInfo.ZkProofInfo.ZkProofPiA;
        Assert(zkProofPiA is { Count: 3 }, "the zkProofInfo zkProofPiA is invalid");
                
        var zkProofPiB1 = guardianInfo.ZkLoginInfo.ZkProofInfo.ZkProofPiB1;
        Assert(zkProofPiB1 is { Count: 2 }, "the zkProofInfo zkProofPiB1 is invalid");
                
        var zkProofPiB2 = guardianInfo.ZkLoginInfo.ZkProofInfo.ZkProofPiB2;
        Assert(zkProofPiB2 is { Count: 2 }, "the zkProofInfo zkProofPiB2 is invalid");
                
        var zkProofPiB3 = guardianInfo.ZkLoginInfo.ZkProofInfo.ZkProofPiB3;
        Assert(zkProofPiB3 is { Count: 2 }, "the zkProofInfo zkProofPiB3 is invalid");
                
        var zkProofPiC = guardianInfo.ZkLoginInfo.ZkProofInfo.ZkProofPiC;
        Assert(zkProofPiC is { Count: 3 }, "the zkProofInfo zkProofPiC is invalid");

        return true;
    }
}