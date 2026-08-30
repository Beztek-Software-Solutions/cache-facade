// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Provider-specific settings shared by all cache backends (name, type, TTL).
    /// </summary>
    public interface ICacheProviderConfiguration
    {
        /// <summary>
        /// Gets or sets the cache provider type.
        /// </summary>
        CacheProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets or sets the logical name of the cache (also the <see cref="CacheFactory"/> registry key).
        /// </summary>
        string CacheName { get; set; }

        /// <summary>
        /// Gets or sets the time in milliseconds that objects are retained in the cache.
        /// </summary>
        long TimeToLiveMillis { get; set; }
    }
}
