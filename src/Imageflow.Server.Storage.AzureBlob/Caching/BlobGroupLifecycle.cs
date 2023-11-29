using System;
namespace Imageflow.Server.Storage.AzureBlob.Caching
{

    internal readonly struct BlobGroupLifecycle
    {

        internal bool? LastAccessTimeTracking { get; }
        internal int? DaysAfterLastAccessTimeGreaterThan { get; }
     
        internal BlobGroupLifecycle(bool? lastAccessTimeTracking, int? daysAfterLastAccessTimeGreaterThan)
        {   
            LastAccessTimeTracking = lastAccessTimeTracking;
            DaysAfterLastAccessTimeGreaterThan = daysAfterLastAccessTimeGreaterThan;
            if (DaysAfterLastAccessTimeGreaterThan.HasValue && DaysAfterLastAccessTimeGreaterThan.Value < 2)
            {
                throw new ArgumentException("DaysAfterLastAccessTimeGreaterThan must be greater than 1, if specified");
            }
            if (DaysAfterLastAccessTimeGreaterThan.HasValue && lastAccessTimeTracking != true)
            {
                throw new ArgumentException("DaysAfterLastAccessTimeGreaterThan cannot be specified if LastAccessTimeTracking is not enabled");
            }
        }

        internal static BlobGroupLifecycle NonExpiring => new BlobGroupLifecycle(null, null);
        internal static BlobGroupLifecycle SlidingExpiry(int? daysUnusedBeforeExpiry) => new BlobGroupLifecycle(daysUnusedBeforeExpiry.HasValue, daysUnusedBeforeExpiry);
    }

}