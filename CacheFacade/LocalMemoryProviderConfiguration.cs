// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for Hazelcast based cache.
    /// </summary>
    public class LocalMemoryProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMemoryProviderConfiguration"/> class using MemoryCache.
        /// </summary>
        /// <param name="cacheName"></param>
        /// <param name="timeToLiveMillis"></param>
        public LocalMemoryProviderConfiguration(string cacheName, long timeToLiveMillis)
        {
            this.CacheName = cacheName;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.LocalMemory;
        }

        public CacheProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets Hazelcast map name.
        /// </summary>
        public string CacheName { get; set; }

        public long TimeToLiveMillis { get; set; }
    }
}
