using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Imazen.HybridCache
{
    public class HashBasedPathBuilder
    {
        private readonly int subfolderBits;

        public int SubfolderBits => subfolderBits;
        private char DisplayDirSeparator { get; }
        private string FileExtension { get; }
        private string PhysicalCacheDir { get; }
        public HashBasedPathBuilder(string physicalCacheDir, int subfolders, char displayDirSeparator, string fileExtension)
        {
            PhysicalCacheDir = physicalCacheDir.TrimEnd('\\', '/');
            FileExtension = fileExtension;
            DisplayDirSeparator = displayDirSeparator;
            subfolderBits = (int) Math.Ceiling(Math.Log(subfolders, 2)); //Log2 to find the number of bits. round up.
            if (subfolderBits < 1) subfolderBits = 1;
        }

        public int GetHashLengthInBytes()
        {
            return 256 / 8;
        }
        public byte[] HashKeyBasis(byte[] keyBasis)
        {
            using (var h = System.Security.Cryptography.SHA256.Create())
            {
                return h.ComputeHash(keyBasis);
            }
        }

        public string GetStringFromHash(byte[] hash)
        {
            return Convert.ToBase64String(hash);
        }
        public byte[] GetHashFromString(string hashString)
        {
            return Convert.FromBase64String(hashString);
        }
        
        public string GetPhysicalPathFromHash(byte[] hash)
        {
            var relativePath = BuildRelativePathForHash(hash, Path.DirectorySeparatorChar);
            return Path.Combine(PhysicalCacheDir, relativePath);
        }
        
        public string GetDisplayPathFromHash(byte[] hash)
        {
            return BuildRelativePathForHash(hash, '/');
        }

        /// <summary>
        /// Builds a key for the cached version, using the hashcode of `data`.
        /// I.e, 12/a1/d3/124211ab132592 or 12/0/12412ab12141.
        /// Key starts with a number that represents the number of bits required to hold the number of subfolders.
        /// Then a segment for each trailing byte of the hash, up to the number of subfolder bits
        /// We use trailing bytes so filenames that start the same aren't grouped together, which slows down some filesystems.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="dirSeparator"></param>
        /// <returns></returns>
        private string BuildRelativePathForHash(byte[] hash, char dirSeparator)
        {
            var allBits = GetTrailingBits(hash, subfolderBits);

            var sb = new StringBuilder(75 + FileExtension.Length);
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

            sb.Append(FileExtension);
            return sb.ToString();
        }


        internal static byte[] GetTrailingBits(byte[] data, int bits)
        {
            var trailingBytes = new byte[(int) Math.Ceiling(bits / 8.0)]; //Round up to bytes.
            Array.Copy(data, data.Length - trailingBytes.Length, trailingBytes, 0, trailingBytes.Length);
            var bitsToClear = (trailingBytes.Length * 8) - bits;
            trailingBytes[0] = (byte) ((byte)(trailingBytes[0] << bitsToClear) >> bitsToClear); //Set extra bits to 0.
            return trailingBytes;
        }

        public string GetDisplayPathForKeyBasis(byte[] keyBasis)
        {
            var hash = HashKeyBasis(keyBasis);
            return GetDisplayPathFromHash(hash);
        }
    }
}