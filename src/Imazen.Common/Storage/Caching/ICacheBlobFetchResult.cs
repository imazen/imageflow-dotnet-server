using System;

namespace Imazen.Common.Storage.Caching
{
    public interface ICacheBlobFetchResult: IDisposable
    {
        // We also need to include hints so we know the original request URI/key/bucket
        
        ICacheBlobData Data { get; }
        bool DataExists { get; }

        int StatusCode { get; }
        // HTTP Status Message
        string StatusMessage { get; }

    }
}
