// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Beztek.Facade.Cache.Providers;
    using Beztek.Facade.Sql;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Extra unit coverage for soft-spot classes from <c>make coverage</c>
    /// (AmbientWriteThroughTransactionCleanup, CacheFactory race, provider Evict, etc.).
    /// </summary>
    [TestFixture]
    public class SoftSpotCoverageTests
    {
        [Test]
        public void CacheFactory_TryUnregister_RejectsNullOrBlank()
        {
            Assert.That(CacheFactory.TryUnregister(null, Mock.Of<ICache>()), Is.False);
            Assert.That(CacheFactory.TryUnregister("", Mock.Of<ICache>()), Is.False);
            Assert.That(CacheFactory.TryUnregister("name", null), Is.False);
        }

        [Test]
        public void CacheFactory_ParallelCreate_SameName_SingleWinner()
        {
            string cacheName = "race-" + Guid.NewGuid().ToString("N");
            var config = new CacheConfiguration(
                new LocalMemoryProviderConfiguration(cacheName, 300000),
                CacheType.NonPersistent);
            var seen = new ConcurrentBag<ICache>();

            Parallel.For(0, 40, _ => seen.Add(CacheFactory.GetOrCreateCache(config)));

            ICache[] distinct = seen.Distinct().ToArray();
            Assert.That(distinct, Has.Length.EqualTo(1));
            ((Cache)distinct[0]).Dispose();
            Assert.That(CacheFactory.GetCache(cacheName), Is.Null);
        }

        [Test]
        public async Task DisposeAsync_IsIdempotent_AndDisposesAsyncProvider()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var provider = new Mock<ICacheProvider>(MockBehavior.Loose);
            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new TestCacheProviderConfiguration(provider.Object, cacheName, 300000),
                    CacheType.NonPersistent));

            var asyncDisposable = new Mock<IAsyncDisposable>();
            asyncDisposable.Setup(d => d.DisposeAsync()).Returns(ValueTask.CompletedTask);
            typeof(Cache)
                .GetField("asyncDisposableProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cache, asyncDisposable.Object);

            await cache.DisposeAsync().ConfigureAwait(false);
            await cache.DisposeAsync().ConfigureAwait(false);

            asyncDisposable.Verify(d => d.DisposeAsync(), Times.Once);
        }

        [Test]
        public async Task RemoveAsync_WhenPersistenceFails_RollsBackProviderPut()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var provider = new Mock<ICacheProvider>(MockBehavior.Strict);
            var persistence = new Mock<IPersistenceService>(MockBehavior.Strict);
            var entity = new TestEtagCacheable(
                "k1", "v1", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());

            provider.Setup(p => p.Get<TestEtagCacheable>("k1")).Returns(entity);
            provider.Setup(p => p.Remove<TestEtagCacheable>("k1")).Returns(entity);
            provider.Setup(p => p.Put("k1", entity));
            persistence.Setup(p => p.DeleteAsync("k1")).ThrowsAsync(new InvalidOperationException("db-down"));

            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new TestCacheProviderConfiguration(provider.Object, cacheName, 300000),
                    CacheType.WriteThrough,
                    persistence.Object));
            cache.CacheProvider = provider.Object;
            try
            {
                Assert.ThrowsAsync<IOException>(async () =>
                    await cache.RemoveAsync<TestEtagCacheable>("k1").ConfigureAwait(false));
                provider.Verify(p => p.Put("k1", entity), Times.Once);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task WriteBehind_EntityWithoutPriorEtag_StampsSequence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            try
            {
                var entity = new WriteBehindOnlyEntity { Id = Guid.NewGuid().ToString(), Etag = null };
                await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                Assert.That(EtagUtil.ParseSequentialEtag(entity.Etag), Is.GreaterThan(0));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void BuildWriteBehindMessage_EmptyEtag_AssignsNextSequence()
        {
            MethodInfo method = typeof(Cache).GetMethod(
                "BuildWriteBehindMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var entity = new WriteBehindOnlyEntity { Id = "wb-1", Etag = null };
            object message = method!.Invoke(null, new object[] { entity.Id, WriteType.Create, entity });
            Assert.That(message, Is.Not.Null);
            Assert.That(EtagUtil.ParseSequentialEtag(entity.Etag), Is.GreaterThan(0));
            Assert.That(entity.IsDeleted, Is.False);
        }

        [Test]
        public void RedisLock_FactoryCtor_WiresCreateDelegate()
        {
            // Construction alone exercises CreateViaFactory (no live Redis acquire).
            var multiplexer = new Mock<StackExchange.Redis.IConnectionMultiplexer>(MockBehavior.Loose);
            multiplexer.SetupGet(m => m.IsConnected).Returns(true);
            var factory = RedLockNet.SERedis.RedLockFactory.Create(
                new List<RedLockNet.SERedis.Configuration.RedLockMultiplexer>
                {
                    new RedLockNet.SERedis.Configuration.RedLockMultiplexer(multiplexer.Object)
                });
            try
            {
                var redisLock = new RedisLock(factory);
                Assert.That(redisLock, Is.Not.Null);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public async Task AmbientAbort_WhenEvictThrows_DoesNotThrowToCaller()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var provider = new Mock<ICacheProvider>(MockBehavior.Loose);
            provider.Setup(p => p.Evict(It.IsAny<string>())).Throws(new InvalidOperationException("evict-boom"));

            ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(
                new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            var persistence = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, new TestSqlGenerator());
            TestUtil.InitializeDB(sqlFacade);

            var logger = new Mock<ILogger>();
            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new TestCacheProviderConfiguration(provider.Object, cacheName, 300000),
                    CacheType.WriteThrough,
                    persistence),
                logger.Object);
            cache.CacheProvider = provider.Object;

            try
            {
                var entity = new TestEtagCacheable(
                    Guid.NewGuid().ToString(), "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());

                using (NewScope())
                {
                    provider.Setup(p => p.Get<TestEtagCacheable>(entity.Id)).Returns((TestEtagCacheable)null);
                    provider.Setup(p => p.Put(entity.Id, It.IsAny<TestEtagCacheable>()));
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                }

                // Evict threw inside TransactionCompleted; cleanup must swallow it.
                provider.Verify(p => p.Evict(entity.Id), Times.AtLeastOnce);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void AmbientCleanup_TrackEmptyKey_IsNoOp_AndFinishOnCompleted()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new LocalMemoryProviderConfiguration(cacheName, 300000),
                    CacheType.NonPersistent));
            try
            {
                var cleanup = new AmbientWriteThroughTransactionCleanup(cache, "tx-empty");
                cleanup.Track(null);
                cleanup.Track(string.Empty);
                cleanup.Track("k");
                // Non-aborted / null transaction still finishes and unregisters.
                cleanup.OnTransactionCompleted(null, null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void EtagUtil_NextSequence_SurvivesConcurrentContention()
        {
            var values = new ConcurrentBag<long>();
            Parallel.For(0, 200, _ => values.Add(EtagUtil.NextSequence()));
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count));
        }

        [Test]
        public void DisposableLock_Dispose_WhenLockEntryMissing_IsNoOp()
        {
            var factory = new DisposableLock();
            string name = "missing-entry-" + Guid.NewGuid().ToString("N");
            IDisposable handle = factory.AcquireLock(name, 50, 200, 1);

            object locksObj = typeof(DisposableLock)
                .GetField("Locks", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            ((System.Collections.IDictionary)locksObj).Remove(name);

            Assert.DoesNotThrow(() => handle.Dispose());
        }

        [Test]
        public void RedisCredentialsDefaultsProvider_IsMatch_ReturnsFalse()
        {
            var provider = new RedisCredentialsDefaultsProvider(
                () => new RedisCredentials("u", "p", DateTimeOffset.UtcNow.AddMinutes(5)));
            Assert.That(provider.IsMatch(new DnsEndPoint("localhost", 6379)), Is.False);
        }

        [Test]
        public async Task SqlPersistenceService_BatchPersist_Delete_ExecutesDeleteSql()
        {
            ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(
                new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            TestUtil.InitializeDB(sqlFacade);
            var service = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, new TestSqlGenerator());

            var entity = new TestEtagCacheable(
                Guid.NewGuid().ToString(), "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
            await service.CreateAsync(entity.Id, entity).ConfigureAwait(false);

            var actions = new List<PersistenceAction>
            {
                new PersistenceAction(entity.Id, WriteType.Delete)
            };
            IDictionary<PersistenceAction, int> result = await service
                .BatchPersistAsync(actions, new Dictionary<string, object>())
                .ConfigureAwait(false);

            Assert.That(result[actions[0]], Is.GreaterThanOrEqualTo(0));
            Assert.That(await service.GetByIdAsync(entity.Id).ConfigureAwait(false), Is.Null);
        }

        private static TransactionScope NewScope() =>
            new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

        private sealed class WriteBehindOnlyEntity : IWriteBehindEntity
        {
            public string Id { get; set; }

            public string Etag { get; set; }

            public bool IsDeleted { get; set; }
        }
    }
}
