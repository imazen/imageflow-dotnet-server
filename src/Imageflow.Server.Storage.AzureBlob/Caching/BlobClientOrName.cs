using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Azure;

namespace Imageflow.Server.Storage.AzureBlob.Caching
{
    internal struct BlobClientOrName
    {
        internal readonly BlobServiceClient? Client;
        internal readonly string? Name;

        internal BlobClientOrName(BlobServiceClient client)
        {
            Client = client;
            Name = null;
        }
        internal BlobClientOrName(string name)
        {
            Client = null;
            Name = name;
        }
        
        public bool IsEmpty => Client == null && Name == null;

        public BlobClientOrName Or(string name)
        {
            if (IsEmpty) return new BlobClientOrName(name);
            return this;
        }
        public BlobClientOrName Or(BlobServiceClient client)
        {
            if (IsEmpty) return new BlobClientOrName(client);
            return this;
        }
        
        public BlobServiceClient? Resolve(IAzureClientFactory<BlobServiceClient> clientFactory)
        {
            if (Client != null) return Client;
            if (Name != null) return clientFactory.CreateClient(Name);
            return null;
        }
        
        
            
        public BlobServiceClient Resolve(Func<string?,BlobServiceClient> clientFactory)
        {
            if (Client != null) return Client;
            return clientFactory(Name);
        }
    }
}