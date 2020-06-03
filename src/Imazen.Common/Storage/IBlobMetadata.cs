using System;
using System.Collections.Generic;
using System.Text;

namespace Imazen.Common.Storage
{
    public interface IBlobMetadata
    {
        bool? Exists { get; set; }
        DateTime? LastModifiedDateUtc { get; set; }
    }
    public class BlobMetadata : IBlobMetadata
    {
        public bool? Exists { get; set; }
        public DateTime? LastModifiedDateUtc { get; set; }
    }

}
