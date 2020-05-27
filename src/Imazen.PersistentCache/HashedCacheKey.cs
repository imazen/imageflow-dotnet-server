using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.PersistentCache
{
    struct HashedCacheKey
    {
        internal uint ReadId { get; set; }

        internal uint Shard { get; set;  }

        internal string BlobKey { get; set; }

        internal byte[] Key1Hash { get; set; }

        internal byte[] Key2Hash { get; set; }

        internal byte[] Key3Hash { get; set; }
    }
}
