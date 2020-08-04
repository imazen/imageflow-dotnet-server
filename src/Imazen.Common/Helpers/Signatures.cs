using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

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

        public static string NormalizePathAndQueryForSigning(string pathAndQuery)
        {
            
            var parts = pathAndQuery.Split('?');
            if (parts.Length > 2)
            {
                throw new ArgumentException("Multiple query delimiters (?) found", nameof(pathAndQuery));
            }

            var path = parts[0];
            //URL encode path
            var newPathAndQuery = System.Web.HttpUtility.UrlDecode(path);
            if (parts.Length > 1)
            {
                var query = parts[1];
                //Split up query
                var pairs = query.Split('&');
                // Remove &signature= query pair
                var newQuery = "";
                foreach (var pair in pairs)
                {
                    if (!pair.StartsWith("signature=", StringComparison.Ordinal))
                    {
                        var pairParts = pair.Split('=');
                        newQuery += "&" + HttpUtility.UrlDecode(pairParts[0]);
                        for (var i = 1; i < pairParts.Length; i++)
                        {
                            newQuery += "=" + HttpUtility.UrlDecode(pairParts[i]); 
                        }
                        
                    }
                }
                
                if (newQuery.Length > 0) newPathAndQuery += "?" + newQuery.TrimStart('&');
            }
            Console.WriteLine($"Normalized url from {pathAndQuery} to {newPathAndQuery}");
            return newPathAndQuery;
        }

        public static string SignRequest(string pathAndQuery, string key)
        {
            var normalized = NormalizePathAndQueryForSigning(pathAndQuery);
            var signature = SignString(normalized, key, 16);
            return normalized + (normalized.Contains("?") ? "&" : "?") + "signature=" + signature;
        }
    }
}