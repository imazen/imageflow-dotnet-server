using System.Runtime.CompilerServices;
using Imazen.Routing.Helpers;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Requests;

namespace Imazen.Routing.Layers;

public interface IFastCond
{
    bool Matches(string path, IReadOnlyQueryWrapper query);
}
public interface IFastCondOr : IFastCond
{
    IReadOnlyCollection<IFastCond> AlternateConditions { get; }
}

internal interface IFastCondCanMerge : IFastCond
{
    IFastCond? TryMerge(IFastCond other);
}

public interface IStringAndComparison
{
    string StringToCompare { get; }
    StringComparison StringComparison { get; }
}

public static class Conditions
{
    public static IFastCond Optimize(this IFastCond condition)
    {
        if (FastCondOptimizer.TryOptimize(condition, out var optimized))
        {
            return optimized;
        }
        return condition;
    }
    public static bool Matches(this IFastCond condition, IHttpRequestStreamAdapter request)
    {
        var path = request.GetPath().Value;
        if (path == null) return false;
        return condition.Matches(path, request.GetQuery());
    }
    public static bool Matches(this IFastCond condition, MutableRequest request)
    {
        return condition.Matches(request.MutablePath, request.ReadOnlyQueryWrapper);
    }
    // Exact match on path
    public static FastCondPathEquals PathEqualsOrdinalIgnoreCase(string path)
        => new FastCondPathEquals(path, StringComparison.OrdinalIgnoreCase);
    
    public static FastCondPathEquals PathEquals(string path)
        => new FastCondPathEquals(path, StringComparison.Ordinal);
    
    // All paths are potentially handled
    public static readonly FastCondTrue True = new FastCondTrue();
    public static readonly FastCondFalse False = new FastCondFalse();
    
    // Contains one of these querystring keys
    public static FastCondHasQueryStringKey HasQueryStringKey(params string[] keys)
        => new FastCondHasQueryStringKey(keys);
    
    public static readonly FastCondPathHasNoExtension PathHasNoExtension = new FastCondPathHasNoExtension();
    
    public static readonly FastCondHasPathSuffixes HasSupportedImageExtension = new FastCondHasPathSuffixes(PathHelpers.ImagePathSuffixes, StringComparison.OrdinalIgnoreCase);

    public static IFastCond HasPathPrefix<T>(IEnumerable<T> prefixes) where T : IStringAndComparison
    {
        return prefixes.GroupBy(p => p.StringComparison)
            .Select(g => (IFastCond)(new FastCondHasPathPrefixes(g.Select(p => p.StringToCompare).ToArray(), g.Key)))
            .AnyPrecondition().Optimize();
    }
    public static FastCondHasPathPrefixes HasPathPrefixOrdinalIgnoreCase(params string[] prefixes)
        => new FastCondHasPathPrefixes(prefixes, StringComparison.OrdinalIgnoreCase);
    
    public static FastCondHasPathPrefixes HasPathPrefixInvariantIgnoreCase(params string[] prefixes)
        => new FastCondHasPathPrefixes(prefixes, StringComparison.InvariantCultureIgnoreCase);
    public static FastCondAny AnyPrecondition<T>(this IEnumerable<T> preconditions) where T:IFastCond 
        => new(preconditions.Select(c => (IFastCond)c).ToList());
    
    public static FastCondAny AnyPrecondition(IReadOnlyCollection<IFastCond> preconditions) => new (preconditions);


    public static FastCondHasPathPrefixes HasPathPrefix(params string[] prefixes)
        => new FastCondHasPathPrefixes(prefixes, StringComparison.Ordinal);
    
    public static FastCondHasPathPrefixes HasPathPrefixInvariant(params string[] prefixes)
        => new FastCondHasPathPrefixes(prefixes, StringComparison.InvariantCulture);
    
    public static FastCondHasPathSuffixes HasPathSuffixOrdinalIgnoreCase(params string[] suffixes)
        => new FastCondHasPathSuffixes(suffixes, StringComparison.OrdinalIgnoreCase);
    
    public static FastCondHasPathSuffixes HasPathSuffix(params string[] suffixes)
        => new FastCondHasPathSuffixes(suffixes, StringComparison.Ordinal);

    
    public static FastCondAnd<T, TU> And<T, TU>(this T a, TU b) where T : IFastCond where TU : IFastCond
        => new FastCondAnd<T, TU>(a, b);

    public static FastCondOr<T, TU> Or<T, TU>(this T a, TU b) where T : IFastCond where TU : IFastCond
        => new FastCondOr<T, TU>(a, b);

    // NOT
    public static FastCondNot<T> Not<T>(this T a) where T : IFastCond
        => new FastCondNot<T>(a);
    
    private interface IFastCondNot : IFastCond
    {
        IFastCond NotThis { get; }
    }
    
    public readonly record struct FastCondNot<T>(T A) : IFastCondNot where T : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
            => !A.Matches(path, query);
        
        public override string ToString() => $"not({A})";
        public IFastCond NotThis => A;
    }
    
    public readonly record struct FastCondTrue : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
            => true;
        
        public override string ToString() => "true()";
    }
    public readonly record struct FastCondFalse : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
            => false;
        
        public override string ToString() => "false()";
    }
    private interface IFastCondAnd : IFastCond
    {
        IEnumerable<IFastCond> RequiredConditions { get; }
    }

    public readonly record struct FastCondAnd<T, TU>(T A, TU B)
        : IFastCondAnd where T : IFastCond where TU : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
            => A.Matches(path, query) && B.Matches(path, query);

        public override string ToString() => $"and({A}, {B})";

        public IEnumerable<IFastCond> RequiredConditions
        {
            get
            {
                yield return A;
                yield return B;
            }
        }
    
    }

    public readonly record struct FastCondOr<T, TU>(T A, TU B) : IFastCondOr where T : IFastCond where TU : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
            => A.Matches(path, query) || B.Matches(path, query);
        
        public IReadOnlyCollection<IFastCond> AlternateConditions => new IFastCond[]{A, B};
        
        public override string ToString()
        {
            return $"or({A}, {B})";
        }
    }

    internal static class FastCondOptimizer
    {
        internal static string[] CombineUnique(string[] a, string[] b)
        {
            if (a == b) return a;
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            if (a.Length == b.Length)
            {
                if (a.SequenceEqual(b)) return a;
            }
            // We have tiny arrays, so iteration is better than allocation
            var combined = new List<string>(a.Length + b.Length);
            foreach (var s in a)
            {
                if (!combined.Contains(s, StringComparer.Ordinal))
                {
                    combined.Add(s);
                }
            }
            foreach (var s in b)
            {
                if (!combined.Contains(s, StringComparer.Ordinal))
                {
                    combined.Add(s);
                }
            }
            return combined.ToArray();
        }
        private static void FlattenRecursiveAndOptimize<T>(List<IFastCond?> flattened, IEnumerable<T> alternateConditions) where T:IFastCond
        {
            // We flatten the Ors, and optimize everything else individually. 
            // Our caller is responsible for optimizing the final list of OR conditions
            foreach (var condition in alternateConditions)
            {
                // Covers FastCondAnyNaive, FastCondAnyOptimized, FastCondOr
                if (condition is IFastCondOr orCondition)
                {
                    FlattenRecursiveAndOptimize(flattened, orCondition.AlternateConditions);
                }
                else{
                    if (TryOptimize(condition, out var replacement))
                    {
                        if (replacement is IFastCondOr replacementOr)
                        {
                            FlattenRecursiveAndOptimize(flattened, replacementOr.AlternateConditions);
                        }
                        else
                        {
                            flattened.Add(replacement);
                        }
                    }
                    else
                    {
                        flattened.Add(condition);
                    }
                }
            }
        }

        
        internal static List<IFastCond> FlattenRecursiveOptimizeMergeAndOptimize<T>(IEnumerable<T> alternateConditions) where T:IFastCond
        {
            var flattened = new List<IFastCond?>((alternateConditions as IReadOnlyCollection<T>)?.Count ?? 2 + 2);
            FlattenRecursiveAndOptimize(flattened, alternateConditions);
            //if any are True, all others can be removed
            if (flattened.Any(f => f is FastCondTrue))
            {
                return [new FastCondTrue()];
            }
            // false can be removed
            flattened.RemoveAll(f => f is FastCondFalse);
            
            var combinedCount = flattened.Count;
            // Now try to combine, each with every other, stealing from the flattened list
            for (var ix = 0; ix < flattened.Count; ix++)
            {
                var a = flattened[ix];
                if (a == null) continue;
                for (var jx = ix + 1; jx < flattened.Count; jx++)
                {
                    var b = flattened[jx];
                    if (b == null) continue;
                    if (a is not IFastCondCanMerge mergeableA || b is not IFastCondCanMerge mergeableB) continue;
                    
                    var merged = mergeableA.TryMerge(mergeableB);
                    if (merged == null) continue;
                    // If we merged it, try to optimize it again.
                    if (TryOptimize(merged, out var optimized))
                    {
                        merged = optimized;
                    }
                    
                    // Null the inner loop target when we merge, so we don't have to check it again
                    flattened[ix] = merged;
                    flattened[jx] = null;
                    a = merged;
                    combinedCount--;
                }
            }
            // Now remove nulls
            var final = new List<IFastCond>(combinedCount);
            foreach (var condition in flattened)
            {
                if (condition != null)
                {
                    final.Add(condition);
                }
            }
            // Now order most likely first
            TryOptimizeOrder(ref final, true);
            return final;
        }

        internal static int GetSortRankFor(IFastCond? c, bool moreLikelyIsHigher)
        {
            // Anything to do with path extensions (typically path suffixes)
            // Then anything to do with path prefixes
            // Then anything to do with querystring keys
            // we recurse into any nested ORs, using (moreLikelyIsBetter = true) and ANDs (moreLikelyIsBetter = false)
            // And NOTs invert the previous
            
            var rank = c switch
            {
                null => 0,
                FastCondPathHasNoExtension => 100,
                FastCondHasPathSuffixes => 90,
                FastCondHasPathPrefixes => 80,
                FastCondHasQueryStringKey => 70,
                FastCondPathEquals => 5,
                FastCondPathEqualsAny => 10,
                IFastCondAnd a => GetSortRankFor(a.RequiredConditions.First(),false),
                IFastCondOr o => GetSortRankFor(o.AlternateConditions.First(),true),
                IFastCondNot n => GetSortRankFor(n.NotThis, !moreLikelyIsHigher),
                _ => 0
            };
            
            return moreLikelyIsHigher ? rank : -rank;
        }
        internal static void TryOptimizeOrder(ref List<IFastCond> conditions, bool MostLikelyOnTop)
        {
            // We will only have OptimizedAnys, nots, and various matchers.
            // Primarily we want the most exclusive matchers first, so we can short-circuit.
            // Anything to do with path extensions at the top
            // Then anything to do with path prefixes
            // Then anything to do with querystring keys
            // then anything to do with path suffixes.
            // we recurse into any nested ORs
            if (MostLikelyOnTop)
                conditions.Sort((a, b) =>
                    GetSortRankFor(a, false).CompareTo(GetSortRankFor(b, false)));
            else
            {
                conditions.Sort((a, b) =>
                    GetSortRankFor(a, true).CompareTo(GetSortRankFor(b, true)));
            }
        }

        internal static bool TryOptimize(IFastCond logicalOp, out IFastCond replacement)
        {
            if (logicalOp is IFastCondAnd andOp)
            {
                var newAnd =
                    andOp.RequiredConditions
                        .Select(c => c.Optimize())
                        .Where(c => c is not FastCondTrue)
                        .ToList();
                
                if (newAnd.Any(c => c is FastCondFalse))
                {
                    replacement = Conditions.False;
                    return true;
                }
                if (newAnd.Any(c => c is IFastCondAnd))
                {
                    var expanded = new List<IFastCond>(newAnd.Count * 2);
                    foreach (var c in newAnd)
                    {
                        if (c is IFastCondAnd and)
                        {
                            expanded.AddRange(and.RequiredConditions);
                        }
                        else
                        {
                            expanded.Add(c);
                        }
                    }
                    newAnd = expanded;
                }
                if (newAnd.Count == 1)
                {
                    replacement = newAnd[0];
                    return true;
                }

                TryOptimizeOrder(ref newAnd, false);
                replacement = new FastCondAll(newAnd);
                return true;
                
            }
            else if (logicalOp is IFastCondOr orOp)
            {
                var optimizedOr = new FastCondAny(
                    FastCondOptimizer.FlattenRecursiveOptimizeMergeAndOptimize(orOp.AlternateConditions));
                replacement = optimizedOr.AlternateConditions.Count == 1 ? optimizedOr.AlternateConditions.First()
                        : optimizedOr;
                return true;
            }
            else if (logicalOp is IFastCondNot notOp)
            {
                
                if (notOp.NotThis is FastCondTrue)
                {
                    replacement = new FastCondFalse();
                    return true;
                }
                if (notOp.NotThis is FastCondFalse)
                {
                    replacement = new FastCondTrue();
                    return true;
                }
                if (TryOptimize(notOp.NotThis, out var optimized))
                {
                    replacement = new FastCondNot<IFastCond>(optimized);
                    return true;
                }
                // we could invert and -> not(each(or()) or or to and(each(not())) if some 
                // sub-conditions are faster that way, but I don't see any at this point.
            
            }
            replacement = logicalOp;
            return false;
        }
    }
    
   
    public readonly record struct FastCondAny(IReadOnlyCollection<IFastCond> Conditions) : IFastCondOr, IFastCondCanMerge
    {
        public IReadOnlyCollection<IFastCond> AlternateConditions => Conditions;

        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (Conditions.Count == 0) return false;
            foreach (var condition in Conditions)
            {
                if (condition.Matches(path, query))
                {
                    return true;
                }
            }
            return false;
        }

        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is IFastCondOr otherAny)
            {
                return new FastCondAny(Conditions.Concat(otherAny.AlternateConditions).ToArray());
            }
            return null;
        }

        public override string ToString()
        {
            return $"any({string.Join(", ", Conditions)})";
        }
    }
    
    // All
    public readonly record struct FastCondAll(IReadOnlyCollection<IFastCond> Conditions) : IFastCondAnd, IFastCondCanMerge
    {
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (Conditions.Count == 0) return true;
            foreach (var condition in Conditions)
            {
                if (!condition.Matches(path, query))
                {
                    return false;
                }
            }
            return true;
        }

        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is IFastCondAnd otherAll)
            {
                return new FastCondAll(Conditions.Concat(otherAll.RequiredConditions).ToArray());
            }
            return null;
        }

        public override string ToString()
        {
            return $"all({string.Join(", ", Conditions)})";
        }

        public IEnumerable<IFastCond> RequiredConditions => Conditions;
    }
    
    public readonly record struct FastCondHasPathPrefixes(string[] Prefixes, StringComparison StringComparison) : IFastCondCanMerge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (Prefixes.Length == 0) throw new InvalidOperationException("FastCondHasPathPrefixes must have at least one prefix");
            foreach (var prefix in Prefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        
        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is FastCondHasPathPrefixes otherPrefix && otherPrefix.StringComparison == StringComparison)
            {
                return new FastCondHasPathPrefixes(FastCondOptimizer.CombineUnique(Prefixes, otherPrefix.Prefixes), StringComparison);
            }
            return null;
        }
        
        // tostring (path starts with one of { prefix1, prefix2, prefix3 } using StringComparison)
        
        public override string ToString()
        {
            if (StringComparison == StringComparison.OrdinalIgnoreCase)
            {
                return $"path.starts_with_i('{string.Join("', '", Prefixes)}')";
            }
            if (StringComparison == StringComparison.Ordinal)
            {
                return $"path.starts_with('{string.Join("', '", Prefixes)}')";
            }
            return $"path.starts_with({StringComparison}, '{string.Join("', '", Prefixes)}')";
        }
    }
    
    // Has processable image extension
    // public readonly record struct FastCondHasSupportedImageExtension : IFastCond
    // {
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public bool Matches(string path, IReadOnlyQueryWrapper query)
    //     {
    //         return PathHelpers.IsImagePath(path);
    //     }
    //     
    //     // tostring (has a supported image extension)
    //     
    //     public override string ToString()
    //     {
    //         return "path.extension.supported_image_type()";
    //     }
    //     
    // }
    
    // has one of the specified extensions (they must include the .)
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="AllowedSuffixes">The allowed suffixes (usually extensions, including the leading '.')</param>
    public readonly record struct FastCondHasPathSuffixes(string[] AllowedSuffixes, StringComparison StringComparison) : IFastCondCanMerge
    {
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (AllowedSuffixes.Length == 0) throw new InvalidOperationException("FastCondHasPathSuffixes must have at least one suffix");
            foreach (var suffix in AllowedSuffixes)
            {
                if (path.EndsWith(suffix, StringComparison))
                {
                    return true;
                }
            }
            return false;
        }
        
        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is FastCondHasPathSuffixes otherSuffixes && otherSuffixes.StringComparison == StringComparison)
            {
                return new FastCondHasPathSuffixes(FastCondOptimizer.CombineUnique(AllowedSuffixes, otherSuffixes.AllowedSuffixes), StringComparison);
            }
            return null;
        }
        
        // tostring (path has one of { suffix1, suffix2, suffix3 } using StringComparison)
        
        public override string ToString()
        {
            if (StringComparison == StringComparison.OrdinalIgnoreCase)
            {
                return $"path.ends_with_i('{string.Join("', '", AllowedSuffixes)}')";
            }
            if (StringComparison == StringComparison.Ordinal)
            {
                return $"path.ends_with('{string.Join("', '", AllowedSuffixes)}')";
            }
            return $"path.ends_with({StringComparison}, '{string.Join("', '", AllowedSuffixes)}')";
        }
    }

    
    // Extensionless 
    public readonly record struct FastCondPathHasNoExtension : IFastCond
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            return path.LastIndexOf('.').Equals(-1);
        }
        
        // tostring (path has no extension)
        
        public override string ToString()
        {
            return "path.extension.is_empty()";
        }
    }
    
    // exact match on path
    public readonly record struct FastCondPathEquals(string Path, StringComparison StringComparison) : IFastCondCanMerge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            return Path.Equals(path, StringComparison);
        }
        
        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is FastCondPathEquals otherPath && otherPath.StringComparison == StringComparison)
            {
                return new FastCondPathEqualsAny(FastCondOptimizer.CombineUnique(new string[]{Path}, new string[]{otherPath.Path}), StringComparison);
            }
            // merge with FastCondPathEqualsAny
            if (other is FastCondPathEqualsAny otherPaths && otherPaths.StringComparison == StringComparison)
            {
                return new FastCondPathEqualsAny(FastCondOptimizer.CombineUnique(new string[]{Path}, otherPaths.Paths), StringComparison);
            }
            return null;
        }
        
        // tostring (path is { path } using StringComparison)
        
        public override string ToString()
        {
            if (StringComparison == StringComparison.OrdinalIgnoreCase)
            {
                return $"path.equals_i('{Path}')";
            }
            if (StringComparison == StringComparison.Ordinal)
            {
                return $"path.equals('{Path}')";
            }
            return $"path.equals({StringComparison}, '{Path}')";
        }
    }
    
    public readonly record struct FastCondPathEqualsAny(string[] Paths, StringComparison StringComparison) : IFastCondCanMerge
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (Paths.Length == 0) throw new InvalidOperationException("FastCondPathEqualsAny must have at least one path");
            foreach (var p in Paths)
            {
                if (p.Equals(path, StringComparison))
                {
                    return true;
                }
            }
            return false;
        }
        
        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is FastCondPathEqualsAny otherPaths && otherPaths.StringComparison == StringComparison)
            {
                return new FastCondPathEqualsAny(FastCondOptimizer.CombineUnique(Paths, otherPaths.Paths), StringComparison);
            }
            // merge with FastCondPathEquals
            if (other is FastCondPathEquals otherPath && otherPath.StringComparison == StringComparison)
            {
                return new FastCondPathEqualsAny(FastCondOptimizer.CombineUnique(Paths, new string[]{otherPath.Path}), StringComparison);
            }
            return null;
        }
        
        // tostring (path is one of { path1, path2, path3 } using StringComparison)
        
        public override string ToString()
        {
            if (StringComparison == StringComparison.OrdinalIgnoreCase)
            {
                return $"path.equals_i('{string.Join("', '", Paths)}')";
            }
            if (StringComparison == StringComparison.Ordinal)
            {
                return $"path.equals('{string.Join("', '", Paths)}')";
            }
            return $"path.equals({StringComparison}, '{string.Join("', '", Paths)}')";
            
        }
    }
    
    
    // Has querystring key (case sensitive)
    public readonly record struct FastCondHasQueryStringKey(string[] Keys) : IFastCondCanMerge
    {
        public bool Matches(string path, IReadOnlyQueryWrapper query)
        {
            if (Keys.Length == 0) throw new InvalidOperationException("FastCondHasQueryStringKey must have at least one key");
            foreach (var key in Keys)
            {
                if (query.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }
        
        public IFastCond? TryMerge(IFastCond other)
        {
            if (other is FastCondHasQueryStringKey otherKeys)
            {
                return new FastCondHasQueryStringKey(FastCondOptimizer.CombineUnique(Keys, otherKeys.Keys));
            }
            return null;
        }
        
        // tostring (has a querystring key from { key1, key2, key3 })
        public override string ToString()
        {
            return $"query.has_key('{string.Join("', '", Keys)}')";
        }
        
    }

    
}

