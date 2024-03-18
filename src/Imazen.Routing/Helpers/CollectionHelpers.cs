namespace Imazen.Routing.Helpers;

internal static class CollectionHelpers
{
    public static void AddIfUnique<T>(this ICollection<T> collection, T value)
    {
        if (!collection.Contains(value)) collection.Add(value);
    }
}