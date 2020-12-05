// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the GNU Affero General Public License, Version 3.0.
// Commercial licenses available at http://imageresizing.net/

using Imazen.Common.Concurrency;
using Imazen.DiskCache.Index;

namespace Imazen.DiskCache
{
    internal delegate void CacheResultHandler(ICleanableCache sender, CacheResult r);
    

    internal interface ICleanableCache
    {
         event CacheResultHandler CacheResultReturned; 
         CacheIndex Index { get; }
         string PhysicalCachePath { get;  }

         AsyncLockProvider Locks { get;}
    }
}
