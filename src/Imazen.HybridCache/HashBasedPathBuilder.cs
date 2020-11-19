using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Imazen.HybridCache
{
    public class HashBasedPathBuilder
    {
        private readonly int subfolderBits;

        public int SubfolderBits => subfolderBits;
        private readonly int subfolders;
        public HashBasedPathBuilder(int subfolders)
        {
            this.subfolders = subfolders;
            subfolderBits = (int) Math.Ceiling(Math.Log(subfolders, 2)); //Log2 to find the number of bits. round up.
            if (subfolderBits < 1) subfolderBits = 1;
            
        }
        /// <summary>
        /// Builds a key for the cached version, using the hashcode of `data`.
        /// I.e, 12\a1\d3\124211ab132592 or 12\0\12412ab12141.
        /// Key starts with a number that represents the number of bits required to hold the number of subfolders.
        /// Then a segment for each trailing byte of the hash, up to the number of subfolder bits
        /// We use trailing bytes so filenames that start the same aren't grouped together, which slows down some filesystems.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="subfolders"></param>
        /// <param name="dirSeparator"></param>
        /// <returns></returns>
        public string BuildRelativePathForData(byte[] data, string dirSeparator)
        {

            using (var h = System.Security.Cryptography.SHA256.Create())
            {
                var hash = h.ComputeHash(data);

                var allBits = GetTrailingBits(hash, subfolderBits);
                
                var sb = new StringBuilder(75);
                //Start with the subfolder distribution in bits, so we can easily delete old folders
                //When we change the subfolder size
                sb.Append(subfolderBits.ToString("D", NumberFormatInfo.InvariantInfo));
                sb.Append(dirSeparator);
                //If subfolders is set above 256, it will nest files in multiple directories, one for each byte
                foreach (var b in allBits)
                {
                    sb.Append(b
                        .ToString("x", NumberFormatInfo.InvariantInfo)
                        .PadLeft(2, '0'));
                    sb.Append(dirSeparator);
                }
                foreach (var b in hash)
                    sb.Append(b.ToString("x", NumberFormatInfo.InvariantInfo).PadLeft(2, '0'));
                return sb.ToString();
            }
        }
        internal static byte[] GetTrailingBits(byte[] data, int bits)
        {
            var trailingBytes = new byte[(int) Math.Ceiling(bits / 8.0)]; //Round up to bytes.
            Array.Copy(data, data.Length - trailingBytes.Length, trailingBytes, 0, trailingBytes.Length);
            var bitsToClear = (trailingBytes.Length * 8) - bits;
            trailingBytes[0] = (byte) ((byte)(trailingBytes[0] << bitsToClear) >> bitsToClear); //Set extra bits to 0.
            return trailingBytes;
        }

    }
}