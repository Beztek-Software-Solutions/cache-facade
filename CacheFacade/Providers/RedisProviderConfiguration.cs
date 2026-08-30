// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for a Redis-backed cache provider.
    /// </summary>
    public class RedisProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">Redis server host:port.</param>
        /// <param name="password">Redis server password (may be empty).</param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="useSSL">Whether to use SSL/TLS for the Redis connection.</param>
        /// <param name="abortConnection">Whether to abort on connect failure (StackExchange.Redis <c>AbortOnConnectFail</c>).</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        /// <param name="nameIndex">Redis database index (default 0). The internal lock cache uses index 1 when applicable.</param>
        public RedisProviderConfiguration(string endpoint, string password, string cacheName, bool useSSL = true, bool abortConnection = false, long timeToLiveMillis = 3600000, int nameIndex = 0)
        {
            this.CacheName = cacheName;
            this.Endpoint = endpoint;
            this.Password = password;
            this.UseSSL = useSSL;
            this.AbortConnection = abortConnection;

            this.NameIndex = nameIndex;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.Redis;
        }

        /// <summary>Redis server endpoint (host:port).</summary>
        public string Endpoint { get; }

        /// <summary>Whether SSL/TLS is enabled for the connection.</summary>
        public bool UseSSL { get; }

        /// <summary>Whether connection attempts abort on failure.</summary>
        public bool AbortConnection { get; }

        /// <summary>Redis server password.</summary>
        public string Password { get; }

        /// <inheritdoc />
        public CacheProviderType ProviderType { get; set; }

        /// <summary>Redis database (logical DB) index for this cache partition.</summary>
        public int NameIndex { get; set; }

        /// <inheritdoc />
        public string CacheName { get; set; }

        /// <inheritdoc />
        public long TimeToLiveMillis { get; set; }

        /// <summary>
        /// A comma-separated list of name=value pairs for the underlying Redis client.
        /// Explicit properties on this class override overlapping values in this string
        /// (for example <see cref="UseSSL"/> overrides <c>ssl=true</c>).
        /// </summary>
        public string Options { get; set; }
    }
}
