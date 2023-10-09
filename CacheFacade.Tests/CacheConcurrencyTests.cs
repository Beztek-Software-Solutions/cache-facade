// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class CacheConcurrencyTests
    {
        private ICache cache;

        [SetUp]
        public void SetUp()
        {
            CacheConfiguration cacheConfiguration = new CacheConfiguration(new LocalMemoryProviderConfiguration("CacheConcurrencyTests", 300000), CacheType.NonPersistent);
            this.cache = CacheFactory.GetOrCreateCache(cacheConfiguration);
        }

        [Test]
        public async Task GetAndReplaceKeyPresentSameEtagAsyncTest()
        {
            DateTime createdDate = DateTime.Now;
            DateTime updatedDate = createdDate;
            TestEtagCacheable origResult = new TestEtagCacheable("GetAndReplaceKeyPresentSameEtagAsyncTest-key", "result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            await this.cache.GetAndPutIfAbsentAsync(origResult.Id, origResult).ConfigureAwait(false);
            TestEtagCacheable oldResult = await this.cache.GetAsync<TestEtagCacheable>(origResult.Id).ConfigureAwait(false);
            TestEtagCacheable toSaveResult = new TestEtagCacheable(oldResult.Id, "new-result", createdDate, updatedDate, oldResult.Etag);
            TestEtagCacheable operationResult = await this.cache.GetAndReplaceAsync<TestEtagCacheable>(toSaveResult.Id, toSaveResult).ConfigureAwait(false);
            Assert.AreEqual(oldResult, operationResult);
            TestEtagCacheable newResult = await this.cache.GetAsync<TestEtagCacheable>(toSaveResult.Id).ConfigureAwait(false);
            Assert.AreEqual(toSaveResult.Value, newResult.Value);
            Assert.AreNotEqual(oldResult.Etag, newResult.Etag);
        }

        [Test]
        public async Task GetAndReplaceKeyPresentDiffEtagAsyncTest()
        {
            DateTime createdDate = DateTime.Now;
            DateTime updatedDate = createdDate;
            TestEtagCacheable oldResult = new TestEtagCacheable("GetAndReplaceKeyPresentDiffEtagAsyncTest-key", "result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            TestEtagCacheable newResult = new TestEtagCacheable(oldResult.Id, "new-result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            await this.cache.GetAndPutIfAbsentAsync(oldResult.Id, oldResult).ConfigureAwait(false);
            Assert.ThrowsAsync<ConcurrencyException>(async () => await this.cache.GetAndReplaceAsync<TestEtagCacheable>(newResult.Id, newResult).ConfigureAwait(false));
        }

        [Test]
        public async Task GetAndPutAsyncKeyPresentSameEtagTest()
        {
            DateTime createdDate = DateTime.Now;
            DateTime updatedDate = createdDate;
            TestEtagCacheable origResult = new TestEtagCacheable("GetAndPutAsyncKeyPresentSameEtagTest-key", "old-result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            await this.cache.GetAndPutIfAbsentAsync(origResult.Id, origResult).ConfigureAwait(false);
            TestEtagCacheable oldResult = await this.cache.GetAsync<TestEtagCacheable>(origResult.Id).ConfigureAwait(false);
            TestEtagCacheable toSaveResult = new TestEtagCacheable(oldResult.Id, "new-result", createdDate, updatedDate, oldResult.Etag);
            TestEtagCacheable operationResult = await this.cache.GetAndPutAsync<TestEtagCacheable>(toSaveResult.Id, toSaveResult).ConfigureAwait(false);
            Assert.AreEqual(oldResult, operationResult);
            TestEtagCacheable newResult = await this.cache.GetAsync<TestEtagCacheable>(toSaveResult.Id).ConfigureAwait(false);
            Assert.AreEqual(toSaveResult.Value, newResult.Value);
            Assert.AreNotEqual(oldResult.Etag, newResult.Etag);
        }

        [Test]
        public async Task GetAndPutAsyncKeyPresentDiffEtagTest()
        {
            DateTime createdDate = DateTime.Now;
            DateTime updatedDate = createdDate;
            TestEtagCacheable oldResult = new TestEtagCacheable("GetAndPutAsyncKeyPresentDiffEtagTest-key", "old-result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            await this.cache.GetAndPutIfAbsentAsync(oldResult.Id, oldResult).ConfigureAwait(false);
            TestEtagCacheable newResult = new TestEtagCacheable(oldResult.Id, "new-result", createdDate, updatedDate, EtagUtil.GenerateEtag());
            Assert.ThrowsAsync<ConcurrencyException>(async () => await this.cache.GetAndPutAsync<TestEtagCacheable>(newResult.Id, newResult).ConfigureAwait(false));
        }
    }
}
