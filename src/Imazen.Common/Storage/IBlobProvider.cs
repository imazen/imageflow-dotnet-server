using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imazen.Common.Storage
{
    public interface IBlobProvider
    {
        IEnumerable<string> GetPrefixes();
        
        bool SupportsPath(string virtualPath);
        
        Task<IBlobData> Fetch(string virtualPath);
    }
}
