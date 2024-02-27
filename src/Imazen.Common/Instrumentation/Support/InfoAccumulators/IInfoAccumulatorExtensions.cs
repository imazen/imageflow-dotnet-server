﻿using System.Text;
using Imazen.Common.Helpers;

namespace Imazen.Common.Instrumentation.Support.InfoAccumulators
{
    public static class InfoAccumulatorExtensions
    {

        public static void Add(this IInfoAccumulator a, string key, Guid value)
        {
            a.AddString(key, EncodingUtils.ToBase64U(value.ToByteArray()));
        }

        public static void Add(this IInfoAccumulator a, IEnumerable<KeyValuePair<string, string>> items)
        {
            foreach (var pair in items)
            {
                a.AddString(pair.Key, pair.Value);
            }
        }

        public static void Add(this IInfoAccumulator a, string key, bool? value)
        {
            a.AddString(key, value?.ToShortString());
        }

        public static void Add(this IInfoAccumulator a, string key, long? value)
        {
            a.AddString(key, value?.ToString());
        }

        public static void Add(this IInfoAccumulator a, string key, string? value)
        {
            a.AddString(key, value);
        }
        public static string ToQueryString(this IInfoAccumulator a, int characterLimit)
        {
            const string truncated = "truncated=true";
            var limit = characterLimit - truncated.Length;
            var pairs = a.GetInfo().Where(pair => pair is { Value: not null, Key: not null })
                     .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + (pair.Value == null ? "" : Uri.EscapeDataString(pair.Value)));
            var sb = new StringBuilder(1000);
            sb.Append("?");
            foreach (var s in pairs)
            {
                if (sb[sb.Length - 1] != '?') sb.Append("&");

                if (sb.Length + s.Length > limit) {
                    sb.Append(truncated);
                    return sb.ToString();
                }
                sb.Append(s);
            }
            return sb.ToString();
        }


    }
}
