using System.IO;
using Imazen.Common.Extensibility.StreamCache;

namespace Imageflow.Server.DiskCache;

internal class StreamCacheResult: IStreamCacheResult{
    public Stream Data { get; set; }
    public string ContentType { get; set; }
    public string Status { get; set; }
}