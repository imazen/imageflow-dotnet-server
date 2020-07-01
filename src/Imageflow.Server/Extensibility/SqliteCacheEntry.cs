using System;

namespace Imageflow.Server.Extensibility
{
    public class SqliteCacheEntry
    {
        public string ContentType { get; set; }
        public byte[] Data { get; set; }
    }
}