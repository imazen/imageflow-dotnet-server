using System.Collections;
using System.Collections.Specialized;
using Microsoft.Extensions.Primitives;

namespace Imazen.Routing.HttpAbstractions;


/// <summary>
/// Can be created over an IQueryCollection or NameValueCollection or IEnumerable<KeyValuePair<string,StringValues>> or Dictionary<string,string>
/// </summary>
public interface IReadOnlyQueryWrapper : IReadOnlyCollection<KeyValuePair<string,StringValues>>
{
    bool TryGetValue(string key, out string? value);
    bool TryGetValue(string key, out StringValues value);
    bool ContainsKey(string key);
    StringValues this[string key] { get; }
    IEnumerable<string> Keys { get; }
    
}

public class DictionaryQueryWrapper : IReadOnlyQueryWrapper
{
    public DictionaryQueryWrapper(IDictionary<string,StringValues> dictionary)
    {
        d = dictionary;
    }

    private readonly IDictionary<string,StringValues> d;
    
    internal IDictionary<string,StringValues> UnderlyingDictionary => d;
    
    public bool TryGetValue(string key, out string? value)
    {
        if (d.TryGetValue(key, out var values))
        {
            value = values;
            return true;
        }
        value = null;
        return false;
    }
    
    public bool TryGetValue(string key, out StringValues value)
    {
        return d.TryGetValue(key, out value);
    }
    
    public bool ContainsKey(string key)
    {
        return d.ContainsKey(key);
    }
    
    public StringValues this[string key] => d[key];
    
    public IEnumerable<string> Keys => d.Keys;
    
    public int Count => d.Count;
    
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
    {
        return d.GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    private static readonly Dictionary<string,StringValues> EmptyDict = new Dictionary<string, StringValues>();
    public static IReadOnlyQueryWrapper Empty { get; } = new DictionaryQueryWrapper(EmptyDict);
}

public class NameValueCollectionWrapper : IReadOnlyQueryWrapper
{
    public NameValueCollectionWrapper(NameValueCollection collection)
    {
        c = collection;
    }

    private readonly NameValueCollection c;

    public bool TryGetValue(string key, out string? value)
    {
        value = c[key];
        return value != null;
    }

    public bool TryGetValue(string key, out StringValues value)
    {
        value = c.GetValues(key);
        return value != StringValues.Empty;
    }

    public bool ContainsKey(string key)
    {
        return c[key] != null;
    }

    public StringValues this[string key] => (StringValues)(c.GetValues(key) ?? Array.Empty<string>());

    public IEnumerable<string> Keys
    {
        get
        {
            foreach (var k in c.AllKeys)
            {
                if (k != null) yield return k;
                yield return "";
            }
        }
    }

    public int Count => c.Count;
    
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
    {
        foreach (var key in c.AllKeys)
        {
            yield return new KeyValuePair<string, StringValues>(key ?? "", (StringValues)(c.GetValues(key) ?? Array.Empty<string>()));
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}