// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;

    [System.Serializable]
    internal class TestCacheProviderConfiguration : ICacheProviderConfiguration
    {
        internal TestCacheProviderConfiguration(ICacheProvider cacheProvider, string cacheName, long timeToLiveMillis)
        {
            this.CacheProvider = cacheProvider;
            this.CacheName = cacheName;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.LocalMemory;
        }

        /// <summary>
        /// Gets Hazelcast map name.
        /// </summary>
        public string CacheName { get; set; }

        public long TimeToLiveMillis { get; set; }

        public CacheProviderType ProviderType { get; set; }

        public ICacheProvider CacheProvider { get; set; }
    }
}
