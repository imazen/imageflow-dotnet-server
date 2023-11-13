
// Contains BlobGroupLifecycle and BlobGroupLocation
using System;
using Amazon;
namespace Imageflow.Server.Storage.S3.Caching
{
    internal readonly struct BlobGroupConfiguration
    {
        internal readonly BlobGroupLocation Location;
        internal readonly BlobGroupLifecycle Lifecycle;

        internal readonly bool UpdateLifecycleRules;
        internal readonly bool CreateBucketIfMissing;

        internal BlobGroupConfiguration(BlobGroupLocation location, BlobGroupLifecycle lifecycle, bool createBucketIfMissing, bool updateLifecycleRules){
            //location cannot be empty
            if (location.BucketName == null)
            {
                throw new ArgumentNullException(nameof(location));
            }
            Location = location;
            Lifecycle = lifecycle;
            
            this.CreateBucketIfMissing = createBucketIfMissing;
            this.UpdateLifecycleRules = updateLifecycleRules;
        }
    }
}