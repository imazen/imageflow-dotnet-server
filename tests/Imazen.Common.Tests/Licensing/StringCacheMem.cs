using System;
using System.Collections.Concurrent;
using Imazen.Common.Persistence;

namespace Imazen.Common.Tests.Licensing
{
    internal class StringCacheMem : IPersistentStringCache
    {
        readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
        readonly ConcurrentDictionary<string, DateTime> cacheWrite = new ConcurrentDictionary<string, DateTime>();

        public StringCachePutResult TryPut(string key, string value)
        {
            string current;
            if (cache.TryGetValue(key, out current) && current == value) {
                return StringCachePutResult.Duplicate;
            }
            cache[key] = value;
            cacheWrite[key] = DateTime.UtcNow;
            return StringCachePutResult.WriteComplete;
        }

        public string Get(string key)
        {
            string current;
            if (cache.TryGetValue(key, out current)) {
                return current;
            }
            return null;
        }

        public DateTime? GetWriteTimeUtc(string key)
        {
            DateTime current;
            if (cacheWrite.TryGetValue(key, out current))
            {
                return current;
            }
            return null;
        }
    }
}