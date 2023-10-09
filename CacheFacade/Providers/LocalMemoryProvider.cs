// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.Linq;
    using System.Runtime.Caching;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides implementation for a Hazelcast based cache.
    /// </summary>
    internal class LocalMemoryProvider : ICacheProvider
    {
        private readonly System.Runtime.Caching.MemoryCache localMemoryCache;
        private readonly CacheItemPolicy cacheItemPolicy;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalMemoryProvider"/> class using MemoeryCache.
        /// </summary>
        /// <param name="localMemoryCacheConfiguration">LocalMemeory cache configuration object.</param>
        public LocalMemoryProvider(LocalMemoryProviderConfiguration localMemoryCacheConfiguration)
        {
            this.localMemoryCache = new System.Runtime.Caching.MemoryCache(localMemoryCacheConfiguration.CacheName);
            this.cacheItemPolicy = new CacheItemPolicy();
            this.cacheItemPolicy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(localMemoryCacheConfiguration.TimeToLiveMillis);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            byte[] result = (byte[])this.localMemoryCache.Get(key);
            return Deserialize<T>(result);
        }

        public async Task PutAsync<T>(string key, T value)
        {
            this.localMemoryCache.Set(key, Serialize(value), this.cacheItemPolicy);
        }

        public async Task<T> RemoveAsync<T>(string key)
        {
            return Deserialize<T>((byte[])this.localMemoryCache.Remove(key));
        }

        public async Task<bool> ClearAsync()
        {
            var allKeys = this.localMemoryCache.Select(o => o.Key);
            Parallel.ForEach(allKeys, key => this.localMemoryCache.Remove(key));
            return true;
        }

        // Internal

        private static byte[] Serialize(object serializable)
        {
            return SerializationUtil.Serialize(SerializationType.Json, serializable);
        }

        private static T Deserialize<T>(byte[] data)
        {
            return data == null ? default(T) : SerializationUtil.Deserialize<T>(SerializationType.Json, data);
        }
    }
}
