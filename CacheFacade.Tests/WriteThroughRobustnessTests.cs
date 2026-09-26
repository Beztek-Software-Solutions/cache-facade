// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Sql;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Write-through-specific robustness: persistence is synchronous with the API call
    /// (no drain lag). Complements <see cref="WriteThroughCacheTest"/> / <see cref="AbstractCacheTest"/>.
    /// </summary>
    [TestFixture]
    public class WriteThroughRobustnessTests
    {
        [Test]
        public async Task Create_PersistsImmediately_AndSurvivesFlush()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("create-v1");
                await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);

                TestEtagCacheable persisted = await GetPersisted(cache, entity.Id).ConfigureAwait(false);
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted.Value, Is.EqualTo("create-v1"));
                Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.GreaterThan(0));

                await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(entity.Id), Is.Null);

                TestEtagCacheable reloaded = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.Value, Is.EqualTo("create-v1"));
                Assert.That(reloaded.Etag, Is.EqualTo(persisted.Etag));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Update_PersistsImmediately_FlushReloadsLatest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable original = NewEntity("v1");
                await cache.GetAndPutAsync(original.Id, original).ConfigureAwait(false);
                TestEtagCacheable afterCreate = await GetPersisted(cache, original.Id).ConfigureAwait(false);

                var updated = new TestEtagCacheable(
                    original.Id, "v2", original.CreatedDate, TestUtil.GetNow(), afterCreate.Etag);
                await cache.GetAndReplaceAsync(original.Id, updated).ConfigureAwait(false);

                TestEtagCacheable persisted = await GetPersisted(cache, original.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo("v2"));
                Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.GreaterThan(EtagUtil.ParseSequentialEtag(afterCreate.Etag)));

                await cache.FlushKeyAsync<TestEtagCacheable>(original.Id).ConfigureAwait(false);
                TestEtagCacheable reloaded = await cache.GetAsync<TestEtagCacheable>(original.Id).ConfigureAwait(false);
                Assert.That(reloaded.Value, Is.EqualTo("v2"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Delete_RemovesFromPersistenceImmediately_AndDoesNotResurrectOnGet()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("to-delete");
                await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
                Assert.That(await GetPersisted(cache, entity.Id).ConfigureAwait(false), Is.Not.Null);

                await cache.RemoveAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);

                Assert.That(await GetPersisted(cache, entity.Id).ConfigureAwait(false), Is.Null);
                Assert.That(await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Remove_MissingKey_IsIdempotent()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                string missingId = Guid.NewGuid().ToString("N");
                TestEtagCacheable removed = await cache.RemoveAsync<TestEtagCacheable>(missingId).ConfigureAwait(false);
                Assert.That(removed, Is.Null);
                Assert.That(await GetPersisted(cache, missingId).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task GetAndPutIfAbsent_WhenPresent_DoesNotOverwritePersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable original = NewEntity("keep-me");
                await cache.GetAndPutIfAbsentAsync(original.Id, original).ConfigureAwait(false);
                string etagAfterCreate = (await GetPersisted(cache, original.Id).ConfigureAwait(false)).Etag;

                var challenger = new TestEtagCacheable(
                    original.Id, "should-not-win", original.CreatedDate, TestUtil.GetNow(), EtagUtil.GenerateEtag());
                TestEtagCacheable returned = await cache.GetAndPutIfAbsentAsync(original.Id, challenger).ConfigureAwait(false);

                Assert.That(returned.Value, Is.EqualTo("keep-me"));
                TestEtagCacheable persisted = await GetPersisted(cache, original.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo("keep-me"));
                Assert.That(persisted.Etag, Is.EqualTo(etagAfterCreate));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task StaleEtag_Replace_Throws_AndLeavesPersistenceUnchanged()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable original = NewEntity("v1");
                await cache.GetAndPutAsync(original.Id, original).ConfigureAwait(false);
                TestEtagCacheable afterCreate = await GetPersisted(cache, original.Id).ConfigureAwait(false);

                var stale = new TestEtagCacheable(
                    original.Id, "stale-write", original.CreatedDate, TestUtil.GetNow(), "not-the-current-etag");

                Assert.ThrowsAsync<ConcurrencyException>(async () =>
                    await cache.GetAndReplaceAsync(original.Id, stale).ConfigureAwait(false));

                TestEtagCacheable persisted = await GetPersisted(cache, original.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo(afterCreate.Value));
                Assert.That(persisted.Etag, Is.EqualTo(afterCreate.Etag));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task EqualValueReplace_IsNoOp_DoesNotBumpEtagInPersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable original = NewEntity("same");
                await cache.GetAndPutAsync(original.Id, original).ConfigureAwait(false);
                TestEtagCacheable afterCreate = await cache.GetAsync<TestEtagCacheable>(original.Id).ConfigureAwait(false);

                // Same field values including etag → Equals true → early return without PersistReplacement.
                var identical = new TestEtagCacheable(
                    afterCreate.Id,
                    afterCreate.Value,
                    afterCreate.CreatedDate,
                    afterCreate.UpdatedDate,
                    afterCreate.Etag,
                    afterCreate.WriteBehindSequence,
                    afterCreate.IsDeleted);

                TestEtagCacheable returned = await cache.GetAndReplaceAsync(afterCreate.Id, identical).ConfigureAwait(false);
                Assert.That(returned, Is.EqualTo(identical));

                TestEtagCacheable persisted = await GetPersisted(cache, afterCreate.Id).ConfigureAwait(false);
                Assert.That(persisted.Etag, Is.EqualTo(afterCreate.Etag));
                Assert.That(persisted.Value, Is.EqualTo("same"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task CreateUpdateDeleteRecreate_EachStepHitsPersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                string id = Guid.NewGuid().ToString("N");
                var created = new TestEtagCacheable(id, "c1", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
                await cache.GetAndPutAsync(id, created).ConfigureAwait(false);
                Assert.That((await GetPersisted(cache, id).ConfigureAwait(false)).Value, Is.EqualTo("c1"));

                TestEtagCacheable current = await cache.GetAsync<TestEtagCacheable>(id).ConfigureAwait(false);
                var updated = new TestEtagCacheable(id, "c2", current.CreatedDate, TestUtil.GetNow(), current.Etag);
                await cache.GetAndPutAsync(id, updated).ConfigureAwait(false);
                Assert.That((await GetPersisted(cache, id).ConfigureAwait(false)).Value, Is.EqualTo("c2"));

                await cache.RemoveAsync<TestEtagCacheable>(id).ConfigureAwait(false);
                Assert.That(await GetPersisted(cache, id).ConfigureAwait(false), Is.Null);

                var recreated = new TestEtagCacheable(id, "c3", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
                await cache.GetAndPutAsync(id, recreated).ConfigureAwait(false);
                TestEtagCacheable persisted = await GetPersisted(cache, id).ConfigureAwait(false);
                Assert.That(persisted, Is.Not.Null);
                Assert.That(persisted.Value, Is.EqualTo("c3"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task RapidUpdates_LastValuePersists_NoDrainRequired()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("u0");
                await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);

                for (int i = 1; i <= 25; i++)
                {
                    TestEtagCacheable current = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                    var next = new TestEtagCacheable(
                        entity.Id, "u" + i, current.CreatedDate, TestUtil.GetNow(), current.Etag);
                    await cache.GetAndReplaceAsync(entity.Id, next).ConfigureAwait(false);
                }

                TestEtagCacheable persisted = await GetPersisted(cache, entity.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo("u25"));
                Assert.That(
                    (await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false)).Value,
                    Is.EqualTo("u25"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task MultiKey_UpdatesAreIndependent_InPersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable a = NewEntity("a");
                TestEtagCacheable b = NewEntity("b");
                await cache.GetAndPutAsync(a.Id, a).ConfigureAwait(false);
                await cache.GetAndPutAsync(b.Id, b).ConfigureAwait(false);

                TestEtagCacheable aCurrent = await cache.GetAsync<TestEtagCacheable>(a.Id).ConfigureAwait(false);
                await cache.GetAndReplaceAsync(
                    a.Id,
                    new TestEtagCacheable(a.Id, "a2", aCurrent.CreatedDate, TestUtil.GetNow(), aCurrent.Etag))
                    .ConfigureAwait(false);

                Assert.That((await GetPersisted(cache, a.Id).ConfigureAwait(false)).Value, Is.EqualTo("a2"));
                Assert.That((await GetPersisted(cache, b.Id).ConfigureAwait(false)).Value, Is.EqualTo("b"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Upsert_EscapesSpecialCharactersInPersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                string value = "line1\nline2\t\"quoted\" 'apos' \\ slash; DROP TABLE test_etag_cacheable;--";
                TestEtagCacheable entity = NewEntity(value);
                await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);

                TestEtagCacheable persisted = await GetPersisted(cache, entity.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo(value));

                await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                TestEtagCacheable reloaded = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                Assert.That(reloaded.Value, Is.EqualTo(value));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task SearchByQuery_SeesWriteThroughCreatesImmediately()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                string prefix = "wtq-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                for (int i = 0; i < 5; i++)
                {
                    var entity = new TestEtagCacheable(
                        prefix + i, "v" + i, TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                }

                SqlSelect query = new SqlSelect(new Table("test_etag_cacheable", "v"))
                    .WithWhere(new Filter().WithExpression(
                        new Expression("v.id", prefix).WithRelation(Relation.GreaterThanOrEqualTo)));

                PagedResults<TestEtagCacheable> page = await cache
                    .SearchByQueryAsync<TestEtagCacheable>(query, 1, 10, true)
                    .ConfigureAwait(false);

                Assert.That(page.PagedList.Count, Is.EqualTo(5));
                Assert.That(((PagedResultsWithTotal<TestEtagCacheable>)page).TotalResults, Is.EqualTo(5));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Create_PersistenceFailure_RollsBackCache_AndSurfacesIOException()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var persistence = new Mock<IPersistenceService>(MockBehavior.Strict);
            persistence.Setup(p => p.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((object)null);
            persistence.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ThrowsAsync(new InvalidOperationException("sql down"));

            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new LocalMemoryProviderConfiguration(cacheName, 300_000),
                    CacheType.WriteThrough,
                    persistence.Object));
            try
            {
                TestEtagCacheable entity = NewEntity("boom");
                IOException ex = Assert.ThrowsAsync<IOException>(async () =>
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false));
                Assert.That(ex!.InnerException, Is.InstanceOf<InvalidOperationException>());

                Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(entity.Id), Is.Null);
                Assert.That(await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task Update_PersistenceFailure_RollsBackCache_ToPreviousValue()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(
                new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            var realPersistence = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, new TestSqlGenerator());
            TestUtil.InitializeDB(sqlFacade);

            var persistence = new Mock<IPersistenceService>(MockBehavior.Strict);
            persistence.Setup(p => p.GetByIdAsync(It.IsAny<string>()))
                .Returns((string id) => realPersistence.GetByIdAsync(id));
            persistence.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<object>()))
                .Returns((string id, object value) => realPersistence.CreateAsync(id, value));
            persistence.Setup(p => p.UpdateAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ThrowsAsync(new InvalidOperationException("update failed"));

            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new LocalMemoryProviderConfiguration(cacheName, 300_000),
                    CacheType.WriteThrough,
                    persistence.Object));
            try
            {
                TestEtagCacheable entity = NewEntity("v1");
                await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
                TestEtagCacheable afterCreate = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);

                var updated = new TestEtagCacheable(
                    entity.Id, "v2", afterCreate.CreatedDate, TestUtil.GetNow(), afterCreate.Etag);

                Assert.ThrowsAsync<IOException>(async () =>
                    await cache.GetAndReplaceAsync(entity.Id, updated).ConfigureAwait(false));

                TestEtagCacheable inCache = cache.CacheProvider.Get<TestEtagCacheable>(entity.Id);
                Assert.That(inCache, Is.Not.Null);
                Assert.That(inCache.Value, Is.EqualTo("v1"));
                Assert.That(inCache.Etag, Is.EqualTo(afterCreate.Etag));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task FlushAll_ThenGet_ReloadsAllFromPersistence()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                var entities = new List<TestEtagCacheable>();
                for (int i = 0; i < 3; i++)
                {
                    TestEtagCacheable entity = NewEntity("f" + i);
                    entities.Add(entity);
                    await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
                }

                await cache.FlushAsync<TestEtagCacheable>().ConfigureAwait(false);
                foreach (TestEtagCacheable entity in entities)
                {
                    Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(entity.Id), Is.Null);
                }

                foreach (TestEtagCacheable entity in entities)
                {
                    TestEtagCacheable reloaded = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                    Assert.That(reloaded, Is.Not.Null);
                    Assert.That(reloaded.Value, Is.EqualTo(entity.Value));
                }
            }
            finally
            {
                cache.Dispose();
            }
        }

        private static async Task<TestEtagCacheable> GetPersisted(Cache cache, string id)
        {
            return (TestEtagCacheable)await cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false);
        }

        private static TestEtagCacheable NewEntity(string value)
        {
            return new TestEtagCacheable(
                Guid.NewGuid().ToString("N"),
                value,
                TestUtil.GetNow(),
                TestUtil.GetNow(),
                EtagUtil.GenerateEtag());
        }
    }
}
