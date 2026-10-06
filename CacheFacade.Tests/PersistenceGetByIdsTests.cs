// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Sql;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Covers <see cref="IPersistenceService.GetByIdsAsync"/> default (1+N loop) and
    /// <see cref="SqlPersistenceService{T}.GetByIdsAsync"/> edge cases, plus Search hydrate gaps.
    /// </summary>
    [TestFixture]
    public class PersistenceGetByIdsTests
    {
        [Test]
        public async Task SqlGetByIdsAsync_SkipsNullRowsFromSqlFacade()
        {
            var live = new TestEtagCacheable(
                "live", "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
            var sqlFacade = new Mock<ISqlFacade>(MockBehavior.Strict);
            sqlFacade.Setup(s => s.GetResults<TestEtagCacheable>(It.IsAny<SqlSelect>()))
                .Returns(new List<TestEtagCacheable> { null, live });

            var persistence = new SqlPersistenceService<TestEtagCacheable>(
                sqlFacade.Object, new TestSqlGenerator());

            IDictionary<string, object> map = await persistence.GetByIdsAsync(
                new List<string> { "live" }).ConfigureAwait(false);

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(((TestEtagCacheable)map["live"]).Id, Is.EqualTo("live"));
        }

        [Test]
        public async Task DefaultGetByIdsAsync_LoopsGetById_AndSkipsNullEmptyAndDuplicates()
        {
            var store = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["a"] = new TestEtagCacheable("a", "va", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag()),
                ["b"] = new TestEtagCacheable("b", "vb", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag()),
            };
            int getByIdCalls = 0;
            IPersistenceService persistence = new DefaultOnlyPersistence(
                id => {
                    getByIdCalls++;
                    return store.TryGetValue(id, out object value) ? value : null;
                });

            IDictionary<string, object> map = await persistence.GetByIdsAsync(
                new List<string> { "a", null, "a", "", "b", "missing", "b" }).ConfigureAwait(false);

            Assert.That(map.Keys.OrderBy(k => k).ToList(), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(getByIdCalls, Is.EqualTo(3), "distinct non-empty ids only: a, b, missing");
            Assert.That(map.ContainsKey("missing"), Is.False);
        }

        [Test]
        public async Task DefaultGetByIdsAsync_NullOrEmptyList_ReturnsEmptyMap()
        {
            IPersistenceService persistence = new DefaultOnlyPersistence(_ => null);

            Assert.That(await persistence.GetByIdsAsync(null).ConfigureAwait(false), Is.Empty);
            Assert.That(await persistence.GetByIdsAsync(Array.Empty<string>()).ConfigureAwait(false), Is.Empty);
            Assert.That(
                await persistence.GetByIdsAsync(new List<string> { null, "" }).ConfigureAwait(false),
                Is.Empty);
        }

        [Test]
        public async Task SqlGetByIdsAsync_NullEmptyAndBlankOnly_ReturnsEmptyWithoutQuery()
        {
            ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(
                new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            var countingGenerator = new CountingSqlGenerator();
            var persistence = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, countingGenerator);
            TestUtil.InitializeDB(sqlFacade);

            Assert.That(await persistence.GetByIdsAsync(null).ConfigureAwait(false), Is.Empty);
            Assert.That(await persistence.GetByIdsAsync(Array.Empty<string>()).ConfigureAwait(false), Is.Empty);
            Assert.That(
                await persistence.GetByIdsAsync(new List<string> { null, "", null }).ConfigureAwait(false),
                Is.Empty);
            Assert.That(countingGenerator.SelectByIdsCalls, Is.EqualTo(0));
        }

        [Test]
        public async Task SqlGetByIdsAsync_DedupesIds_AndOmitsSoftDeletedAndNullGetId()
        {
            ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(
                new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            var generator = new SelectiveIdGenerator(nullId: "no-key");
            var persistence = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, generator);
            TestUtil.InitializeDB(sqlFacade);

            var live = new TestEtagCacheable(
                "live", "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
            var deleted = new TestEtagCacheable(
                "gone", "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag(), isDeleted: true);
            var noKey = new TestEtagCacheable(
                "no-key", "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());

            await persistence.CreateAsync(live.Id, live).ConfigureAwait(false);
            await persistence.CreateAsync(deleted.Id, deleted).ConfigureAwait(false);
            await persistence.CreateAsync(noKey.Id, noKey).ConfigureAwait(false);

            IDictionary<string, object> map = await persistence.GetByIdsAsync(
                new List<string> { "live", "live", "gone", "no-key", "missing" }).ConfigureAwait(false);

            Assert.That(map.Count, Is.EqualTo(1));
            Assert.That(map.ContainsKey("live"), Is.True);
            Assert.That(map.ContainsKey("gone"), Is.False, "soft-deleted omitted");
            Assert.That(map.ContainsKey("no-key"), Is.False, "GetId null omitted");
            Assert.That(generator.SelectByIdsCalls, Is.EqualTo(1));
            Assert.That(generator.LastSelectIds, Is.EqualTo(new[] { "live", "gone", "no-key", "missing" }));
        }

        [Test]
        public async Task SearchByQuery_SkipsMissesAbsentFromGetByIdsMap()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var persistence = new Mock<IPersistenceService>(MockBehavior.Strict);
            persistence.Setup(p => p.SearchIdsByQueryAsync(
                    It.IsAny<SqlSelect>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new PagedResults<string>(1, 10, new List<string> { "hit-miss", "only-id" }));
            persistence.Setup(p => p.GetByIdsAsync(It.IsAny<IReadOnlyList<string>>()))
                .ReturnsAsync(new Dictionary<string, object>(StringComparer.Ordinal) {
                    // only one miss hydrated; only-id absent → null slot in page
                    ["hit-miss"] = new TestEtagCacheable(
                        "hit-miss", "v", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag()),
                });

            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new LocalMemoryProviderConfiguration(cacheName, 300_000),
                    CacheType.WriteThrough,
                    persistence.Object));
            try
            {
                PagedResults<TestEtagCacheable> page = await cache
                    .SearchByQueryAsync<TestEtagCacheable>(
                        new SqlSelect("unused"), 1, 10, false)
                    .ConfigureAwait(false);

                Assert.That(page.PagedList.Count, Is.EqualTo(2));
                Assert.That(page.PagedList[0].Id, Is.EqualTo("hit-miss"));
                Assert.That(page.PagedList[1], Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public void SearchByQuery_NonPersistent_ThrowsNotSupported()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(
                    new LocalMemoryProviderConfiguration(cacheName, 300_000),
                    CacheType.NonPersistent));
            try
            {
                Assert.ThrowsAsync<NotSupportedException>(async () =>
                    await cache.SearchByQueryAsync<TestEtagCacheable>(
                        new SqlSelect("x"), 1, 10).ConfigureAwait(false));
            }
            finally
            {
                cache.Dispose();
            }
        }

        /// <summary>
        /// Implements <see cref="IPersistenceService"/> without overriding <see cref="IPersistenceService.GetByIdsAsync"/>
        /// so the default interface method is used.
        /// </summary>
        private sealed class DefaultOnlyPersistence : IPersistenceService
        {
            private readonly Func<string, object> getById;

            public DefaultOnlyPersistence(Func<string, object> getById)
            {
                this.getById = getById;
            }

            public Task<object> GetByIdAsync(string id) =>
                Task.FromResult(this.getById(id));

            public Task<int> CreateAsync(string id, object value) =>
                throw new NotSupportedException();

            public Task<int> UpdateAsync(string id, object value) =>
                throw new NotSupportedException();

            public Task<int> DeleteAsync(string id) =>
                throw new NotSupportedException();

            public Task<IDictionary<PersistenceAction, int>> BatchPersistAsync(
                List<PersistenceAction> persistenceActions,
                Dictionary<string, object> actionableItems) =>
                throw new NotSupportedException();

            public Task<PagedResults<string>> SearchIdsByQueryAsync(
                SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false) =>
                throw new NotSupportedException();
        }

        private sealed class CountingSqlGenerator : TestSqlGenerator
        {
            public int SelectByIdsCalls { get; private set; }

            public override SqlSelect GetSqlSelectByIds(IReadOnlyList<string> ids)
            {
                this.SelectByIdsCalls++;
                return base.GetSqlSelectByIds(ids);
            }
        }

        private sealed class SelectiveIdGenerator : TestSqlGenerator
        {
            private readonly string nullId;

            public SelectiveIdGenerator(string nullId)
            {
                this.nullId = nullId;
            }

            public int SelectByIdsCalls { get; private set; }

            public IReadOnlyList<string> LastSelectIds { get; private set; }

            public override SqlSelect GetSqlSelectByIds(IReadOnlyList<string> ids)
            {
                this.SelectByIdsCalls++;
                this.LastSelectIds = ids.ToList();
                return base.GetSqlSelectByIds(ids);
            }

            public override string GetId(TestEtagCacheable entity)
            {
                if (entity != null && string.Equals(entity.Id, this.nullId, StringComparison.Ordinal))
                {
                    return null;
                }

                return base.GetId(entity);
            }
        }
    }
}
