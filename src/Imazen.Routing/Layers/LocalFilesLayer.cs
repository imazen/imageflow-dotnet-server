using System.Buffers;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Resulting;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Promises;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;
public interface IPathMapping : IStringAndComparison
{
    string VirtualPath { get; }
    string PhysicalPath { get; }
    bool IgnorePrefixCase { get; }
    
    // TODO: when one of these is mapping / to /wwwroot, we need some preconditions to 
    // prevent all file requests from being routed to it.
    
    // But we also don't want to be serving .config files if it's NOT root. 
}

public record PathMapping(string VirtualPath, string PhysicalPath, bool IgnorePrefixCase = false) : IPathMapping
{
    public string StringToCompare => VirtualPath;
    public StringComparison StringComparison => IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
public class LocalFilesLayer : IRoutingLayer
{
    public LocalFilesLayer(List<IPathMapping> pathMappings)
    {
        this.PathMappings = pathMappings; // TODO, should we clone it?
        // We want to compare the longest prefixes first so they match in case of collisions
        this.PathMappings.Sort((a, b) => b.VirtualPath.Length.CompareTo(a.VirtualPath.Length));
        
        // ReSharper disable twice ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (this.PathMappings.Any(m => m.PhysicalPath == null || m.VirtualPath == null))
        {
            throw new ArgumentException("Path mappings must have both a virtual and physical path");
        }
        FastPreconditions = Conditions.HasPathPrefix(PathMappings);
        
    }
    protected readonly List<IPathMapping> PathMappings;
    
    public string Name => "LocalFiles";
    public IFastCond? FastPreconditions { get; }


    public ValueTask<CodeResult<IRoutingEndpoint>?> ApplyRouting(MutableRequest request, CancellationToken cancellationToken = default)
    {
        var path = request.Path;
        foreach (var mapping in PathMappings)
        {
            if (path.StartsWith(mapping.VirtualPath, mapping.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                
                bool isRoot = mapping.VirtualPath == "/" || mapping.VirtualPath == "";
                if (isRoot && !Conditions.HasSupportedImageExtension.Matches(request))
                {
                    // If it's a / mapping, we only take image extensions.
                    // This breaks extensionless paths configuration if the root is mapped to a directory or blob provider, however.
                    
                    return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
                }
                    
                var relativePath = path
                    .Substring(mapping.VirtualPath.Length)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar);
                
                var physicalDir = Path.GetFullPath(mapping.PhysicalPath.TrimEnd(Path.DirectorySeparatorChar));

                var physicalPath = Path.GetFullPath(Path.Combine(
                    physicalDir,
                    relativePath));
                if (!physicalPath.StartsWith(physicalDir, StringComparison.Ordinal))
                {
                    return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null); //We stopped a directory traversal attack (most likely)
                }
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc(physicalPath);
                if (lastWriteTimeUtc.Year == 1601) // file doesn't exist, pass to next middleware
                {
                    return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
                }
                else
                {
                    
                    var latencyZone = new LatencyTrackingZone(mapping.PhysicalPath, 10);
                    return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(CodeResult<IRoutingEndpoint>.Ok(new PromiseWrappingEndpoint(
                        new FilePromise(request.ToSnapshot(true), physicalPath, latencyZone, lastWriteTimeUtc))));
                }
            }
        }

        return Tasks.ValueResult<CodeResult<IRoutingEndpoint>?>(null);
    }
    
    /// ToString includes all data in the layer, for full diagnostic transparency, and lists the preconditions and data count
    public override string ToString()
    {
        // Include all mappings and preconditions, if any
        var mappingPairs = PathMappings.Select(m => $"  {m.VirtualPath}{(m.IgnorePrefixCase ? @"(i)" : @"")} => {m.PhysicalPath}").ToArray();
        return $"Routing Layer {Name}: {PathMappings.Count} mappings, Preconditions: {FastPreconditions}\n{string.Join("\n", mappingPairs)}";
    }

    
    internal record FilePromise(IRequestSnapshot FinalRequest, string PhysicalPath,LatencyTrackingZone LatencyZone, DateTime LastWriteTimeUtc): CacheableBlobPromiseBase(FinalRequest, LatencyZone)
    {
        
        public override void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
        {
            base.WriteCacheKeyBasisPairsToRecursive(writer);
            // We don't write the physical path since it might change based on deployment. The relative path and date are sufficient.
            writer.WriteLong(LastWriteTimeUtc.ToBinary());
        }
        public override ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline,
            CancellationToken cancellationToken = default)
        {
            return Tasks.ValueResult(CodeResult<IBlobWrapper>.Ok(new BlobWrapper(this.LatencyZone, PhysicalFileBlobHelper.CreateConsumableBlob(PhysicalPath, LastWriteTimeUtc))));
        }
    }

    internal abstract record CacheableBlobPromiseBase(IRequestSnapshot FinalRequest, LatencyTrackingZone? LatencyZone) : ICacheableBlobPromise
    {
        public bool IsCacheSupporting => true;

        public async ValueTask<IAdaptableHttpResponse> CreateResponseAsync(IRequestSnapshot request, IBlobRequestRouter router, IBlobPromisePipeline pipeline,
            CancellationToken cancellationToken = default)
        {
            return new BlobResponse(await TryGetBlobAsync(request, router, pipeline, cancellationToken)).AsResponse();
        }
        

        public bool HasDependencies => false;
        public bool ReadyToWriteCacheKeyBasisData => true;
        public ValueTask<CodeResult> RouteDependenciesAsync(IBlobRequestRouter router, CancellationToken cancellationToken = default)
        {
            return Tasks.ValueResult(CodeResult.Ok());
        }

        public virtual void WriteCacheKeyBasisPairsToRecursive(IBufferWriter<byte> writer)
        {
            FinalRequest.WriteCacheKeyBasisPairsTo(writer);
        }

        private byte[]? cacheKey32Bytes = null;
        public byte[] GetCacheKey32Bytes()
        {
            return cacheKey32Bytes ??= this.GetCacheKey32BytesUncached();
        }

        public virtual bool SupportsPreSignedUrls => false;

        public abstract ValueTask<CodeResult<IBlobWrapper>> TryGetBlobAsync(IRequestSnapshot request,
            IBlobRequestRouter router, IBlobPromisePipeline pipeline,
            CancellationToken cancellationToken = default);
    }

}