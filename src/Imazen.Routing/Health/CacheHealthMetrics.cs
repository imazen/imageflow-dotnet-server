using System.Diagnostics;
using System.Text;
using Imazen.Abstractions.BlobCache;

namespace Imazen.Routing.Health;

internal class CacheHealthMetrics
{
    /// <summary>
    /// The last exception thrown by a health check, if any.
    /// </summary>
    public  Exception? LastHealthCheckException { get; private set; }

    /// <summary>
    /// The last report from a health check, whether it was healthy or not.
    /// </summary>
    private IBlobCacheHealthDetails? LastHealthCheckCompleted { get; set; } = null;
    
    /// <summary>
    /// If we haven't had a successful health check in 3+ minutes and it has failed at least 3 times
    /// We are on our own for determining capabilities based on observed behavior
    /// </summary>
    private bool HealthCheckCrashing => ConsecutiveCheckCrashes > 3 && LastCheckCompletedOrFailed < DateTimeOffset.UtcNow - TimeSpan.FromMinutes(3);

    
    /// <summary>
    /// True if the cache advertises support for health checks.
    /// </summary>
    private bool SupportsHealthCheck { get; init; }
    //  collectedMetrics[BehaviorTask.HealthCheck] refers to whether the health check completed or threw an exception, not if it returned a degraded result (it was still a success) 
    private readonly BehaviorMetrics[] collectedMetrics = new BehaviorMetrics[BehaviorTaskHelpers.BehaviorTaskCount];
    // This just tracks reported capabilities over time.
    private readonly BehaviorMetrics[] healthCheckMetrics = new BehaviorMetrics[BehaviorTaskHelpers.BehaviorTaskCount];
    /// <summary>
    /// This tracks whether the health checks have been returning at full health or not (ignoring crashes, those are counted in collectedMetrics)
    /// </summary>
    private BehaviorMetrics atFullHealth = new BehaviorMetrics(MetricBasis.HealthChecks, BehaviorTask.HealthCheck);

    private readonly BlobCacheCapabilities initialAdvertisedCapabilities;


    public CacheHealthMetrics(BlobCacheCapabilities initialAdvertisedCapabilities)
    {
        this.initialAdvertisedCapabilities = initialAdvertisedCapabilities;
        
        SupportsHealthCheck = initialAdvertisedCapabilities.SupportsHealthCheck;
        for (var i = 0; i < collectedMetrics.Length; i++)
        {
            collectedMetrics[i] = new BehaviorMetrics(MetricBasis.ProblemReports, (BehaviorTask) i);
            healthCheckMetrics[i] = new BehaviorMetrics(MetricBasis.HealthChecks, (BehaviorTask) i);
        }
    }

    private DateTimeOffset LastCheckCompletedOrFailed => collectedMetrics[(int) BehaviorTask.HealthCheck].LastReport;
    public int ConsecutiveCheckCrashes => collectedMetrics[(int) BehaviorTask.HealthCheck].ConsecutiveFailureReports;
    public int ConsecutiveCheckResponses => collectedMetrics[(int) BehaviorTask.HealthCheck].ConsecutiveSuccessReports;
    public int ConsecutiveUnhealthyChecks => atFullHealth.ConsecutiveFailureReports;

    private int consecutiveIdenticalHealthCheckCaps;



    private BlobCacheCapabilities? cachedEstimate;
    public BlobCacheCapabilities EstimateCapabilities()
    {
        // If we have a successful health check, we can use the reported capabilities
        if (!HealthCheckCrashing && LastHealthCheckCompleted != null)
            return LastHealthCheckCompleted.CurrentCapabilities;
        
        // If health checks aren't supported, or checks themselves have been failing for a while, we should assume the worst and 
        // only report capabilities that intersect with (a) initial capabilities and (b) capabilities NOT on a failure streak
        // based on behavior reports
        // Here we go off reported behavior
        cachedEstimate ??= initialAdvertisedCapabilities;
        cachedEstimate = Intersect(cachedEstimate, collectedMetrics);
        return cachedEstimate;
    }


    /// <summary>
    /// This returns true when we should fire off another health check.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="UnreachableException"></exception>
    public bool IsStale()
    {
        if (!SupportsHealthCheck) return false; // We simply never support health checks.
        
        var timeSinceLastCheck = DateTimeOffset.UtcNow - LastCheckCompletedOrFailed;
        
        // First-time call (or first time call is pending)
        if (LastHealthCheckCompleted == null && ConsecutiveCheckCrashes == 0) return true;
        
        // Fast exit with an absolute minimum delay
        if (timeSinceLastCheck < TimeSpan.FromMilliseconds(500)) return false;

        return timeSinceLastCheck > GetOrAdjustHealthCheckInterval();
    }
    private TimeSpan GetOrAdjustHealthCheckInterval(bool readOnly = false){
        // See if we can listen to the cache's health check interval
        var suggestedRecheck = ConsecutiveCheckCrashes == 0 ? LastHealthCheckCompleted?.SuggestedRecheckDelay : null;
        if (suggestedRecheck != null && LastHealthCheckCompleted != null) return suggestedRecheck.Value;
        
        // If HealthCheckCrashing=true, we're not using the health check results anyway, we've fallen back to behavior reports.
        
        // We should probably do exponential backoff if we're always getting the same results, whether good or bad
        // ConsecutiveIdenticalHealthCheckCaps > 1
        
        // But if we get a surge in bad behavior reports, we should lower the MAX delay between health checks to 10 seconds
        // Because clearly there's a conflict between the capabilities and the behavior. 
        
        var maxCheckInterval = HealthCheckCrashing ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(20);
        var minCheckInterval = HealthCheckCrashing ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromMinutes(1);
        
        // 10+ consecutive failures with the latest in the interval probably means a difference between reported health and actual behavior.
        var badBehaviorPatience = TimeSpan.FromSeconds(15);
        var badBehavior = collectedMetrics.Any(m => m.SeemsDown() && m.LastFailureReport > DateTimeOffset.UtcNow - badBehaviorPatience);
        if (badBehavior)
        {
            maxCheckInterval = badBehaviorPatience;
            minCheckInterval = TimeSpan.FromSeconds(1);
        }
        if (lastDelay == default) lastDelay = minCheckInterval + GetJitter(); // Not actually reachable except in a race condition.
        
        // Read-only mode is used to determine the next interval without actually changing the state.
        if (readOnly) return lastDelay;
        
        if (lastMinCheckInterval != minCheckInterval)
        {
            // When we initially transition between init / bad behavior / crashing checks / consistent reporting (good or bad)
            // We reset the delay to the minimum and trigger an immediate check.
            // We're not interlocking lastMinCheckInterval because this whole thing is a heuristic anyway.
            lastDelay = minCheckInterval + GetJitter();
            return lastDelay;
        }
        lastMinCheckInterval = minCheckInterval;

        if (consecutiveIdenticalHealthCheckCaps < 3)
        {
            // We don't need to start backing off yet. We can poll at the minimum interval.
            return lastDelay;
        }
        // Ok, now we've gotten 3 identical health check results in a row. We should start backing off.
        const int backOffMultiplier = 2;
        lastDelay = Min(new TimeSpan(lastDelay.Ticks * backOffMultiplier), maxCheckInterval);
        return lastDelay;
    }

    private TimeSpan lastDelay;
    private TimeSpan lastMinCheckInterval;

    /// <summary>
    /// 50-550ms jitter. We only call this during state transitions, it's used once for the base jitter and then we presume
    /// multiplication will simply expand the difference between this client and other clients.
    /// </summary>
    /// <returns></returns>
    private TimeSpan GetJitter()
    {
        // Add some jitter to avoid thundering herd (although this is unlikely unless we have a lot of servers and the cache is not good quality)
#if DOTNET6_OR_GREATER
        var random = Random.Shared;
#else
        var random = new Random();
#endif
        return TimeSpan.FromMilliseconds(50 + random.NextDouble() * 500);
    }
    private TimeSpan Min(TimeSpan a, TimeSpan b)
    {
        return a < b ? a : b;
    }
    
    public void ReportBehavior(bool successful, BehaviorTask task, TimeSpan taskDuration)
    {
        ref var m = ref collectedMetrics[(int) task];
        m.ReportBehavior(successful, task, taskDuration);
    }

    private BlobCacheCapabilities? lastReportedCapabilities;
    private void AnyReport(BlobCacheCapabilities? newCapabilities, TimeSpan taskDuration)
    {
        if (newCapabilities == lastReportedCapabilities) consecutiveIdenticalHealthCheckCaps++;
        else consecutiveIdenticalHealthCheckCaps = 0;
        lastReportedCapabilities = newCapabilities;
        
        ReportBehavior(newCapabilities != null, BehaviorTask.HealthCheck, taskDuration);
        if (newCapabilities != null)
        {
            healthCheckMetrics[(int)BehaviorTask.FetchData]
                .ReportBehavior(newCapabilities.CanFetchData, BehaviorTask.FetchData, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.FetchMetadata].ReportBehavior(newCapabilities.CanFetchMetadata,
                BehaviorTask.FetchMetadata, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.Put].ReportBehavior(newCapabilities.CanPut, BehaviorTask.Put, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.Delete].ReportBehavior(newCapabilities.CanDelete, BehaviorTask.Delete, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.SearchByTag].ReportBehavior(newCapabilities.CanSearchByTag,
                BehaviorTask.SearchByTag, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.PurgeByTag].ReportBehavior(newCapabilities.CanPurgeByTag,
                BehaviorTask.PurgeByTag, TimeSpan.Zero);
            healthCheckMetrics[(int)BehaviorTask.HealthCheck].ReportBehavior(newCapabilities.SupportsHealthCheck,
                BehaviorTask.HealthCheck, TimeSpan.Zero);
        }
        LastHealthCheckException = null;
    }

    public void ReportHealthCheck(IBlobCacheHealthDetails newHealth, IBlobCacheHealthDetails? priorHealth,
        TimeSpan taskDuration)
    {
        atFullHealth.ReportBehavior(newHealth.AtFullHealth, BehaviorTask.HealthCheck, taskDuration);
        AnyReport(newHealth.CurrentCapabilities, taskDuration);
    }

    public void ReportFailedHealthCheck(Exception exception, IBlobCacheHealthDetails? priorHealth, TimeSpan taskDuration)
    {
        LastHealthCheckException = exception;
        ReportBehavior(false, BehaviorTask.HealthCheck, taskDuration);
        AnyReport(null, taskDuration);
    }
    
    private BlobCacheCapabilities Intersect(BlobCacheCapabilities caps, BehaviorMetrics[] metrics)
    {
        if (caps.CanFetchData && metrics[(int)BehaviorTask.FetchData].SeemsDown())
        {
            caps = caps with {CanFetchData = false};
        }
        if (caps.CanFetchMetadata && metrics[(int)BehaviorTask.FetchMetadata].SeemsDown())
        {
            caps = caps with {CanFetchMetadata = false};
        }
        if (caps.CanPut && metrics[(int)BehaviorTask.Put].SeemsDown())
        {
            caps = caps with {CanPut = false};
        }
        if (caps.CanDelete && metrics[(int)BehaviorTask.Delete].SeemsDown())
        {
            caps = caps with {CanDelete = false};
        }
        if (caps.CanSearchByTag && metrics[(int)BehaviorTask.SearchByTag].SeemsDown())
        {
            caps = caps with {CanSearchByTag = false};
        }
        if (caps.CanPurgeByTag && metrics[(int)BehaviorTask.PurgeByTag].SeemsDown())
        {
            caps = caps with {CanPurgeByTag = false};
        }
        // We'll always estimate health checks as supported, because we handle that backoff ourselves.
        // if (caps.SupportsHealthCheck && metrics[(int)BehaviorTask.HealthCheck].SeemsDown())
        // {
        //     caps = caps with {SupportsHealthCheck = false};
        // }
        return caps;
    }
    private bool WasAdvertised(BehaviorTask task)
    {
        return initialAdvertisedCapabilities switch
        {
            {CanFetchData: true} when task == BehaviorTask.FetchData => true,
            {CanFetchMetadata: true} when task == BehaviorTask.FetchMetadata => true,
            {CanPut: true} when task == BehaviorTask.Put => true,
            {CanDelete: true} when task == BehaviorTask.Delete => true,
            {CanSearchByTag: true} when task == BehaviorTask.SearchByTag => true,
            {CanPurgeByTag: true} when task == BehaviorTask.PurgeByTag => true,
            {SupportsHealthCheck: true} when task == BehaviorTask.HealthCheck => true,
            _ => false
        };
    }

    public void WriteHealthCheckStatusTo(StringBuilder sb, string linePrefix)
    {
        
        // We have 4 states - healthy, unhealthy, unsupported, and not responding.
        // consecutiveIdenticalHealthCheckCaps determines how long it's been that way. (although unsupported we don't count)
        if (!SupportsHealthCheck)
        {
            sb.Append(linePrefix);
            sb.AppendLine("Health checks are not supported by this cache.");
            return;
        }
        // not responding
        else if (ConsecutiveCheckCrashes > 0)
        {
            sb.Append(linePrefix);
            sb.AppendLine($"Health check has crashed for the last {ConsecutiveCheckCrashes} times in a row ({LastHealthCheckException?.Message}).");
            
        } else if (ConsecutiveCheckResponses > 0)
        {
            var statusLabel = LastHealthCheckCompleted?.AtFullHealth == true ? "Healthy" : "Unhealthy";
            sb.Append(linePrefix);
            sb.AppendLine(
                $"Health check has reported [{statusLabel}] for the last {consecutiveIdenticalHealthCheckCaps} checks.");

        }
        if (LastCheckCompletedOrFailed == default)
        {
            // Shouldn't happen
            sb.Append(linePrefix);
            sb.AppendLine("Health check has not been attempted yet.");
            return;
        }
        
        var interval = GetOrAdjustHealthCheckInterval(true);
        // last attempt was {time} ago, next attempt in {time}, current interval is {time}
        var nextIn = (LastCheckCompletedOrFailed + interval - DateTimeOffset.UtcNow);
        var lastAgo = (DateTimeOffset.UtcNow - LastCheckCompletedOrFailed);
        sb.AppendLine($@"Last check was {lastAgo:hh\:mm\:ss}s, next attempt in {nextIn:hh\:mm\:ss}s, current interval is {lastDelay:hh\:mm\:ss}s.");
    }

    public void WriteMetricsTo(StringBuilder sb, string linePrefix, bool minimal)
    {
        WriteHealthCheckStatusTo(sb, linePrefix);
        var estimatedCapabilities = EstimateCapabilities();
        
        sb.Append(linePrefix);
        sb.AppendLine("Advertised cache capabilities:");
        initialAdvertisedCapabilities.WriteTruePairs(sb);
        sb.AppendLine();
        if (SupportsHealthCheck)
        {
            if (LastHealthCheckCompleted != null &&
                initialAdvertisedCapabilities != LastHealthCheckCompleted?.CurrentCapabilities)
            {
                sb.Append(linePrefix);
                sb.AppendLine("Last responding health check instead reported:");
                LastHealthCheckCompleted?.CurrentCapabilities.WriteTruePairs(sb);
                
                sb.Append("Difference: ");
                LastHealthCheckCompleted?.CurrentCapabilities.WriteReducedPairs(sb, initialAdvertisedCapabilities);
                sb.AppendLine();
            }
        }
        if (initialAdvertisedCapabilities != estimatedCapabilities)
        {
            sb.Append(linePrefix);
            sb.AppendLine("Current observed cache capabilities also differ: ");
            estimatedCapabilities.WriteTruePairs(sb);
            sb.AppendLine();
            sb.Append("Difference: ");
            estimatedCapabilities.WriteReducedPairs(sb, initialAdvertisedCapabilities);
            sb.AppendLine();
        }

        if (!minimal)
        {
            sb.Append(linePrefix);
            sb.AppendLine(HealthCheckCrashing
                ? "Observed behavior metrics: (due to health check crashes, we are using these instead)"
                : "Observed behavior metrics:");
            WriteMetricsTo(sb, collectedMetrics, linePrefix, minimal);
            if (!SupportsHealthCheck && !minimal)
            {
                sb.Append(linePrefix);
                sb.AppendLine("Health check reports over time:");
                WriteMetricsTo(sb, healthCheckMetrics, linePrefix, minimal);
            }
        }
    }
   
    private void WriteMetricsTo(StringBuilder sb, BehaviorMetrics[] metrics, string linePrefix, bool minimal)
    {
        foreach(var m in metrics){
            var advertised = WasAdvertised(m.Task);
            if (minimal && !advertised) continue;
            sb.Append(linePrefix);
            sb.Append(m.Task.ToString());   
            sb.Append(": ");
            if (advertised)
            {
                m.WriteSummaryTo(sb);
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("-- never implemented --");
            }
        }
    }
}