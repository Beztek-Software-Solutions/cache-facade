// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class ProviderConfigurationTests
    {
        [Test]
        public void CanInitializeRedisProviderConfiguration()
        {
            string cacheName = "dummy-name";
            string redisCacheEndpoint = "dummy-endpoint";
            string redisPassword = "dummy-password";
            long timeToLiveMillis = 300000;
            RedisProviderConfiguration redisProviderConfiguration = new RedisProviderConfiguration(redisCacheEndpoint, redisPassword, cacheName, true, false, timeToLiveMillis);
            Assert.AreEqual(cacheName, redisProviderConfiguration.CacheName);
            Assert.AreEqual(redisCacheEndpoint, redisProviderConfiguration.Endpoint);
            Assert.AreEqual(redisPassword, redisProviderConfiguration.Password);
            Assert.AreEqual(timeToLiveMillis, redisProviderConfiguration.TimeToLiveMillis);
        }

        [Test]
        public void CanInitializeLocalMemoryProviderConfiguration()
        {
            string cacheName = "dummn-hazelcastcachename";
            long timeToLiveMillis = 300000;
            LocalMemoryProviderConfiguration LocalMemoryProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, timeToLiveMillis);
            Assert.AreEqual(cacheName, LocalMemoryProviderConfiguration.CacheName);
            Assert.AreEqual(timeToLiveMillis, LocalMemoryProviderConfiguration.TimeToLiveMillis);
        }
    }
}
