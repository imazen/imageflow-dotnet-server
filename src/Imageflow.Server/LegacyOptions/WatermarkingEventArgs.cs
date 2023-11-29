using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Http;

namespace Imageflow.Server
{
    public class WatermarkingEventArgs
    {

        internal WatermarkingEventArgs(HttpContext? context, string virtualPath, Dictionary<string, string> query,
            List<NamedWatermark> watermarks)
        {
            VirtualPath = virtualPath;
            Context = context;
            Query = new ReadOnlyDictionary<string, string>(query);
            AppliedWatermarks = watermarks;
        }

        public string VirtualPath { get; }

        public IReadOnlyDictionary<string, string> Query { get; }

        public List<NamedWatermark> AppliedWatermarks { get; set; }

        public HttpContext? Context { get; }
    }

}