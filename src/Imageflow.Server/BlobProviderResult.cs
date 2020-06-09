using System;
using System.Threading.Tasks;
using Imazen.Common.Storage;

namespace Imageflow.Server
{
    internal struct BlobProviderResult
    {
        internal bool IsFile;
        internal Func<Task<IBlobData>> GetBlob;
    }

}