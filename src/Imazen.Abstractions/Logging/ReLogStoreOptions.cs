namespace Imazen.Abstractions.Logging
{
    public class ReLogStoreOptions
    {
        public int MaxEventGroups { get; set; } = 50;
        public int MaxEntriesPerUniqueKey { get; set; } = 3;
        public int MaxEntriesPerExceptionClass { get; set; } = 5;
    }
}