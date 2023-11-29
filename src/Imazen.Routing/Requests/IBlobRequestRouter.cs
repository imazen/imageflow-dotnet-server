using Imazen.Abstractions.Resulting;
using Imazen.Routing.Promises;

namespace Imazen.Routing.Requests;

public interface IBlobRequestRouter
{
    ValueTask<CodeResult<ICacheableBlobPromise>?> RouteToPromiseAsync(MutableRequest request, CancellationToken cancellationToken = default);
}