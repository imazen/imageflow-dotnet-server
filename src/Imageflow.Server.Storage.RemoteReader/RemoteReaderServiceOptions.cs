using System;
using System.Collections.Generic;
using System.Text;

namespace Imageflow.Server.Storage.RemoteReader
{
    public class RemoteReaderServiceOptions
    {
        internal readonly List<string> _prefixes = new List<string>();

        public int RedirectLimit { get; set; } = 5;
        public string SigningKey { get; set; }
        public string UserAgent { get; set; } = "ImageFlow-DotNet-Server";
        
        public bool IgnorePrefixCase { get; set; }

        public RemoteReaderServiceOptions AddPrefix(string prefix)
        {
            prefix = prefix.TrimStart('/').TrimEnd('/');
            if (prefix.Length == 0)
            {
                throw new ArgumentException("Prefix cannot be /", nameof(prefix));
            }

            prefix = '/' + prefix + '/';

            _prefixes.Add(prefix);
            return this;
        }
    }
}
