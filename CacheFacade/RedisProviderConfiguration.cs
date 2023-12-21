// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for Redis based cache.
    /// </summary>
    public class RedisProviderConfiguration : ICacheProviderConfiguration
    {
        public enum LockType
        {
            RedisLock,
            DisposableLockWithRedis,
            DisposableLockLocalMemory
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProviderConfiguration"/> class using Redis server endpoint and password.
        /// </summary>
        /// <param name="endpoint">Redis server endpoint, along with the port</param>
        /// <param name="useSSL">Flags whether to use SSL or not</param>
        /// <param name="abortConnection">Flags whether to abort connections or not</param>
        /// <param name="password">Redis server password.</param>
        /// <param name="nameIndex">Partition index of cache (optional, defaults to 0). (Note: the lockCache uses index 1)</param>
        public RedisProviderConfiguration(string endpoint, string password, string cacheName, bool useSSL=true, bool abortConnection=false, long timeToLiveMillis=3600000, int nameIndex = 0, LockType lockAlgorithm = LockType.RedisLock)
        {
            this.CacheName = cacheName;
            this.Endpoint = endpoint;
            this.Password = password;
            this.UseSSL = useSSL;
            this.AbortConnection = abortConnection;

            this.NameIndex = nameIndex;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.LockAlgorithm = lockAlgorithm;
            this.ProviderType = CacheProviderType.Redis;
        }

        /// <summary>
        /// Redis server endpoint.
        /// </summary>
        public string Endpoint { get; }

        public bool UseSSL { get; }

        public bool AbortConnection { get; }

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

        public LockType LockAlgorithm { get; set; } = LockType.RedisLock;
    }
}
