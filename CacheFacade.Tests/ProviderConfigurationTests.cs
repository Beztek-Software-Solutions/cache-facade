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
            Assert.That(cacheName,  Is.EqualTo(redisProviderConfiguration.CacheName));
            Assert.That(redisCacheEndpoint,  Is.EqualTo(redisProviderConfiguration.Endpoint));
            Assert.That(redisPassword,  Is.EqualTo(redisProviderConfiguration.Password));
            Assert.That(timeToLiveMillis,  Is.EqualTo(redisProviderConfiguration.TimeToLiveMillis));
        }

        [Test]
        public void CanInitializeLocalMemoryProviderConfiguration()
        {
            string cacheName = "dummn-hazelcastcachename";
            long timeToLiveMillis = 300000;
            LocalMemoryProviderConfiguration LocalMemoryProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, timeToLiveMillis);
            Assert.That(cacheName,  Is.EqualTo(LocalMemoryProviderConfiguration.CacheName));
            Assert.That(timeToLiveMillis,  Is.EqualTo(LocalMemoryProviderConfiguration.TimeToLiveMillis));
        }
    }
}
