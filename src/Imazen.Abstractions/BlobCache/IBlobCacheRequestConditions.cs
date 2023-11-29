namespace Imazen.Abstractions.BlobCache;

public interface IBlobCacheRequestConditions
{
    string? IfMatch { get; }
    IReadOnlyList<string>? IfNoneMatch { get; }
}

public record BlobCacheRequestConditions(string? IfMatch, IReadOnlyList<string>? IfNoneMatch) : IBlobCacheRequestConditions
{
    public static BlobCacheRequestConditions None => new(null, null);
    public static BlobCacheRequestConditions ConditionIfMatch(string? ifMatch) => new(ifMatch, null);
    public static BlobCacheRequestConditions ConditionIfNoneMatch(IReadOnlyList<string>? ifNoneMatch) => new(null, ifNoneMatch);
   
}