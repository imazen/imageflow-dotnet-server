using Imazen.Routing.HttpAbstractions;

namespace Imazen.Routing.Serving;

public interface IHasDiagnosticPageSection
{
    string? GetDiagnosticsPageSection(DiagnosticsPageArea section);
}

public enum DiagnosticsPageArea
{
    Start,
    End
}

public interface IImageServer<in TRequest, in TResponse, in TContext>: IHasDiagnosticPageSection where TRequest : IHttpRequestStreamAdapter where TResponse : IHttpResponseStreamAdapter
{
    bool MightHandleRequest<TQ>(string? path, TQ query, TContext context) where TQ : IReadOnlyQueryWrapper;
    ValueTask<bool> TryHandleRequestAsync(TRequest request, TResponse response, TContext context, CancellationToken cancellationToken = default);
}