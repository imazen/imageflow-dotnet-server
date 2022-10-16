# Roadmap

# Imageflow.Server

Improve static file proxy support. 

Unify providers and prefixes. 

Create configuration file system. Maybe allow C# scripting?



# Imageflow.Server.Managable

Providing better functionality will require storage of metadata, of object detection infomation, etc.

We need generic table and generic blob storage for Imageflow.Server.Managed

1. We want to facilitate uploads and later viewing/editing.
2. We want to enable face and object detection for smart cropping. This is slow and needs to be cached.
3. We want to more heavily compress frequently requested files to save bandwith.
4. We want to learn patterns of usage of command strings so they can be pre-generated after upload.
5. We want to enable background encoding for stuff like avif. 
6. We want instant access to image dimensions to prevent web page repaints. This would have to be cached in-memory and only for C#. Would require an API.
7. We want to cache remote images in blob storage.
8. We want to be able to cache processed images in blob storage.
9. We want to be able to cache intermediate lossless images in blob storage (when connection speeds are fast enough)








First, establish enough code to store metadata and enable object detection caching.

We need an authoritative database to track where files are located and not let them be orphaned.
Or... we learn to search our data sources and index them?



Require writable blob storage of some kind. Allow optional redis/db for cached access to metadata.

Write-only logs can work if they are condensed periodically. Contents can be hashed and referenced. File names could be YYYY-MM-DD-[hash]


Either reuse a generic data store adapter or a blob-storage based one.

For blob storage, do write-only