using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using Groth16.Net;

namespace Portkey.Contracts.CA;

public static class ZkHelpers
{
    public static bool VerifyBn254(ZkGuardianInfo zkGuardianInfo, string verifyingKey)
    {
        // Prepare public input
        var identifier = zkGuardianInfo.IdentifierHash.ToByteArray().Select(x => x.ToString());
        var salt = HexStringToByteArray16(zkGuardianInfo.Salt).Select(x => x.ToString());
        var pubKey = zkGuardianInfo.IssuerPubkey.HexToChunkedBytes(121, 17).Select(x => x.HexToBigInt());

        var publicInputs = identifier.Concat(pubKey).Concat(salt).ToList();

        return Verifier.VerifyBn254(verifyingKey, publicInputs, zkGuardianInfo.Proof.ToHex());
    }

    static byte[] HexStringToByteArray16(string hex)
    {
        var byteArray = new byte[16];

        for (var i = 0; i < 16; i += 2)
        {
            byteArray[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return byteArray;
    }
}