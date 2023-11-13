// We can handle blob caching remote URLs
// We can disk cache remote URLs and source blobs

// we can disk cache results 
// we can blob cache results

// we can mem cache stuff too


// Some blob caches need to renew entries so they don't expire (s3)
// Blob caches are named
// Disk caches *need* to be named
// Mem caches *need* to be named (different quotas, perhaps)

// For fetching from high-latency caches, we can use ExistenceProbableMap to determine if we should go ahead and start fetching the source image already

