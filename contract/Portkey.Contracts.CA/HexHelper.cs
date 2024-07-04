using System;

namespace Portkey.Contracts.CA;

public static class HexHelper
{
    public static byte[] HexStringToByteArray(this string hex)
    {
        var length = hex.Length;
        var bytes = new byte[length / 2];
        for (var i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }
}