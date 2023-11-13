using System;

namespace Imazen.Common.Storage.Caching
{
    public interface ICacheBlobPutResult
    {
        // HTTP Code
        int StatusCode { get; }
        // HTTP Status Message
        string StatusMessage { get; }

        bool Success { get; }

        // List the full set of properties that can be returned from a Put operation on Azure and S3
        // https://docs.microsoft.com/en-us/rest/api/storageservices/put-blob
        // https://docs.aws.amazon.com/AmazonS3/latest/API/RESTObjectPUT.html

        // Which result codes exist? List them below in a comment
        // 200 OK
        // 201 Created
        // 202 Accepted
        // 203 Non-Authoritative Information (since HTTP/1.1)
        // 204 No Content
        // 205 Reset Content
        // 206 Partial Content (RFC 7233)
        // 207 Multi-Status (WebDAV; RFC 4918)

    }
}
