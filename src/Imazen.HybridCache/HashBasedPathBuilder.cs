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

            sb.AppendFormat(NumberFormatInfo.InvariantInfo, 
                "{0:x2}{1:x2}{2:x2}{3:x2}{4:x2}{5:x2}{6:x2}{7:x2}{8:x2}{9:x2}{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}{16:x2}{17:x2}{18:x2}{19:x2}{20:x2}{21:x2}{22:x2}{23:x2}{24:x2}{25:x2}{26:x2}{27:x2}{28:x2}{29:x2}{30:x2}{31:x2}",
                hash[0], hash[1], hash[2], hash[3], hash[4], hash[5], hash[6], hash[7], hash[8], hash[9], hash[10], hash[11]
                , hash[12], hash[13], hash[14], hash[15], hash[16], hash[17], hash[18], hash[19], hash[20], hash[21], hash[22], hash[23]
                , hash[24], hash[25], hash[26], hash[27], hash[28], hash[29], hash[30], hash[31]);

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