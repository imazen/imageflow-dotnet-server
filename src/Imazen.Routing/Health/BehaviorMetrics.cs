using System.Text;

namespace Imazen.Routing.Health;

internal enum MetricBasis: byte
{
    ProblemReports,
    HealthChecks
}

internal struct BehaviorMetrics(MetricBasis basis, BehaviorTask task)
{
    public MetricBasis Basis { get; init; } = basis;
    public BehaviorTask Task { get; init; } = task;

    
    public TimeSpan Uptime { get; private set; }
    public TimeSpan Downtime { get; private set; }
    public DateTimeOffset LastReport { get; private set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastSuccessReport { get; private set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastFailureReport { get; private set; } = DateTimeOffset.MinValue;
    
    public int TotalSuccessReports { get; private set; } = 0;
    public int TotalFailureReports { get; private set; } = 0;
    public int ConsecutiveSuccessReports { get; private set; } = 0;
    public int ConsecutiveFailureReports { get; private set; } = 0;
    
    // track TTFB, TTLB, and byte count?
    // If the ArraySegmentBlobWrapper duration is > 0 < 5ms, it was probably 
    // buffered prior, and we will assume that only TTLB is relevant.
    

    public void ReportBehavior(bool ok, BehaviorTask task, TimeSpan taskDuration)
    {
        LastReport = DateTimeOffset.UtcNow;
        if (ok)
        {
            if (LastSuccessReport > LastFailureReport)
            {
                Uptime += LastReport - LastSuccessReport;
            } // Don't add transition time.
            
            LastSuccessReport = LastReport;
            ConsecutiveSuccessReports++;
            ConsecutiveFailureReports = 0;
            TotalSuccessReports++;
        }
        else
        {
            if (LastFailureReport > LastSuccessReport)
            {
                Downtime += LastReport - LastFailureReport;
            } // Don't add transition time.
            LastFailureReport = LastReport;
            ConsecutiveFailureReports++;
            ConsecutiveSuccessReports = 0;
            TotalFailureReports++;
        }
    }

    /// <summary>
    /// If it's had ten failures in a row, it's probably down.
    /// </summary>
    /// <returns></returns>
    public bool SeemsDown()
    {
        return ConsecutiveFailureReports > 10;
    }

    public void WriteSummaryTo(StringBuilder sb)
    {
        if (ConsecutiveFailureReports > 0)
        {
            sb.Append("! Failed last ");
            sb.Append(ConsecutiveFailureReports);
        }
        else if (ConsecutiveSuccessReports > 0)
        {
            sb.Append("OK last ");
            sb.Append(ConsecutiveSuccessReports);
        }
        else
        {
            sb.Append("? No data");
            return;
        }

        sb.Append(" times");
        if (Uptime > TimeSpan.Zero)
        {
            sb.Append(", uptime=");
            sb.Append(Uptime.ToString(@"hh\:mm\:ss"));
        }
        if (Downtime > TimeSpan.Zero)
        {
            sb.Append(", downtime=");
            sb.Append(Downtime.ToString(@"hh\:mm\:ss"));
        }
        sb.Append(", total of ");
        sb.Append(TotalFailureReports);
        sb.Append(" errors to ");
        sb.Append(TotalSuccessReports);
        sb.Append(" successes overall)");
        var now = DateTimeOffset.UtcNow;
        if (TotalSuccessReports > 0)
        {
            sb.Append(", last success ");
            sb.Append((now - LastSuccessReport).ToString(@"hh\:mm\:ss"));
            sb.Append("s ago");
        }
        if (TotalFailureReports > 0)
        {
            sb.Append(", last failure ");
            sb.Append((now - LastFailureReport).ToString(@"hh\:mm\:ss"));
            sb.Append("s ago");
        }
        
    }
}