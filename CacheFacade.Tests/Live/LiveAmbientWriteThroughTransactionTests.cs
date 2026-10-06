// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Beztek.Facade.Sql;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;

    /// <summary>
    /// Live WriteThrough ambient <see cref="TransactionScope"/> tests against enlisting SQL engines
    /// (not SQLite). Discovered only when <c>CACHEFACADE_LIVE_SQL_ENGINES</c> is set.
    /// <para>
    /// One engine: <c>CACHEFACADE_LIVE_SQL_ENGINES=postgres</c><br/>
    /// All enlisting engines: <c>CACHEFACADE_LIVE_SQL_ENGINES=all</c>
    /// </para>
    /// </summary>
    [TestFixtureSource(typeof(LiveSqlEngineFixtureSource), nameof(LiveSqlEngineFixtureSource.Engines))]
    [Category("Live")]
    public class LiveAmbientWriteThroughTransactionTests
    {
        private readonly DbType _dbType;
        private LiveSqlEngineHost _host;
        private ILogger _logger;

        public LiveAmbientWriteThroughTransactionTests(DbType dbType)
        {
            _dbType = dbType;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _logger = new ServiceCollection()
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning).AddConsole())
                .BuildServiceProvider()
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("LiveAmbientWriteThrough");

            try
            {
                _host = await LiveSqlEngineHost.StartAsync(_dbType).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_host, Is.Not.Null);
            LiveWriteThroughSchema.Reset(_host.Sql);
        }

        [Test]
        public async Task Abort_RollsBackSql_AndEvictsCache()
        {
            Cache cache = CreateWriteThroughCache();
            try
            {
                TestEtagCacheable entity = NewEntity("live-abort");

                using (NewReadCommittedScope())
                {
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                    Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Not.Null);
                    Assert.That(LiveWriteThroughSchema.CountRows(_host.Sql, entity.Id), Is.EqualTo(1),
                        "Row should be visible inside the open ambient transaction.");
                }

                Assert.That(LiveWriteThroughSchema.CountRows(_host.Sql, entity.Id), Is.EqualTo(0),
                    "SQL should roll back when the ambient scope is not completed.");
                Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null,
                    "WriteThrough should evict the provider key on ambient abort.");
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Complete_CommitsSql_AndKeepsCache()
        {
            Cache cache = CreateWriteThroughCache();
            try
            {
                TestEtagCacheable entity = NewEntity("live-commit");

                using (TransactionScope scope = NewReadCommittedScope())
                {
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                    scope.Complete();
                }

                Assert.That(LiveWriteThroughSchema.CountRows(_host.Sql, entity.Id), Is.EqualTo(1));
                TestEtagCacheable peeked = await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                Assert.That(peeked, Is.Not.Null);
                Assert.That(peeked.Value, Is.EqualTo("live-commit"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task ExternalFailureBeforeComplete_RollsBackSql_AndEvictsCache()
        {
            Cache cache = CreateWriteThroughCache();
            try
            {
                TestEtagCacheable entity = NewEntity("live-external-fail");

                try
                {
                    using TransactionScope scope = NewReadCommittedScope();
                    await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
                    throw new InvalidOperationException("simulated external failure");
                }
                catch (InvalidOperationException)
                {
                    // expected
                }

                Assert.That(LiveWriteThroughSchema.CountRows(_host.Sql, entity.Id), Is.EqualTo(0));
                Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task DefaultSerializableScope_ThrowsOnJoin()
        {
            Cache cache = CreateWriteThroughCache();
            try
            {
                TestEtagCacheable entity = NewEntity("live-serializable");

                Exception ex = Assert.CatchAsync(async () =>
                {
                    using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                    scope.Complete();
                });
                Assert.That(ex, Is.Not.Null);
                Assert.That(
                    ex!.Message + ex,
                    Does.Contain("transaction").IgnoreCase.Or.Contain("isolation").IgnoreCase);
            }
            finally
            {
                cache.Dispose();
            }
        }

        private Cache CreateWriteThroughCache()
        {
            IPersistenceService persistence = new SqlPersistenceService<TestEtagCacheable>(_host.Sql, new TestSqlGenerator());
            string cacheName = "live-wt-" + Guid.NewGuid().ToString("N");
            var providerConfig = new LocalMemoryProviderConfiguration(cacheName, 300_000);
            var cacheConfig = new CacheConfiguration(providerConfig, CacheType.WriteThrough, persistence);
            return (Cache)CacheFactory.GetOrCreateCache(cacheConfig, _logger);
        }

        private static TransactionScope NewReadCommittedScope() =>
            new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

        private static TestEtagCacheable NewEntity(string value) =>
            new TestEtagCacheable(
                Guid.NewGuid().ToString("N"),
                value,
                TestUtil.GetNow(),
                TestUtil.GetNow(),
                null);
    }
}
