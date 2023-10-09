// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Sql;
    using NUnit.Framework;

    [TestFixture]
    public class WriteThroughCacheTest : AbstractCacheTest
    {
        [SetUp]
        public void SetUp()
        {
            this.CacheType = CacheType.WriteThrough;
        }

        [Test]
        public async Task QueryTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);

            int startIndex = 5000;
            int numItems = 1000;
            int pageSize = 53;

            // Create numItems objects
            for (int index = startIndex; index < startIndex + numItems; index++)
            {
                await cache.GetAndPutIfAbsentAsync($"q{index}", new TestEtagCacheable($"q{index}", $"Description for {index}", GetNow(), GetNow(), Guid.NewGuid().ToString())).ConfigureAwait(false);
            }

            SqlSelect query = new SqlSelect(new Table("test_etag_cacheable", "v"))
                                    .WithWhere(new Filter().WithExpression(new Expression("v.id", "q" + startIndex).WithRelation(Relation.GreaterThanOrEqualTo)));

            if (this.CacheType == CacheType.WriteBehind)
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }

            // First Page
            int pageNum = 1;
            PagedResults<TestEtagCacheable> pagedResults = await cache.SearchByQueryAsync<TestEtagCacheable>(query, pageNum, pageSize, true).ConfigureAwait(false);
            Assert.AreEqual(pageSize, pagedResults.PagedList.Count);
            Assert.AreEqual(pageNum, pagedResults.PageNum);
            Assert.AreEqual(numItems, ((PagedResultsWithTotal<TestEtagCacheable>)pagedResults).TotalResults);
            Assert.AreEqual(1 + (numItems / pageSize), ((PagedResultsWithTotal<TestEtagCacheable>)pagedResults).TotalPages);

            // Random in-between page
            pageNum = 1 + (numItems / (2 * pageSize));
            pagedResults = await cache.SearchByQueryAsync<TestEtagCacheable>(query, pageNum, pageSize, true).ConfigureAwait(false);
            Assert.AreEqual(pageSize, pagedResults.PagedList.Count);
            Assert.AreEqual(pageNum, pagedResults.PageNum);

            // Last page
            pageNum = 1 + (numItems / pageSize);
            pagedResults = await cache.SearchByQueryAsync<TestEtagCacheable>(query, pageNum, pageSize, true).ConfigureAwait(false);
            Assert.AreEqual(numItems - (pageSize * (pageNum - 1)), pagedResults.PagedList.Count);
            Assert.AreEqual(pageNum, pagedResults.PageNum);

            // Wrong page
            pageNum = 2 + (numItems / pageSize);
            pagedResults = await cache.SearchByQueryAsync<TestEtagCacheable>(query, pageNum, pageSize, true).ConfigureAwait(false);
            Assert.AreEqual(0, pagedResults.PagedList.Count);
            Assert.AreEqual(pageNum, pagedResults.PageNum);
        }
    }
}
