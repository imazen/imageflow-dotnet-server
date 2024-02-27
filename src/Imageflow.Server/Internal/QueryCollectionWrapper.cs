using System.Collections;
using Imazen.Routing.HttpAbstractions;
using Imazen.Routing.Serving;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Imageflow.Server.Internal;


internal readonly struct QueryCollectionWrapper : IReadOnlyQueryWrapper
{
    public QueryCollectionWrapper(IQueryCollection collection)
    {
        c = collection;
    }

    private readonly IQueryCollection c;
    
    public bool TryGetValue(string key, out string? value)
    {
        if (c.TryGetValue(key, out var values))
        {
            value = values.ToString();
            return true;
        }
        value = null;
        return false;
    }
    
    public bool TryGetValue(string key, out StringValues value) => c.TryGetValue(key, out value);

    public bool ContainsKey(string key) => c.ContainsKey(key);

    public StringValues this[string key] => c[key];
    
    public IEnumerable<string> Keys => c.Keys;

    
    public int Count => c.Count;
    
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => c.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
    
    
    