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
        ///
        /// Perhaps the really good reason is that the changes() function misbehaves if used concurrently
        /// </summary>
        public bool SynchronizeDatabaseCalls { get; set; } = false;

        /// <summary>
        /// False will cause lots of BUSY failures and other locking issues
        /// </summary>
        public bool ShareDatabaseConnection { get; set; } = true;
    }
}