namespace Imazen.Common.Logging
{
    public interface ILoggerProvider
    {
        ILogger Logger { get; }
    }
}