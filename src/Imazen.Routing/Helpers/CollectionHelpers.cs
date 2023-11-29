namespace Imazen.Routing.Helpers;

public static class CollectionHelpers
{
    public static void AddIfUnique<T>(this ICollection<T> collection, T value)
    {
        if (!collection.Contains(value)) collection.Add(value);
    }
}