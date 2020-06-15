using System;
using System.IO;
using Imazen.Common.Storage;

namespace Imageflow.Server
{
    internal class BlobProviderFile : IBlobData
    {
        public string Path;
        public bool? Exists { get; set; }
        public DateTime? LastModifiedDateUtc { get; set; }
        public Stream OpenRead()
        {
            return File.OpenRead(Path);
        }

        public void Dispose()
        {
        }
    }
}