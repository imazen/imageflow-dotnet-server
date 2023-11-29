namespace Imazen.Common.Storage
{
    [Obsolete("Use Imazen.Abstractions.Blobs.LegacyProviders.IBlobWrapperProvider instead")]
    public interface IBlobProvider
    {
        IEnumerable<string> GetPrefixes();
        
        bool SupportsPath(string virtualPath);
        
        Task<IBlobData> Fetch(string virtualPath);
    }
}
