namespace Imazen.Common.Storage.Caching
{
    public class CacheBlobPutResult : ICacheBlobPutResult{
        public CacheBlobPutResult(int statusCode, string statusMessage, bool success)
        {
            StatusCode = statusCode;
            StatusMessage = statusMessage;
            Success = success;
        }
        public int StatusCode {get; private set;}
        public string StatusMessage {get;private set;}
        public bool Success {get;private set;}
        
    }
}
