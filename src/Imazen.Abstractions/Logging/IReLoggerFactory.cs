using Microsoft.Extensions.Logging;

namespace Imazen.Abstractions.Logging
{
    /// <summary>
    /// Like ILoggerFactory, but provides IReLogger instances,
    /// which can retain a set of redacted issues and provide them to the app for a diagnostics summary
    /// </summary>
    public interface IReLoggerFactory : ILoggerFactory
    {
        IReLogger CreateReLogger(string categoryName);

    }
}