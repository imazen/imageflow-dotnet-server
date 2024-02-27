using Imazen.Abstractions.BlobCache;
using Imazen.Abstractions.Blobs;
using Imazen.Abstractions.Logging;
using Imazen.Common.Concurrency.BoundedTaskCollection;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Promises.Pipelines;

public record CacheEngineOptions
{
    public CacheEngineOptions(){}
    public CacheEngineOptions(List<IBlobCache> simultaneousFetchAndPut, BoundedTaskCollection<BlobTaskItem>? taskPool, IReLogger logger)
    {
        SeriesOfCacheGroups = new List<List<IBlobCache>>(){simultaneousFetchAndPut};
        SaveToCaches = simultaneousFetchAndPut;
        UploadQueue = taskPool;
        Logger = logger;
        
    }
    // Each cache group is a list of caches that can be queried in parallel
    public required List<List<IBlobCache>> SeriesOfCacheGroups { get; init; }
    
    public required List<IBlobCache> SaveToCaches { get; init; }
    
    [Obsolete("Use the parameterized one local to the request")]
    public IBlobRequestRouter? RequestRouter { get; init; }
    
    public required IReusableBlobFactory BlobFactory { get; init; }
    
    public required BoundedTaskCollection<BlobTaskItem>? UploadQueue { get; init; }
    
    public required IReLogger Logger { get; init; }
    
    /// <summary>
    /// If true, provides the opportunity for an IBlobCache to eliminate duplicate requests and prevent thundering herd.
    /// </summary>
    public bool LockByUniqueRequest { get; init; }
    
    /// <summary>
    /// How long to wait for fetching and generation of the same request by another thread.
    /// </summary>
    public int LockTimeoutMs { get; init; } = 2000;
    
    
}