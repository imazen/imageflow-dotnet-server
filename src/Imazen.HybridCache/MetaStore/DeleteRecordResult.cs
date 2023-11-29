namespace Imazen.HybridCache.MetaStore
{
    internal enum DeleteRecordResult
    {
        Deleted,
        NotFound,
        RecordStaleReQueryRetry
    }
}