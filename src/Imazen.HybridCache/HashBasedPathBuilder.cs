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
        private char RelativeDirSeparator { get; }
        private string FileExtension { get; }
        private string PhysicalCacheDir { get; }
        public HashBasedPathBuilder(string physicalCacheDir, int subfolders, char relativeDirSeparator, string fileExtension)
        {
            PhysicalCacheDir = physicalCacheDir.TrimEnd('\\', '/');
            FileExtension = fileExtension;
            RelativeDirSeparator = relativeDirSeparator;
            subfolderBits = (int) Math.Ceiling(Math.Log(subfolders, 2)); //Log2 to find the number of bits. round up.
            if (subfolderBits < 1) subfolderBits = 1;
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
            var relativePath = GetRelativePathFromHash(hash);
            return GetPhysicalPathFromRelativePath(relativePath);
        }

        public string GetPhysicalPathFromRelativePath(string relativePath)
        {
            var fixSlashes = relativePath.Replace(RelativeDirSeparator, Path.DirectorySeparatorChar);
            return Path.Combine(PhysicalCacheDir, fixSlashes);
        }
    

        /// <summary>
        /// Builds a key for the cached version, using the hashcode of `data`.
        /// I.e, 12/a1/d3/124211ab132592 or 12/0/12412ab12141.
        /// Key starts with a number that represents the number of bits required to hold the number of subfolders.
        /// Then a segment for each trailing byte of the hash, up to the number of subfolder bits
        /// We use trailing bytes so filenames that start the same aren't grouped together, which slows down some filesystems.
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public string GetRelativePathFromHash(byte[] hash)
        {
            var allBits = GetTrailingBits(hash, subfolderBits);

            var sb = new StringBuilder(75 + FileExtension.Length);
            //Start with the subfolder distribution in bits, so we can easily delete old folders
            //When we change the subfolder size
            sb.AppendFormat(NumberFormatInfo.InvariantInfo, "{0:D}",subfolderBits);
            sb.Append(Path.DirectorySeparatorChar);
            //If subfolders is set above 256, it will nest files in multiple directories, one for each byte
            foreach (var b in allBits)
            {
                sb.AppendFormat(NumberFormatInfo.InvariantInfo, "{0:x2}",b);
                sb.Append(Path.DirectorySeparatorChar);
            }

            foreach (var b in hash)
                sb.AppendFormat(NumberFormatInfo.InvariantInfo, "{0:x2}",b);

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
            return GetRelativePathFromHash(hash);
        }
    }
}