﻿using System.Reflection;
using System.Text;
using Imazen.Common.Helpers;

// ReSharper disable LoopVariableIsNeverChangedInsideLoop

namespace Imazen.Common.Instrumentation.Support
{
    internal static class Utilities
    {
        
        public static string Sha256Hex(string input)
        {
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
        }

        public static string Sha256Base64(string input)
        {
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return EncodingUtils.ToBase64U(hash);
        }

        public static string Sha256TruncatedBase64(string input, int bytes)
        {
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return EncodingUtils.ToBase64U(hash.Take(bytes).ToArray());
        }

        /// Returns the original value
        public static long InterlockedMax(ref long location1, long other)
        {
            long copy;
            long finalOriginal;
            do {
                copy = Interlocked.Read(ref location1);
                if (copy >= other) return copy;
                finalOriginal = Interlocked.CompareExchange(ref location1, other, copy);
            } while (finalOriginal != copy) ;
            return finalOriginal;
        }
        /// Returns the original value
        public static long InterlockedMin(ref long location1, long other)
        {
            long copy;
            long finalOriginal;
            do {
                copy = Interlocked.Read(ref location1);
                if (copy <= other) return copy;
                finalOriginal = Interlocked.CompareExchange(ref location1, other, copy);
            } while (finalOriginal != copy) ;
            return finalOriginal;
        }

    }

    static class BoolExtensions
    {
        public static string ToShortString(this bool b)
        {
            return b ? "1" : "0";
        }

    }
    static class StringExtensions
    {
        /// <summary>
        /// Only lowercases A..Z -> a..z, and only if req.d.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ToLowerOrdinal(this string s)
        {
            StringBuilder? b = null;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c is < 'A' or > 'Z') continue;
                b ??= new StringBuilder(s);
                b[i] = (char)(c + 0x20);
            }
            return b?.ToString() ?? s;
        }
    }

    static class AssemblyExtensions
    {
        public static string IntoString(this IEnumerable<char> c) => string.Concat(c);

        public static T? GetFirstAttribute<T>(this Assembly a)
        {
            try
            {
                var attrs = a.GetCustomAttributes(typeof(T), false);
                if (attrs.Length > 0) return (T)attrs[0];
            }
            catch(FileNotFoundException) {
                //Missing dependencies
            }
            catch (Exception)
            {
                // ignored
            }

            return default(T);
        }

        public static Exception? GetExceptionForReading<T>(this Assembly a)
        {
            try {
                var _ = a.GetCustomAttributes(typeof(T), false);
            } catch (Exception e) {
                return e;
            }
            return null;
        }


        // public static string GetShortCommit(this Assembly a) =>
        //     GetFirstAttribute<CommitAttribute>(a)?.Value.Take(8).IntoString();
        //
        // public static string GetEditionCode(this Assembly a) =>
        //     GetFirstAttribute<EditionAttribute>(a)?.Value;

        public static string? GetInformationalVersion(this Assembly a)
        {
            return GetFirstAttribute<AssemblyInformationalVersionAttribute>(a)?.InformationalVersion;
        }
        public static string? GetFileVersion(this Assembly a)
        {
            return GetFirstAttribute<AssemblyFileVersionAttribute>(a)?.Version;
        }
    }


    static class PercentileExtensions
    {
        public static long GetPercentile(this long[] data, float percentile)
        {
            if (data.Length == 0)
            {
                return 0;
            }
            var index = Math.Max(0, percentile * data.Length + 0.5f);

            return (data[(int)Math.Max(0, Math.Ceiling(index - 1.5))] +
                    data[(int)Math.Min(Math.Ceiling(index - 0.5), data.Length - 1)]) / 2;


        }

    }

}
