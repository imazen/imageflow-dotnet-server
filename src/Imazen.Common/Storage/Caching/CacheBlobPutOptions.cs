namespace Imazen.Common.Storage.Caching
{
    public class CacheBlobPutOptions : ICacheBlobPutOptions 
    {
        public CacheBlobPutOptions()
        {
        }

        public static CacheBlobPutOptions Default { get; } = new CacheBlobPutOptions();
    }
}
