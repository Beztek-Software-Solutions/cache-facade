// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for a Memcached-backed cache provider.
    /// </summary>
    public class MemcachedProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemcachedProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">Memcached server host:port (e.g. <c>127.0.0.1:11211</c>).</param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key and key prefix.</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        public MemcachedProviderConfiguration(string endpoint, string cacheName, long timeToLiveMillis = 3600000)
        {
            this.Endpoint = endpoint;
            this.CacheName = cacheName;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.Memcached;
        }

        /// <summary>Memcached server endpoint (host:port).</summary>
        public string Endpoint { get; }

        /// <inheritdoc />
        public CacheProviderType ProviderType { get; set; }

        /// <inheritdoc />
        public string CacheName { get; set; }

        /// <inheritdoc />
        public long TimeToLiveMillis { get; set; }
    }
}
