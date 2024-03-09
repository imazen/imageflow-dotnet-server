using Imazen.Routing.Layers;

namespace Imazen.Routing.Engine;

public class RoutingGroupPreconditionsBuilder
{
    internal List<IFastCond>? ExtensionlessPathPrefixes { get; set; }
    
    internal List<IFastCond>? PathExtensions { get; set; }
    
    internal List<IFastCond>? AlternatePreconditions { get; set; }
    
    internal List<IFastCond>? RequiredPreconditions { get; set; }
    public RoutingGroupPreconditionsBuilder ClearAllPreconditions()
    {
        ExtensionlessPathPrefixes = null;
        PathExtensions = null;
        AlternatePreconditions = null;
        return this;
    }
    
    public RoutingGroupPreconditionsBuilder IncludePathPrefixes(StringComparison comparison, params string[] prefixes)
    {
        (ExtensionlessPathPrefixes ??= []).Add(Conditions.HasPathPrefix(comparison, prefixes));
        return this;
    }
    
    public RoutingGroupPreconditionsBuilder IncludePathPrefixes(params string[] prefixes)
    {
        (ExtensionlessPathPrefixes ??= []).Add(Conditions.HasPathPrefix(StringComparison.OrdinalIgnoreCase, prefixes));
        return this;
    }
    public RoutingGroupPreconditionsBuilder IncludePathPrefixes<T>(IEnumerable<T> prefixes) where T : IStringAndComparison
    {
        (ExtensionlessPathPrefixes ??= []).Add(Conditions.HasPathPrefix(prefixes));
        return this;
    }
    public RoutingGroupPreconditionsBuilder IncludePathPrefixesCaseSensitive(params string[] prefixes)
    {
        (ExtensionlessPathPrefixes ??= []).Add(Conditions.HasPathPrefix(StringComparison.Ordinal, prefixes));
        return this;
    }
    
    public RoutingGroupPreconditionsBuilder IncludeFileExtensions(params string[] suffixes)
    {
        if (suffixes.Any(s => !s.StartsWith(".")))
        {
            throw new ArgumentException("File extensions must include the '.' prefix", nameof(suffixes));
        }
        (PathExtensions ??= []).Add(Conditions.HasPathSuffixOrdinalIgnoreCase(suffixes));
        return this;
    }

    public RoutingGroupPreconditionsBuilder IncludeDefaultImageExtensions()
    {
        (PathExtensions ??= []).Add(Conditions.HasSupportedImageExtension);
        return this;
    }

    public RoutingGroupPreconditionsBuilder IncludePathSuffixesCaseSensitive(params string[] suffixes)
    {
        (PathExtensions ??= []).Add(Conditions.HasPathSuffix(suffixes));
        return this;
    }
    public RoutingGroupPreconditionsBuilder IncludePathSuffixes<T>(IEnumerable<T> suffixes) where T : IStringAndComparison
    {
        (PathExtensions ??= []).Add(Conditions.HasPathSuffix(suffixes));
        return this;
    }
    
    
    public RoutingGroupPreconditionsBuilder IncludeRequestsMatching(IFastCond fastMatcher)
    {
        (AlternatePreconditions ??= []).Add(fastMatcher ?? Conditions.True);
        return this;
    }
    
    public RoutingGroupPreconditionsBuilder RequireAllRequestsMatch(IFastCond? fastMatcher)
    {
        (AlternatePreconditions ??= []).Add(fastMatcher ?? Conditions.True);
        return this;
    }

    public IFastCond ToOptimizedCondition() => (ExtensionlessPathPrefixes ?? Enumerable.Empty<IFastCond>())
        .Concat((PathExtensions ?? Enumerable.Empty<IFastCond>())
            .Concat(AlternatePreconditions ?? Enumerable.Empty<IFastCond>())).AnyPrecondition().And(
            (RequiredPreconditions ?? Enumerable.Empty<IFastCond>()).All()).Optimize().OrDefault(true);
}