using System;
using System.Collections.Generic;
using System.Text;

namespace Imageflow.Server.ServerHelpers
{
    public static class EncodingUtils
    {
        /// <summary>
        /// Converts arbitrary bytes to a URL-safe version of base64 (no = padding, with - instead of + and _ instead of /)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToBase64U(byte[] data)
        {
            return Convert.ToBase64String(data).Replace("=", String.Empty).Replace('+', '-').Replace('/', '_');
        }
        /// <summary>
        /// Converts a URL-safe version of base64 to a byte array.  (no = padding, with - instead of + and _ instead of /)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] FromBase64UToBytes(string data)
        {
            data = data.PadRight(data.Length + ((4 - data.Length % 4) % 4), '='); //if there is 1 leftover octet, add ==, if 2, add =. 3 octets = 4 chars.
            return Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/'));
        }
        /// <summary>
        /// Converts arbitrary strings to a URL-safe version of base64 (no = padding, with - instead of + and _ instead of /)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToBase64U(string data)
        {
            return ToBase64U(UTF8Encoding.UTF8.GetBytes(data));
        }
        /// <summary>
        /// Converts a URL-safe version of base64 to a string. 64U is (no = padding, with - instead of + and _ instead of /)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string FromBase64UToString(string data)
        {
            return UTF8Encoding.UTF8.GetString(FromBase64UToBytes(data));
        }

    }
}
