// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    public interface ICacheProviderConfiguration
    {
        /// <summary>
        /// Gets cache provider type.
        /// </summary>
        CacheProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets the name of the cache.
        /// </summary>
        string CacheName { get; set; }

        /// <summary>
        /// Gets the time in milliseconds that objects will be retained withn the cache
        /// </summary>
        long TimeToLiveMillis { get; set; }
    }
}
