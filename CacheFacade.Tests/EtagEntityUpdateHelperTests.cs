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
            EtagEntityUpdateHelper.Configure(null);
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
        public void UpdateAllStalePath()
        {
            // the paramter "true" would cause a concurrency exception every time the function used by the helper is called
            Assert.ThrowsAsync<ConcurrencyException>(async () => await this.ParameterizedTest(true));
        }

        [Test]
        public void RetryPolicy_UsesFiveRetriesWithExponentialBackoff()
        {
            EtagEntityUpdateHelper.Configure(new EtagEntityUpdateOptions
            {
                MaxRetryCount = 5,
                InitialRetryDelayMillis = 5,
                MaxRetryDelayMillis = 200,
                UseExponentialBackoff = true,
            });

            Assert.That(EtagEntityUpdateHelper.MaxRetryCount, Is.EqualTo(5));
            Assert.That(EtagEntityUpdateHelper.InitialRetryDelayMillis, Is.EqualTo(5));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(0), Is.EqualTo(5));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(1), Is.EqualTo(10));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(2), Is.EqualTo(20));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(3), Is.EqualTo(40));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(4), Is.EqualTo(80));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(5), Is.EqualTo(160));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(10), Is.EqualTo(EtagEntityUpdateHelper.MaxRetryDelayMillis));
        }

        [Test]
        public void Configure_AndPerCallOptions_AreHonored()
        {
            EtagEntityUpdateHelper.Configure(new EtagEntityUpdateOptions
            {
                MaxRetryCount = 2,
                InitialRetryDelayMillis = 3,
                MaxRetryDelayMillis = 50,
                UseExponentialBackoff = false,
            });

            Assert.That(EtagEntityUpdateHelper.MaxRetryCount, Is.EqualTo(2));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(0), Is.EqualTo(3));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(4), Is.EqualTo(3));

            var perCall = new EtagEntityUpdateOptions
            {
                MaxRetryCount = 1,
                InitialRetryDelayMillis = 7,
                MaxRetryDelayMillis = 100,
                UseExponentialBackoff = true,
            };
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(0, perCall), Is.EqualTo(7));
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(1, perCall), Is.EqualTo(14));

            // Reset for other tests.
            EtagEntityUpdateHelper.Configure(null);
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
            Assert.That("newresult", Is.EqualTo(updated.Value));
            Assert.That(oldResult.Etag, Is.Not.EqualTo(updated.Etag));
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
