using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Imazen.Common.Storage;

namespace Imageflow.Server
{
    internal class BlobProvider
    {
        private readonly List<IBlobProvider> blobProviders = new List<IBlobProvider>();
        private readonly List<string> blobPrefixes = new List<string>();
        private readonly List<PathMapping> pathMappings;
        public BlobProvider(IEnumerable<IBlobProvider> blobProviders, List<PathMapping> pathMappings)
        {
            this.pathMappings = pathMappings.ToList();
            this.pathMappings.Sort((a, b) => b.VirtualPath.Length.CompareTo(a.VirtualPath.Length));
            
            foreach (var provider in blobProviders)
            {
                this.blobProviders.Add(provider);
                foreach (var prefix in provider.GetPrefixes())
                {
                    var conflictingPrefix =
                        blobPrefixes.FirstOrDefault(p => 
                            prefix.StartsWith(p, StringComparison.OrdinalIgnoreCase) || 
                            p.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
                    if (conflictingPrefix != null)
                    {
                        throw new InvalidOperationException($"Blob Provider failure: Prefix {{prefix}} conflicts with prefix {conflictingPrefix}");
                    }
                    // We don't check for conflicts with PathMappings because / is a path mapping usually, 
                    // and we simply prefer blobs over files if there are overlapping prefixes.
                    blobPrefixes.Add(prefix);
                }
            }
        }

        internal BlobProviderResult? GetResult(string virtualPath)
        {
            return GetBlobResult(virtualPath) ?? GetFileResult(virtualPath);
        }

        private BlobProviderResult? GetBlobResult(string virtualPath)
        {
            if (blobPrefixes.Any(p => virtualPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
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

        private BlobProviderResult? GetFileResult(string virtualPath)
        {
            var mapping = pathMappings.FirstOrDefault(
                m => virtualPath.StartsWith(m.VirtualPath, 
                    m.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (mapping.PhysicalPath == null || mapping.VirtualPath == null) return null;

            var relativePath = virtualPath
                .Substring(mapping.VirtualPath.Length)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var physicalDir = Path.GetFullPath(mapping.PhysicalPath.TrimEnd(Path.DirectorySeparatorChar));
            
            var physicalPath = Path.GetFullPath(Path.Combine(
                physicalDir,
                relativePath));
            if (!physicalPath.StartsWith(physicalDir, StringComparison.Ordinal))
            {
                return null; //We stopped a directory traversal attack (most likely)
            }
            
            
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(physicalPath);
            if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
            {
                return null;

            }

            return new BlobProviderResult()
            {
                IsFile = true,
                GetBlob = () => Task.FromResult(new BlobProviderFile()
                {
                    Path = physicalPath,
                    Exists = true,
                    LastModifiedDateUtc = lastWriteTimeUtc
                } as IBlobData)
            };
        }

    }



}