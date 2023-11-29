using System.Text;
using Imazen.Abstractions.Resulting;

namespace Imazen.Abstractions.BlobCache;

public interface IBlobCacheHealthDetails
{
    bool AtFullHealth { get; }

    TimeSpan? SuggestedRecheckDelay { get; }
    string? Summary { get; }

    IReadOnlyList<CodeResult>? ErrorList { get; }

    BlobCacheCapabilities CurrentCapabilities { get; }

    DateTimeOffset? LastChecked { get; }
    
    string GetReport();
}

public record BlobCacheHealthDetails: IBlobCacheHealthDetails
{
    public required bool AtFullHealth { get; init; }

    public TimeSpan? SuggestedRecheckDelay { get; init; }
    public string? Summary { get; init; }

    public IReadOnlyList<CodeResult>? ErrorList { get; init; }

    public required BlobCacheCapabilities CurrentCapabilities { get; init; }

    public DateTimeOffset? LastChecked { get; init; }
    
    public string GetReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"At full health: {AtFullHealth}");
        sb.AppendLine($"Last checked: {LastChecked}");
        sb.AppendLine($"Current capabilities: {CurrentCapabilities}");
        sb.AppendLine($"Summary: {Summary}");
        if (ErrorList != null)
        {
            sb.AppendLine($"Errors: {string.Join("\n", ErrorList)}");
        }
        return sb.ToString();
    }
    
    public static BlobCacheHealthDetails FullHealth(BlobCacheCapabilities capabilities, DateTimeOffset? lastCheckedAt = null)
    {
        return new BlobCacheHealthDetails()
        {
            AtFullHealth = true,
            CurrentCapabilities = capabilities,
            LastChecked = lastCheckedAt ?? DateTimeOffset.UtcNow,
            Summary = "OK"
        };
    }
    
    public static BlobCacheHealthDetails Errors(string summary, IReadOnlyList<CodeResult> errors, BlobCacheCapabilities capabilities, DateTimeOffset? lastCheckedAt = null)
    {
        return new BlobCacheHealthDetails()
        {
            AtFullHealth = false,
            Summary = summary,
            CurrentCapabilities = capabilities,
            LastChecked = lastCheckedAt ?? DateTimeOffset.UtcNow,
            ErrorList = errors
            
        };
    }
    public static BlobCacheHealthDetails Error(CodeResult error, BlobCacheCapabilities capabilities, DateTimeOffset? lastCheckedAt = null)
    {
        return new BlobCacheHealthDetails()
        {
            AtFullHealth = false,
            Summary = error.ToString(),
            CurrentCapabilities = capabilities,
            LastChecked = lastCheckedAt ?? DateTimeOffset.UtcNow,
            ErrorList = new List<CodeResult>(){error}
            
        };
    }
}