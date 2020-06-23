using System;

namespace Imageflow.Server.SqliteCache
{
    public class SqliteCacheOptions
    {
        public string DatabaseDir { get; set; }
        
        public SqliteCacheOptions(string databaseDir)
        {
            DatabaseDir = databaseDir;
        }
    }
}