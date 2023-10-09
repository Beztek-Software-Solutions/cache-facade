// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class EtagEntityUpdateHelperTests
    {
        private ICache cache;

        [SetUp]
        public void SetUp()
        {
            CacheConfiguration cacheConfiguration = new CacheConfiguration(new LocalMemoryProviderConfiguration("EtagEntityUpdateHelperTests", 300000), CacheType.NonPersistent);
            this.cache = CacheFactory.GetOrCreateCache(cacheConfiguration);
        }

        [Test]
        public async Task UpdateHappyPath()
        {
            // the paramter "false" would cause no concurrency exception when the function used by the helper is called
            await this.ParameterizedTest(false).ConfigureAwait(false);
            return;
        }

        [Test]
        public async Task UpdateAllStalePath()
        {
            // the paramter "true" would cause a concurrency exception every time the function used by the helper is called
            Assert.ThrowsAsync<ConcurrencyException>(async () => await this.ParameterizedTest(true).ConfigureAwait(false));
        }

        // Internal 

        private async Task ParameterizedTest(bool throwExceptionFlag)
        {
            DateTime createdDate = DateTime.Now;
            DateTime updatedDate = createdDate;
            string key = $"{throwExceptionFlag}-key";
            TestEtagCacheable oldResult = new TestEtagCacheable(key, "oldresult", createdDate, updatedDate, EtagUtil.GenerateEtag());
            await this.cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(oldResult.Id, oldResult).ConfigureAwait(false);
            object[] parameters = new object[] { throwExceptionFlag };
            TestEtagCacheable updated = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(this.cache, oldResult.Id, parameters, this.UpdateEtagCacheable).ConfigureAwait(false);
            Assert.AreEqual("newresult", updated.Value);
            Assert.AreNotEqual(oldResult.Etag, updated.Etag);
            return;
        }

        // Update function method

        private TestEtagCacheable UpdateEtagCacheable(TestEtagCacheable baseEntity, object[] parameters)
        {
            bool throwExceptionFlag = (bool)parameters[0];

            baseEntity.Value = "newresult";
            if (throwExceptionFlag)
            {
                // If we attempt to update with a different Etag, a concurrency exception should be thrown
                baseEntity.Etag = EtagUtil.GenerateEtag();
            }

            return baseEntity;
        }
    }
}
