using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Imazen.Abstractions.BlobCache;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;
using System.Linq;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;

namespace Imageflow.Server.Storage.AzureBlob.Caching
{

    /// <summary>
    ///  Best used with Azure Block Blob Premium Storage (SSD) (LRS and Flat Namespace are fine; GRS is not needed for caches). Costs ~20% more but is much faster.
    /// </summary>
    internal class AzureBlobCache : IBlobCache
    {
        private NamedCacheConfiguration config;
        private BlobServiceClient defaultBlobServiceClient;
        private ILogger logger;

        private Tuple<string, bool>[] containerExistenceCache;

        private Dictionary<BlobGroup, BlobServiceClient> serviceClients;
        private bool GetContainerExistsMaybe(string containerName)
        {
            var result = containerExistenceCache.FirstOrDefault(x => x.Item1 == containerName);
            if (result == null)
            {
                return false;
            }
            return result.Item2;
        }
        private void SetContainerExists(string containerName, bool exists)
        {
            for (int i = 0; i < containerExistenceCache.Length; i++)
            {
                if (containerExistenceCache[i].Item1 == containerName)
                {
                    containerExistenceCache[i] = new Tuple<string, bool>(containerName, exists);
                }
            }
        }

// https://devblogs.microsoft.com/azure-sdk/best-practices-for-using-azure-sdk-with-asp-net-core/

        public AzureBlobCache(NamedCacheConfiguration config, Func<string?, BlobServiceClient> blobServiceFactory,
            ILoggerFactory loggerFactory)
        {
            this.config = config;
            // Map BlobGroupConfigurations dict, replacing those keys with the .Location.BlobClient value

            this.serviceClients = config.BlobGroupConfigurations.Select(p =>
                new KeyValuePair<BlobGroup, BlobServiceClient>(p.Key,
                    p.Value.Location.AzureClient.Resolve(blobServiceFactory))).ToDictionary(x => x.Key, x => x.Value);

            this.defaultBlobServiceClient = blobServiceFactory(null);
            this.logger = loggerFactory.CreateLogger("AzureBlobCache");

            this.containerExistenceCache = config.BlobGroupConfigurations.Values.Select(x => x.Location.ContainerName)
                .Distinct().Select(x => new Tuple<string, bool>(x, false)).ToArray();

            this.InitialCacheCapabilities = new BlobCacheCapabilities
            {
                CanFetchMetadata = true,
                CanFetchData = true,
                CanConditionalFetch = false,
                CanPut = true,
                CanConditionalPut = false,
                CanDelete = false,
                CanSearchByTag = false,
                CanPurgeByTag = false,
                CanReceiveEvents = false,
                SupportsHealthCheck = false,
                SubscribesToRecentRequest = false,
                SubscribesToExternalHits = true,
                SubscribesToFreshResults = true,
                RequiresInlineExecution = false,
                FixedSize = false
            };
        }

    

        public string UniqueName => config.CacheName;

        internal BlobServiceClient GetClientFor(BlobGroup group)
        {
            return serviceClients[group];
        }
        internal BlobGroupConfiguration GetConfigFor(BlobGroup group)
        {
            if (config.BlobGroupConfigurations.TryGetValue(group, out var groupConfig))
            {
                return groupConfig;
            }
            throw new Exception($"No configuration for blob group {group} in cache {UniqueName}");
        }

        internal string TransformKey(string key)
        {
            switch (config.KeyTransform)
            {
                case KeyTransform.Identity:
                    return key;
                default:
                    throw new Exception($"Unknown key transform {config.KeyTransform}");
            }
        }

        internal string GetKeyFor(BlobGroup group, string key)
        {
            var groupConfig = GetConfigFor(group);
            return groupConfig.Location.BlobPrefix + TransformKey(key);
        }


        
        public async Task<CodeResult> CachePut(ICacheEventDetails e, CancellationToken cancellationToken = default)
        {
            if (e.Result == null || e.Result.IsError) return CodeResult.Err(HttpStatus.BadRequest.WithMessage("CachePut cannot be called with an invalid result"));
            var group = e.BlobCategory;
            var key = e.OriginalRequest.CacheKeyHashString;
            var groupConfig = GetConfigFor(group);
            var azureKey = GetKeyFor(group, key);
            // TODO: validate key eventually
            var container = GetClientFor(group).GetBlobContainerClient(groupConfig.Location.ContainerName);
            var blob = container.GetBlobClient(azureKey);
            if (!GetContainerExistsMaybe(groupConfig.Location.ContainerName) && !groupConfig.CreateContainerIfMissing)
            {
                try
                {
                    await container.CreateIfNotExistsAsync();
                    SetContainerExists(groupConfig.Location.ContainerName, true);
                }
                catch (Azure.RequestFailedException ex)
                {
                    LogIfSerious(ex, groupConfig.Location.ContainerName, key);
                    return CodeResult.Err(new HttpStatus(ex.Status).WithAppend(ex.Message));
                }
            }
            try
            {
                using var consumable = await e.Result.Unwrap().GetConsumablePromise().IntoConsumableBlob();
                using var data = consumable.BorrowStream(DisposalPromise.CallerDisposesStreamThenBlob);
                await blob.UploadAsync(data, cancellationToken);
                return CodeResult.Ok();
            }
            catch (Azure.RequestFailedException ex)
            {

                LogIfSerious(ex, groupConfig.Location.ContainerName, key);
                return CodeResult.Err(new HttpStatus(ex.Status).WithAppend(ex.Message));
            }
        }


        public void Initialize(BlobCacheSupportData supportData)
        {
            
        }

        public async Task<IResult<IBlobWrapper, IBlobCacheFetchFailure>> CacheFetch(IBlobCacheRequest request, CancellationToken cancellationToken = default)
        {
            var group = request.BlobCategory;
            var key = request.CacheKeyHashString;
            var groupConfig = GetConfigFor(group);
            var azureKey = GetKeyFor(group, key);
            var container = GetClientFor(group).GetBlobContainerClient(groupConfig.Location.ContainerName);
            var blob = container.GetBlobClient(azureKey);
            var storage = new AzureBlobStorageReference(container.Uri.AbsoluteUri, azureKey);
            try
            {
                var response = await blob.DownloadStreamingAsync(new BlobDownloadOptions(), cancellationToken);
                SetContainerExists(groupConfig.Location.ContainerName, true);
                return BlobCacheFetchFailure.OkResult(new BlobWrapper(null,AzureBlobHelper.CreateConsumableBlob(storage, response)));

            }
            catch (Azure.RequestFailedException ex)
            {
                //For cache misses, just return a null blob. 
                if (ex.Status == 404)
                {
                    return BlobCacheFetchFailure.MissResult(this, this);
                }
                LogIfSerious(ex, groupConfig.Location.ContainerName, key);
                return BlobCacheFetchFailure.ErrorResult(new HttpStatus(ex.Status).WithAppend(ex.Message), this, this);
            }
        }
    
        
        public Task<CodeResult> CacheDelete(IBlobStorageReference reference, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeResult.Err(HttpStatus.NotImplemented));
           
        }
        internal void LogIfSerious(Azure.RequestFailedException ex, string containerName, string key)
        {
            // Implement similar logging as in the S3 version, adjusted for Azure exceptions and error codes.
        }

  

        public Task<CodeResult<IAsyncEnumerable<IBlobStorageReference>>> CacheSearchByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeResult<IAsyncEnumerable<IBlobStorageReference>>.Err(HttpStatus.NotImplemented));
        }

        public Task<CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>> CachePurgeByTag(SearchableBlobTag tag, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeResult<IAsyncEnumerable<CodeResult<IBlobStorageReference>>>.Err(HttpStatus.NotImplemented));
        }
        
        public Task<CodeResult> OnCacheEvent(ICacheEventDetails e, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CodeResult.Err(HttpStatus.NotImplemented));
        }

        public BlobCacheCapabilities InitialCacheCapabilities { get; }
        public ValueTask<IBlobCacheHealthDetails> CacheHealthCheck(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
