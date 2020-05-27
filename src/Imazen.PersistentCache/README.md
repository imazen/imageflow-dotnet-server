# PersistentCache design

* Sharded to allow scaling past memory limits and distribution across multiple drives
* Can be backed by disk, blob storage, or any backend
* Strictly limits size of cached blobs (but not metadata)
* 3-part cache keys allow evicting older versions of files, or all instances of a file
* LFU cache so frequently used items are less likely to be evicted
* Psuedorandom candiate selection for eviction evaluation, with favor towards older entries
* Re-creation cost is factored in when selecting equally frequently used items for deletion


Shard into multiple folders

Each shard has
/blobs
/write/logs/date-uuid
/reads/index

3 part key. Parts are hashed

Parts: basepath variant(moddate) version

Writelogs are used for eviction
Part1 part2 part3 readid datewritten size cost deleted

Old design: Readlogs are written to a percentage of the time when a read occurs. Readlogs are periodically consildated into a binary search list with a 32-bit ID and a 16 bit last used date and a 16 bit frequency bits - see redis design morris counter 

https://redis.io/topics/lru-cache


Eviction is done by scanning the write logs and filtering by the read logs. Low cost and high size items are prioritized. 

Eviction can be done by base path and version

When write logs have lots of deleted entries they are rewritten.

S3, azure, and disk backends allowed. 

