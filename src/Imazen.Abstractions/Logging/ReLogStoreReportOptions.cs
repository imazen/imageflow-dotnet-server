namespace Imazen.Abstractions.Logging
{
    
    public enum ReLogStoreReportType
    {
        QuickSummary,
        FullReport
    }
    public record ReLogStoreReportOptions
    {
        public ReLogStoreReportType ReportType { get; init; } = ReLogStoreReportType.QuickSummary;
    }
}