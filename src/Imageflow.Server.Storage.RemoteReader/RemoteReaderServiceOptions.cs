using System;
using System.Collections.Generic;

namespace Imageflow.Server.Storage.RemoteReader
{
    public class RemoteReaderServiceOptions
    {
        internal readonly List<string> Prefixes = new List<string>();
        public string SigningKey { get; set; }
        public bool IgnorePrefixCase { get; set; }

        public RemoteReaderServiceOptions AddPrefix(string prefix)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';

            Prefixes.Add(prefix);
            return this;
        }
    }
}
