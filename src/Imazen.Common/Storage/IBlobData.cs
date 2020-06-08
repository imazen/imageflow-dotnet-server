using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Imazen.Common.Storage
{
    public interface IBlobData
    {
        bool? Exists { get; }
        DateTime? LastModifiedDateUtc { get; }

        Stream OpenReadAsync();
    }
}
