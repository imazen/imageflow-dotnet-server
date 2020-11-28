namespace Imazen.HybridCache.Sqlite
{
    public class SqliteCacheDatabaseOptions
    {
        public SqliteCacheDatabaseOptions(string databaseDir)
        {
            DatabaseDir = databaseDir;
        }
        public string DatabaseDir { get; set; }

        /// <summary>
        /// This should be false unless you have a really good reason for it.
        /// The database interface uses async code and locking around async code causes 100-fold slowdowns
        /// whereas using the sqlite intrinsic concurrency is much faster
        /// </summary>
        public bool SynchronizeDatabaseCalls { get; set; } = false;
    }
}