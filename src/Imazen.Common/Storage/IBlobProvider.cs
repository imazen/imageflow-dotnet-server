using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Imazen.Common.Storage
{
    public interface IBlobProvider
    {


        bool Belongs(string virtualPath);

        /// <summary>
        /// Should perform an immediate (uncached) query of blob metadata (such as existence and modified date information)
        /// </summary>
        /// <param namen="virtualPath"></param>
        /// <param name="queryString"></param>
        /// <returns></returns>
        Task<IBlobMetadata> FetchMetadataAsync(string virtualPath, Dictionary<string,string> queryString);
        Task<Stream> OpenAsync(string virtualPath, Dictionary<string,string> queryString);


    }
}
