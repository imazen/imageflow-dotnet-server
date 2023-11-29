namespace Imazen.Routing.Helpers;

public static class EnumHelpers
{
    /// <summary>
    /// Maps a <see cref="StringComparison"/> to a short string representation
    /// Ordinal => "", OrdinalIgnoreCase => "(i)",
    /// CurrentCulture => "(current culture)", CurrentCultureIgnoreCase => "(current culture, i)",
    /// InvariantCulture => "(invariant)", InvariantCultureIgnoreCase => "(invariant i)"
    /// </summary>
    /// <param name="comparison"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToStringShort(this StringComparison comparison)
    {
        return comparison switch
        {
            StringComparison.CurrentCulture => "(current culture)",
            StringComparison.CurrentCultureIgnoreCase => "(current culture, i)",
            StringComparison.InvariantCulture => "(invariant)",
            StringComparison.InvariantCultureIgnoreCase => "(invariant i)",
            StringComparison.Ordinal => "",
            StringComparison.OrdinalIgnoreCase => "(i)",
            
            
            _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, null)
        };
    }
}