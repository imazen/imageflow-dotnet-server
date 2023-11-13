using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Imazen.Common.Storage.Caching
{
    public class CacheBlobFetchResult : ICacheBlobFetchResult
    {
        public CacheBlobFetchResult(ICacheBlobData data, bool dataExists, int statusCode, string statusMessage)
        {
            Data = data;
            DataExists = dataExists;
            StatusCode = statusCode;
            StatusMessage = statusMessage;
        }

        public ICacheBlobData Data { get; private set; }
        public bool DataExists { get; private set; }

        public int StatusCode { get; private set; }
        public string StatusMessage { get; private set; }
        public void Dispose()
        {
            if (Data != null)
            {
                Data.Dispose();
                Data = null;
            }
        }





 
    }
}
