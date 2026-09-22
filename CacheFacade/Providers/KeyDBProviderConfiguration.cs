// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Configuration for a KeyDB-backed cache. KeyDB is a multi-threaded Redis fork and
    /// speaks the Redis protocol, so this reuses the Redis provider and RedLock via the same connection settings.
    /// </summary>
    public class KeyDBProviderConfiguration : RedisProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyDBProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">KeyDB server host:port.</param>
        /// <param name="password">Server password (may be empty).</param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="useSSL">Whether to use SSL/TLS for the connection.</param>
        /// <param name="abortConnection">Whether to abort on connect failure.</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        /// <param name="nameIndex">Logical database index (default 0).</param>
        public KeyDBProviderConfiguration(
            string endpoint,
            string password,
            string cacheName,
            bool useSSL = false,
            bool abortConnection = false,
            long timeToLiveMillis = 3600000,
            int nameIndex = 0)
            : base(endpoint, password, cacheName, useSSL, abortConnection, timeToLiveMillis, nameIndex)
        {
            this.ProviderType = CacheProviderType.KeyDB;
        }
    }
}
