using System.Collections.Generic;

namespace Portkey.Contracts.CA;

public static class BytesExtension
{
    private static void ShiftArrayRight(byte[] array, int shiftBits)
    {
      int shiftBytes = shiftBits / 8;
      int num1 = shiftBits % 8;
      if (num1 == 0)
      {
        ShiftBytesRight(array, shiftBytes);
      }
      else
      {
        int num2 = (8 - num1) % 8;
        int length = array.Length;
        int num3 = length - shiftBytes;
        int num4 = length - 1;
        for (int index1 = 0; index1 < num3; ++index1)
        {
          int num5 = index1 + shiftBytes;
          int index2 = num4 - index1;
          int index3 = num4 - num5;
          int num6 = (int) array[index3];
          byte num7 = (byte) ((index3 > 0 ? (int) array[index3 - 1] : 0) << num2 & 0xff); //& 0xff
          int num8 = num1 & 31;
          byte num9 = (byte) (num6 >> num8);
          array[index2] = (byte) ((uint) num7 | (uint) num9);
        }
        for (int index = 0; index < shiftBytes; ++index)
          array[index] = (byte) 0;
      }
    }

    private static void ShiftBytesRight(byte[] array, int shiftBytes)
    {
      int length = array.Length;
      int num1 = length - shiftBytes;
      int num2 = length - 1;
      for (int index1 = 0; index1 < num1; ++index1)
      {
        int num3 = index1 + shiftBytes;
        int index2 = num2 - index1;
        int index3 = num2 - num3;
        array[index2] = array[index3];
      }
      for (int index = 0; index < shiftBytes; ++index)
        array[index] = (byte) 0;
    }
    
    private static byte[] Mask(byte[] array, int maskBits)
    {
      int length1 = (maskBits - 1) / 8 + 1;
      int num1 = maskBits % 8;
      int length2 = array.Length;
      var numList = new List<byte>(length1);
      for (var i = 0; i < length1; i++)
        numList.Add(0);
      int num2 = length1 - 1;
      int num3 = length2 - 1;
      for (int index = 0; index < length1; ++index)
        numList[num2 - index] = array[num3 - index];
      byte num4 = 0;
      for (int index = 0; index < num1; ++index)
        num4 |= (byte) (1 << index);
      if (num1 > 0)
        numList[0] = (byte) ((uint) numList[0] & (uint) num4);
      return numList.ToArray();
    }

    public static IList<byte[]> ToChunked(this byte[] bytes, int bytesPerChunk, int numOfChunks)
    {
      List<byte[]> chunked = new List<byte[]>();
      for (int index = 0; index < numOfChunks; ++index)
      {
        chunked.Add(Mask(bytes, bytesPerChunk));
        ShiftArrayRight(bytes, bytesPerChunk);
      }
      return chunked;
    }
}