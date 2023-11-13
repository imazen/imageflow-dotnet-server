using System;
namespace Imageflow.Server.Storage.S3.Caching
{
    /// <summary>
    /// Immutable struct that specifies the blob group prefix, the number of days before expiring the group of blobs, and the number of days before expiry to renew the blob if still in use. 
    /// There is a 2 day gap, so blobs must be renewed at earliest 2 days after creation.
    /// </summary>
    internal readonly struct BlobGroupLifecycle
    {
     
        internal BlobGroupLifecycle(int? daysBeforeExpiry, int? daysMinRenewalInterval)
        {
            DaysBeforeExpiry = daysBeforeExpiry;
            DaysMinRenewalInterval = daysMinRenewalInterval;
            //If both daysBeforeExpiry and daysMinRenewalInterval are present, then daysBeforeExpiry should be greater
            if (daysBeforeExpiry.HasValue && daysMinRenewalInterval.HasValue && daysBeforeExpiry <= daysMinRenewalInterval)
            {
                throw new ArgumentException("daysBeforeExpiry must be greater than daysMinRenewalInterval");
            }
        }


        /// <summary>
        /// Blobs will never expire or be renewed.
        /// </summary>
        public static BlobGroupLifecycle NonExpiring => new BlobGroupLifecycle( null, null);

        /// <summary>
        /// The maximum lifespan of a blob is daysUnused = daysMinRenewalInterval
        /// </summary>
        /// <param name="daysUnused">If the blob is not used for the specified number of days, it will be deleted. </param>
        /// <param name="daysMinRenewalInterval">DaysMinRenewalInterval is the number of days after creation/renewal that a renewal will not be attempted.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static BlobGroupLifecycle SlidingExpiryAndRenewal(int daysUnused, int daysMinRenewalInterval){
            if (daysUnused < 1)
            {
                throw new ArgumentException("daysUnused must be greater than 0");
            }
            if (daysMinRenewalInterval < 1)
            {
                throw new ArgumentException("daysMinRenewalInterval must be greater than 0");
            }

            return new BlobGroupLifecycle( daysUnused + daysMinRenewalInterval, daysMinRenewalInterval);
        }
    
        /// <summary>
        /// Even if used continuously, the blob will be deleted the specified number of days after being first created.
        /// </summary>
        /// <param name="daysToExpire">Must be greater than 0</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static BlobGroupLifecycle ExpireAfterCreation(int daysToExpire)
        {
            if (daysToExpire < 1)
            {
                throw new ArgumentException("daysToExpire must be greater than 0");
            }
            return new BlobGroupLifecycle(daysToExpire, null);
        }

        /// <summary>
        /// Blobs expire this many days after creation or last renewal
        /// </summary>
        public int? DaysBeforeExpiry { get; }
        /// <summary>
        /// If used within this many days of creation/renewal, a renewal/copy will not be attempted. Helps reduce S3 costs.
        /// </summary>
        public int? DaysMinRenewalInterval { get; }
    }

}