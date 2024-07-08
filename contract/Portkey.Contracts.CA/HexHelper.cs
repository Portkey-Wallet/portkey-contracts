using System;
using System.Collections.Generic;

namespace Portkey.Contracts.CA;

public static class HexHelper
{
    // public static byte[] HexStringToByteArray(this string hex)
    // {
    //     var length = hex.Length;
    //     var bytes = new byte[length / 2];
    //     for (var i = 0; i < length; i += 2)
    //     {
    //         bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
    //     }
    //
    //     return bytes;
    // }
    
    public static byte[] HexStringToByteArray(this string hex)
    {
        var length = hex.Length;
        var bytes = new List<byte>(length / 2);
        for (var i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes.ToArray();
    }
    
    public static string ConvertBigEndianToDecimalString(byte[] bytes)
    {
        var result = new List<int> { 0 };
        foreach (var b in bytes)
        {
            MultiplyBy256(result);
            AddByte(result, b);
        }
        result.Reverse();
        return string.Join("", result);
    }
    static void MultiplyBy256(List<int> number)
    {
        var carry = 0;
        for (var i = 0; i < number.Count; i++)
        {
            var product = number[i] * 256 + carry;
            number[i] = product % 10;
            carry = product / 10;
        }
        while (carry > 0)
        {
            number.Add(carry % 10);
            carry /= 10;
        }
    }
    static void AddByte(List<int> number, byte b)
    {
        int carry = b;
        for (var i = 0; i < number.Count; i++)
        {
            var sum = number[i] + carry;
            number[i] = sum % 10;
            carry = sum / 10;
            if (carry == 0) break;
        }
        while (carry > 0)
        {
            number.Add(carry % 10);
            carry /= 10;
        }
    }
}