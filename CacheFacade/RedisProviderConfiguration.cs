// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for Redis based cache.
    /// </summary>
    public class RedisProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProviderConfiguration"/> class using Redis server endpoint and password.
        /// </summary>
        /// <param name="endpoint">Redis server endpoint.</param>
        /// <param name="password">Redis server password.</param>
        /// <param name="nameIndex">Partition index of cache (optional, defaults to 0). (Note: the lockCache uses index 1)</param>
        public RedisProviderConfiguration(string endpoint, string password, string cacheName, long timeToLiveMillis, int nameIndex = 0)
        {
            this.CacheName = cacheName;
            this.Endpoint = endpoint;
            this.Password = password;
            this.NameIndex = nameIndex;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.Redis;
        }

        /// <summary>
        /// Redis server endpoint.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Redis server password.
        /// </summary>
        public string Password { get; }

        public CacheProviderType ProviderType { get; set; }

        /// <summary>
        /// Gets index for this cache partition in Redis.
        /// </summary>
        public int NameIndex { get; set; }

        /// <summary>
        /// Gets Redist map name.
        /// </summary>
        public string CacheName { get; set; }

        public long TimeToLiveMillis { get; set; }
    }
}
