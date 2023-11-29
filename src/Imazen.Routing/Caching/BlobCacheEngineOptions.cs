namespace Imazen.Routing.Caching;
internal class BlobCacheEngineOptions{
    public int MaxQueueBytes { get; set; } =  1024 * 1024 * 300; //300mb seems reasonable (was 100mb)
    public int WaitForIdenticalRequestsTimeoutMs { get; set; } = 100000;
    public bool FailRequestsOnEnqueueLockTimeout { get; set; } = true;
}