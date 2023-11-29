namespace Imazen.Abstractions.HttpStrings;

#if NETSTANDARD2_1_OR_GREATER
#else
internal class SearchValues<T>
{
    internal SearchValues(T[] values)
    {
        Values = values;
    }

    public T[] Values { get; }

    public bool Contains(T c)
    {
        return Values.Contains(c);
    }
}

internal static class SearchValues
{
    public static SearchValues<char> Create(string values)
    {
        return new SearchValues<char>(values.ToCharArray());
    }
}
    
internal static class SpanExtensions
{
    public static int IndexOfAnyExcept(this ReadOnlySpan<char> span, SearchValues<char> searchValues)
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (searchValues.Contains(c)) continue;
            return i;
        }

        return -1;
    }
    //     // ContainsAnyExcept
    
    public static bool ContainsAnyExcept(this ReadOnlySpan<char> span, SearchValues<char> searchValues)
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (searchValues.Contains(c)) continue;
            return true;
        }

        return false;
    }
}
#endif