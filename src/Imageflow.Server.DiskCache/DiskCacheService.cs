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
using Imazen.Common.Instrumentation.Support.InfoAccumulators;
using Imazen.Common.Issues;
using Imazen.DiskCache;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.DiskCache
{

    
 

    public class DiskCacheService: IBlobCache, IInfoProvider
    {
        private readonly DiskCacheOptions options;
        private readonly ClassicDiskCache cache;
        private readonly ILogger logger;
        private IBlobCache blobCacheImplementation;

        public string UniqueName => options.UniqueName;


        public DiskCacheService(DiskCacheOptions options, ILogger logger)
        {
            this.options = options;
            cache = new ClassicDiskCache(options, logger );
            this.logger = logger;
        }


        public IEnumerable<IIssue> GetIssues()
        {
            return cache.GetIssues();
        }
        public async Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken, bool retrieveContentType)
        {
            var strKey = Encoding.UTF8.GetString(key);
            var fileExtension = "jpg"; //TODO: fix this
            var classicCacheResult = await this.GetOrCreate(strKey, fileExtension,  async (outStream) => {
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

    
        public Task<ICacheResult> GetOrCreate(string key, string fileExtension, AsyncWriteResult writeCallback)
        {
            return cache.GetOrCreate(key, fileExtension, writeCallback);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return cache.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return cache.StopAsync(cancellationToken);
        }

        public void Add(IInfoAccumulator accumulator)
        {
            accumulator.Add("diskcache_subfolders", options.Subfolders);
            accumulator.Add("diskcache_autoclean", options.AutoClean);
            accumulator.Add("diskcache_asyncwrites", options.AsyncWrites);
                        /*
            diskcache_virtualpath /imagecache
            diskcache_drive_total 161059172352
            diskcache_drive_avail 38921302016
            diskcache_filesystem NTFS
            diskcache_network_drive 0
            diskcache_subfolders 8192
                */
                
        }

        public Task<IResult<IBlobWrapper, IBlobCacheFetchFailure>> CacheFetch(IBlobCacheRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult> CachePut(ICacheEventDetails e, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult<IAsyncEnumerable<IBlobStorageReference>>> CacheSearchByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
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

        public BlobCacheCapabilities InitialCacheCapabilities { get; }
        public ValueTask<IBlobCacheHealthDetails> CacheHealthCheck(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}