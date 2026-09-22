// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Configuration for a Microsoft Garnet-backed cache. Garnet speaks a Redis RESP command
    /// subset, so this reuses the Redis provider. Defaults to <see cref="RedisDistributedLockKind.Token"/>
    /// because RedLock unlock requires Lua scripts that Garnet may not support.
    /// </summary>
    public class GarnetProviderConfiguration : RedisProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GarnetProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">Garnet server host:port.</param>
        /// <param name="password">Server password (may be empty).</param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="useSSL">Whether to use SSL/TLS for the connection.</param>
        /// <param name="abortConnection">Whether to abort on connect failure.</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        /// <param name="nameIndex">Logical database index (default 0).</param>
        public GarnetProviderConfiguration(
            string endpoint,
            string password,
            string cacheName,
            bool useSSL = false,
            bool abortConnection = false,
            long timeToLiveMillis = 3600000,
            int nameIndex = 0)
            : base(endpoint, password, cacheName, useSSL, abortConnection, timeToLiveMillis, nameIndex)
        {
            this.ProviderType = CacheProviderType.Garnet;
            this.DistributedLockKind = RedisDistributedLockKind.Token;
        }
    }
}
