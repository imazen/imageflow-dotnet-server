namespace Imazen.Abstractions
{
    public interface IUniqueNamed
    {
        /// <summary>
        /// Must be unique across all caches and providers
        /// </summary>

        string UniqueName { get; }
    }
    
    public static class UniqueNamedExtensions
    {
        public static string NameAndClass<T>(this T obj) where T : IUniqueNamed
        {
            return $"{obj.UniqueName} ({obj.GetType().Name})";
        }
    }
}