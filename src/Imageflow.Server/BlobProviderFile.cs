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
            return new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        public void Dispose()
        {
        }
    }
}