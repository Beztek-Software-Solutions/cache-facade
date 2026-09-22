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
            Assert.That(cacheName, Is.EqualTo(redisProviderConfiguration.CacheName));
            Assert.That(redisCacheEndpoint, Is.EqualTo(redisProviderConfiguration.Endpoint));
            Assert.That(redisPassword, Is.EqualTo(redisProviderConfiguration.Password));
            Assert.That(timeToLiveMillis, Is.EqualTo(redisProviderConfiguration.TimeToLiveMillis));
        }

        [Test]
        public void CanInitializeLocalMemoryProviderConfiguration()
        {
            string cacheName = "dummn-hazelcastcachename";
            long timeToLiveMillis = 300000;
            LocalMemoryProviderConfiguration LocalMemoryProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, timeToLiveMillis);
            Assert.That(cacheName, Is.EqualTo(LocalMemoryProviderConfiguration.CacheName));
            Assert.That(timeToLiveMillis, Is.EqualTo(LocalMemoryProviderConfiguration.TimeToLiveMillis));
        }

        [Test]
        public void CanInitializeDragonflyProviderConfiguration()
        {
            var config = new DragonflyProviderConfiguration("127.0.0.1:6379", "", "orders", useSSL: false, timeToLiveMillis: 300000);
            Assert.That(config.ProviderType, Is.EqualTo(CacheProviderType.Dragonfly));
            Assert.That(config.Endpoint, Is.EqualTo("127.0.0.1:6379"));
            Assert.That(config.CacheName, Is.EqualTo("orders"));
            Assert.That(config, Is.InstanceOf<RedisProviderConfiguration>());
        }

        [Test]
        public void CanInitializeKeyDBProviderConfiguration()
        {
            var config = new KeyDBProviderConfiguration("127.0.0.1:6379", "", "orders", useSSL: false, timeToLiveMillis: 300000);
            Assert.That(config.ProviderType, Is.EqualTo(CacheProviderType.KeyDB));
            Assert.That(config.Endpoint, Is.EqualTo("127.0.0.1:6379"));
            Assert.That(config.CacheName, Is.EqualTo("orders"));
            Assert.That(config, Is.InstanceOf<RedisProviderConfiguration>());
        }

        [Test]
        public void CanInitializeGarnetProviderConfiguration()
        {
            var config = new GarnetProviderConfiguration("127.0.0.1:6379", "", "orders", useSSL: false, timeToLiveMillis: 300000);
            Assert.That(config.ProviderType, Is.EqualTo(CacheProviderType.Garnet));
            Assert.That(config.Endpoint, Is.EqualTo("127.0.0.1:6379"));
            Assert.That(config.CacheName, Is.EqualTo("orders"));
            Assert.That(config, Is.InstanceOf<RedisProviderConfiguration>());
            Assert.That(config.DistributedLockKind, Is.EqualTo(RedisDistributedLockKind.Token));
        }

        [Test]
        public void RedisProviderConfiguration_DefaultsToRedLock()
        {
            var config = new RedisProviderConfiguration("127.0.0.1:6379", "", "orders");
            Assert.That(config.DistributedLockKind, Is.EqualTo(RedisDistributedLockKind.RedLock));
        }

        [Test]
        public void CanInitializeMemcachedProviderConfiguration()
        {
            var config = new MemcachedProviderConfiguration("127.0.0.1:11211", "orders", 300000);
            Assert.That(config.ProviderType, Is.EqualTo(CacheProviderType.Memcached));
            Assert.That(config.Endpoint, Is.EqualTo("127.0.0.1:11211"));
            Assert.That(config.CacheName, Is.EqualTo("orders"));
            Assert.That(config.TimeToLiveMillis, Is.EqualTo(300000));
        }

        [Test]
        public void CanInitializeHazelcastProviderConfiguration()
        {
            var config = new HazelcastProviderConfiguration("dev", "127.0.0.1:5701", "orders", 300000);
            Assert.That(config.ProviderType, Is.EqualTo(CacheProviderType.Hazelcast));
            Assert.That(config.ClusterName, Is.EqualTo("dev"));
            Assert.That(config.Address, Is.EqualTo("127.0.0.1:5701"));
            Assert.That(config.CacheName, Is.EqualTo("orders"));
        }

    }
}
