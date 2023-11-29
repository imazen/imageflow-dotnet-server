using Imazen.Abstractions.Resulting;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Promises;

public interface IBlobPromisePipeline
{
    /// <summary>
    /// This probably needs more detail, such as - are we allowing job wrapping? cache wrapping? etc.
    /// </summary>
    /// <param name="promise"></param>
    /// <param name="promisePipeline"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="router"></param>
    /// <param name="outerRequest"></param>
    /// <returns></returns>
    ValueTask<CodeResult<ICacheableBlobPromise>> GetFinalPromiseAsync(ICacheableBlobPromise promise, IBlobRequestRouter router, IBlobPromisePipeline promisePipeline, IHttpRequestStreamAdapter outerRequest, CancellationToken cancellationToken = default);
}