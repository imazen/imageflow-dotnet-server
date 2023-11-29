
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Common.Extensibility.ClassicDiskCache;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;

namespace Imageflow.Server.DiskCache{
    internal class ClassicDiskCacheToBlobCacheAdapter: IBlobCache{

        readonly IClassicDiskCache diskCache;
        public ClassicDiskCacheToBlobCacheAdapter(string uniqueName, IClassicDiskCache diskCache){
            this.diskCache = diskCache;
            this.UniqueName = uniqueName;
        }

        public string UniqueName { get; }

        public IEnumerable<IIssue> GetIssues() => diskCache.GetIssues();

        public BlobCacheCapabilities InitialCacheCapabilities { get; }
        public Task<IResult<IBlobWrapper, IBlobCacheFetchFailure>> CacheFetch(IBlobCacheRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult> CachePut(ICacheEventDetails e, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        public async Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken, bool retrieveContentType)
        {
            var strKey = Encoding.UTF8.GetString(key);
            var fileExtension = "jpg"; //TODO: fix this
            var classicCacheResult = await diskCache.GetOrCreate(strKey, fileExtension,  async (outStream) => {
                var dataResult = await dataProviderCallback(cancellationToken);
                await outStream.WriteAsync(dataResult.Bytes.Array,0, dataResult.Bytes.Count,
                    CancellationToken.None);
                await outStream.FlushAsync();
            });

 
            var statusString = classicCacheResult.Result switch
            {
                CacheQueryResult.Hit => classicCacheResult.Data != null ? "MemoryHit" : "DiskHit",
                CacheQueryResult.Miss => "Miss",
                CacheQueryResult.Failed => "TimeoutAndFailed",
                _ => "Unknown"
            };
            
            var result = new StreamCacheResult(){
                Data = classicCacheResult.Data ?? File.OpenRead(classicCacheResult.PhysicalPath),
                ContentType = null,
                Status = statusString
            };
            return result;
        }

    
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return diskCache.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return diskCache.StopAsync(cancellationToken);
        }


        public Task<CodeResult<IAsyncEnumerable<IBlobStorageReference>>> CacheSearchByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult<IList<IBlobStorageReference>>> CacheSearchByTag(string tag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult<IList<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(string tag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult> CacheDelete(IBlobStorageReference reference, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult> OnCacheEvent(ICacheEventDetails e, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<IBlobCacheHealthDetails> CacheHealthCheck(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}