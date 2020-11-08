using System.Collections.Generic;

namespace Imazen.Common.ExtensionMethods
{
    internal static class GenericExtensions
    {
        public static string Delimited<T>(this IEnumerable<T> values, string separator) =>
            string.Join(separator, values);

        public static string Delimited(this string[] values, string separator) =>
            string.Join(separator, values);
    }
}