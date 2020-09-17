using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Imageflow.Server.Storage.AzureBlob;
using Imazen.Common.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Imageflow.Server.Example
{
    
    public static class CustomBlobServiceExtensions
    {
        public static IServiceCollection AddImageflowCustomBlobService(this IServiceCollection services,
            CustomBlobServiceOptions options)
        {
            services.AddSingleton<IBlobProvider>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<CustomBlobService>>();
                return new CustomBlobService(options, logger);
            });

            return services;
        }
    }

    public class CustomBlobServiceOptions
    {

        public BlobClientOptions BlobClientOptions { get; set; } = new BlobClientOptions();

        /// <summary>
        /// Can block container/key pairs by returning null
        /// </summary>
        public Func<string, string, Tuple<string,string>> ContainerKeyFilterFunction { get; set; } 
            = Tuple.Create;
        public string ConnectionString { get; set; }
        public bool IgnorePrefixCase { get; set; } = true;
        private string prefix = "/blob/";
        /// <summary>
        /// Ensures the prefix begins and ends with a slash
        /// </summary>
        /// <exception cref="ArgumentException">If prefix is / </exception>
        public string Prefix
        {
            get => prefix;
            set
            {
                var p = value.TrimStart('/').TrimEnd('/');
                if (p.Length == 0)
                {
                    throw new ArgumentException("Prefix cannot be /", nameof(p));
                }
                prefix = '/' + p + '/';
            }
        }
    }
    
    
    public class CustomBlobService : IBlobProvider
    {
        private readonly BlobServiceClient client;

        private CustomBlobServiceOptions options;
        public CustomBlobService(CustomBlobServiceOptions options, ILogger<CustomBlobService> logger)
        {
            this.options = options;
            client = new BlobServiceClient(options.ConnectionString, options.BlobClientOptions);
        }

        public IEnumerable<string> GetPrefixes()
        {
            return Enumerable.Repeat(options.Prefix, 1);
        }

        public bool SupportsPath(string virtualPath)
            => virtualPath.StartsWith(options.Prefix,
                options.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        

        public async Task<IBlobData> Fetch(string virtualPath)
        {
            if (!SupportsPath(virtualPath))
            {
                return null;
            }
            var path = virtualPath.Substring(options.Prefix.Length).TrimStart('/');
            var indexOfSlash = path.IndexOf('/');
            if (indexOfSlash < 1) return null;

            var container = path.Substring(0, indexOfSlash);
            var blobKey = path.Substring(indexOfSlash + 1);

            var filtered = options.ContainerKeyFilterFunction(container, blobKey);
            
            if (filtered == null)
            {
                return null;
            }
            
            container = filtered.Item1;
            blobKey = filtered.Item2;
            
            try
            {
                var blobClient = client.GetBlobContainerClient(container).GetBlobClient(blobKey);

                var s = await blobClient.DownloadAsync();
                return new CustomAzureBlob(s);

            }
            catch (RequestFailedException e)
            {
                if (e.Status == 404)
                {
                    throw new BlobMissingException($"Azure blob \"{blobKey}\" not found.", e);
                }

                throw;

            }
        }
    }
    internal class CustomAzureBlob :IBlobData, IDisposable
    {
        private readonly Response<BlobDownloadInfo> response;

        internal CustomAzureBlob(Response<BlobDownloadInfo> r)
        {
            response = r;
        }

        public bool? Exists => true;
        public DateTime? LastModifiedDateUtc => response.Value.Details.LastModified.UtcDateTime;
        public Stream OpenRead()
        {
            return response.Value.Content;
        }

        public void Dispose()
        {
            response?.Value?.Dispose();
        }
    }
}