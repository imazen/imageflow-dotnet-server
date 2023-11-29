namespace Imazen.Common.Instrumentation
{
    internal interface IImageJobInstrumentation
    {
        int? SourceWidth { get; }
        int? SourceHeight { get; }
        int? FinalWidth { get; }
        int? FinalHeight { get; }
        
        long TotalTicks { get; }
        
        long DecodeTicks { get; }
        long EncodeTicks { get; }
        
        /// <summary>
        /// var ext = PathUtils.GetExtension(job.SourcePathData).ToLowerInvariant().TrimStart('.');
        /// </summary>
        string? SourceFileExtension { get; }
        
        /// <summary>
        /// request?.Url?.DnsSafeHost
        /// </summary>
        string? ImageDomain { get; }
        /// <summary>
        /// request?.UrlReferrer?.DnsSafeHost
        /// </summary>
        string? PageDomain { get; }
        
        IEnumerable<string>? FinalCommandKeys { get;  }

    }
}