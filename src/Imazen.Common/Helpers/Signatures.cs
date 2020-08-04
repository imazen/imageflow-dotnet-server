using System;
using System.Security.Cryptography;
using System.Text;

namespace Imazen.Common.Helpers
{
    public static class Signatures
    {
        public static string SignString(string data, string key, int signatureLengthInBytes)
        {
            if (signatureLengthInBytes < 1 || signatureLengthInBytes > 32) throw new ArgumentOutOfRangeException(nameof(signatureLengthInBytes));
            HMACSHA256 hmac = new HMACSHA256(UTF8Encoding.UTF8.GetBytes(key));
            byte[] hash = hmac.ComputeHash(UTF8Encoding.UTF8.GetBytes(data));
            //32-byte hash is a bit overkill. Truncation only marginally weakens the algorithm integrity.
            byte[] shorterHash = new byte[signatureLengthInBytes];
            Array.Copy(hash, shorterHash, signatureLengthInBytes);
            return EncodingUtils.ToBase64U(shorterHash);
        }
    }
}