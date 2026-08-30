// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for an in-process local memory cache
    /// (<c>System.Runtime.Caching.MemoryCache</c>).
    /// </summary>
    public class LocalMemoryProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMemoryProviderConfiguration"/> class.
        /// </summary>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="timeToLiveMillis">Absolute expiration TTL for entries in milliseconds.</param>
        public LocalMemoryProviderConfiguration(string cacheName, long timeToLiveMillis)
        {
            this.CacheName = cacheName;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.LocalMemory;
        }

        /// <inheritdoc />
        public CacheProviderType ProviderType { get; set; }

        /// <inheritdoc />
        public string CacheName { get; set; }

        /// <inheritdoc />
        public long TimeToLiveMillis { get; set; }
    }
}
