namespace Imazen.HybridCache.Sqlite
{
    public class SqliteCacheDatabaseOptions
    {
        public SqliteCacheDatabaseOptions(string databaseDir)
        {
            DatabaseDir = databaseDir;
        }
        public string DatabaseDir { get; set; }

        public bool SynchronizeDatabaseCalls { get; set; } = false;
    }
}