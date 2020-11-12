using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Imageflow.Server
{
    public class UrlEventArgs
    {
        internal UrlEventArgs(HttpContext context, string virtualPath, Dictionary<string, string> query)
        {
            VirtualPath = virtualPath;
            Context = context;
            Query = query;
        }
        
        public string VirtualPath { get; set; }
        
        public Dictionary<string,string> Query { get; set; }
        
        public HttpContext Context { get; }
    }
}