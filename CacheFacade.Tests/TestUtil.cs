// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Data;
    using System.Threading;
    using Beztek.Facade.Queue;
    using Beztek.Facade.Sql;
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public static class TestUtil
    {
        internal static DateTime GetNow()
        {
            DateTime dt = DateTime.Now;
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
        }

        internal static Cache GetCache(CacheType cacheType, CancellationToken cancellationToken)
        {
            ILogger logger = new ServiceCollection()
                .AddLogging((loggingBuilder) => loggingBuilder
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddConsole()
                    ).BuildServiceProvider().GetService<ILoggerFactory>().CreateLogger<Cache>();
            string cacheName = Guid.NewGuid().ToString();
            ISqlFacade sqlFacade = null;
            IPersistenceService persistenceService = null;
            if (cacheType == CacheType.WriteThrough || cacheType == CacheType.WriteBehind)
            {
                sqlFacade = SqlFacadeFactory.GetSqlFacade(new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
                persistenceService = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, new TestSqlGenerator());
                InitializeDB(sqlFacade);
            }

            // Persist for 5 minutes
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);
            IQueueProviderConfig queueProviderConfig = new LocalMemoryQueueProviderConfig($"{cacheName}Queue");
            IQueueClient queueClient = QueueClientFactory.GetQueueClient(queueProviderConfig, logger);
            // Batch mode of max batch size of 100 for write behind cache
            QueueConfiguration queueConfiguration = null;

            if (cacheType == CacheType.WriteBehind)
            {
                CacheWriteBehindProcessor<TestEtagCacheable> cacheWriteBehindeProcessor = new CacheWriteBehindProcessor<TestEtagCacheable>(cacheName);
                queueConfiguration = new QueueConfiguration(queueClient, cacheWriteBehindeProcessor, cancellationToken, 1000, 5, 100, 10);
            }

            return (Cache)CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType, persistenceService, queueConfiguration), logger);
        }

        internal static void InitializeDB(ISqlFacade sqlFacade)
        {
            // Create the tables for the tests
            string stm1 = "CREATE TABLE IF NOT EXISTS test_etag_cacheable(id TEXT PRIMARY KEY, value TEXT, created_date TIMESTAMP, updated_date TIMESTAMP, etag TEXT)";
            string stm2 = "DELETE FROM test_etag_cacheable";

            using (IDbConnection con = sqlFacade.GetSqlFacadeConfig().GetConnection())
            {
                using var cmd1 = new SqliteCommand(stm1, (SqliteConnection)con);
                cmd1.ExecuteNonQuery();
                using var cmd2 = new SqliteCommand(stm2, (SqliteConnection)con);
                cmd2.ExecuteNonQuery();
            }
        }
    }
}
