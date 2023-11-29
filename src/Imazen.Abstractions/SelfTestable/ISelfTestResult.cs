namespace Imazen.Abstractions.SelfTestable
{
    internal interface ISelfTestResult 
    {
        DateTimeOffset CompletedAt { get; }
        TimeSpan WallTime { get; }
        bool Passed { get; }
        string ResultSummary { get;  }
        string? ResultDetails { get; }
        string TestTitle { get; }
        string SourceObjectTitle { get; }
    }
}