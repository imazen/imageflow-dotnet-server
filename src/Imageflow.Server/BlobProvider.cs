using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Imageflow.Fluent;
using Imazen.Common.Storage;

namespace Imageflow.Server
{
    internal class BlobProvider
    {
        private readonly List<IBlobProvider> blobProviders = new List<IBlobProvider>();
        private readonly List<string> blobPrefixes = new List<string>();
        private readonly string webRootPath;
        public BlobProvider(IEnumerable<IBlobProvider> blobProviders, string webRootPath)
        {
            this.webRootPath = webRootPath;
            foreach (var provider in blobProviders)
            {
                this.blobProviders.Add(provider);
                foreach (var prefix in provider.GetPrefixes())
                {
                    var conflictingPrefix =
                        blobPrefixes.FirstOrDefault(p => prefix.StartsWith(p) || p.StartsWith(prefix));
                    if (conflictingPrefix != null)
                    {
                        throw new InvalidOperationException($"Blob Provider failure: Prefix {{prefix}} conflicts with prefix {conflictingPrefix}");
                    }
                    blobPrefixes.Add(prefix);
                }
            }
        }


        internal BlobProviderResult? GetBlobResult(string virtualPath)
        {
            if (blobPrefixes.Any(p => virtualPath.StartsWith(p, StringComparison.Ordinal)))
            {
                foreach (var provider in blobProviders)
                {
                    if (provider.SupportsPath(virtualPath))
                    {
                        return new BlobProviderResult()
                        {
                            IsFile = false,
                            GetBlob = () => provider.Fetch(virtualPath)
                        };
                    }
                }
            }
            return null;
        }

        internal BlobProviderResult? GetFileResult(string virtualPath)
        {

            var imagePath = Path.Combine(
                webRootPath,
                virtualPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(imagePath);
            if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
            {
                return null;

            }

            return new BlobProviderResult()
            {
                IsFile = true,
                GetBlob = () => Task.FromResult(new BlobProviderFile()
                {
                    Path = imagePath,
                    Exists = true,
                    LastModifiedDateUtc = lastWriteTimeUtc
                } as IBlobData)
            };
        }

    }



}