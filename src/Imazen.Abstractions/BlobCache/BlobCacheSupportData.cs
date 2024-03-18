namespace Imazen.Abstractions.BlobCache;

public record BlobCacheSupportData(Func<Task> AwaitBeforeShutdown);