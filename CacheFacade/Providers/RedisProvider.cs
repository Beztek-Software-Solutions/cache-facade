// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using StackExchange.Redis;

    /// <summary>
    /// Provides implementation for a Redis based cache.
    /// </summary>
    internal class RedisProvider : ICacheProvider
    {
        public const SerializationType SerType = SerializationType.Json;

        /// <summary>
        /// Redis CacheProvider connection that will be thread safe lazy initialize.
        /// </summary>
        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(ConnectionConfig));

        private readonly IDatabase cacheDatabase;
        private readonly string Endpoint;
        private TimeSpan TimeToLive;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProvider"/> class using redis cache configuration.
        /// </summary>
        /// <param name="redisCacheConfiguration">Redis cache configuration.</param>
        public RedisProvider(RedisProviderConfiguration redisCacheConfiguration)
        {
            ConnectionConfig = new ConfigurationOptions {
                Password = redisCacheConfiguration.Password,
                Ssl = redisCacheConfiguration.UseSSL,
                AbortOnConnectFail = redisCacheConfiguration.AbortConnection
            };

            ConnectionConfig.EndPoints.Add(redisCacheConfiguration.Endpoint);

            this.cacheDatabase = LazyConnection.Value.GetDatabase(redisCacheConfiguration.NameIndex);
            this.Endpoint = redisCacheConfiguration.Endpoint;
            this.TimeToLive = TimeSpan.FromMilliseconds(redisCacheConfiguration.TimeToLiveMillis);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProvider"/> class using redis cache database and JsonUtil. This constructor is used for unit testing.
        /// </summary>
        /// <param name="cacheDatabase">Redis cache database.</param>
        internal RedisProvider(IDatabase cacheDatabase)
        {
            this.cacheDatabase = cacheDatabase;
        }

        /// <summary>
        /// Gets configuration reader for Redis CacheProvider.
        /// </summary>
        private static ConfigurationOptions ConnectionConfig { get; set; }

        public async Task<T> GetAsync<T>(string key)
        {
            var result = await this.cacheDatabase.StringGetAsync(key).ConfigureAwait(false);
            return SerializationUtil.Deserialize<T>(SerType, result);
        }

        public async Task PutAsync<T>(string key, T value)
        {
            bool result = await this.cacheDatabase.StringSetAsync(key, SerializationUtil.ByteToString(SerializationUtil.Serialize(SerType, value)), this.TimeToLive).ConfigureAwait(false);
            if (!result)
            {
                throw new IOException($"Unable to save the value 'in the cache for key: {key}.");
            }
        }

        public async Task<T> RemoveAsync<T>(string key)
        {
            T currentValue = default(T);
            if (await this.cacheDatabase.KeyExistsAsync(key).ConfigureAwait(false))
            {
                string currentValueString = await this.cacheDatabase.StringGetAsync(key).ConfigureAwait(false);
                if (currentValueString != null)
                {
                    await this.cacheDatabase.KeyDeleteAsync(key).ConfigureAwait(false);
                    currentValue = SerializationUtil.Deserialize<T>(SerType, SerializationUtil.StringToByte(currentValueString));
                }
            }

            return currentValue;
        }

        public async Task<bool> ClearAsync()
        {
            return await Task.Run( () =>
            {
                IServer server = LazyConnection.Value.GetServer(this.Endpoint);
                server.FlushDatabase();
                return true;
            }).ConfigureAwait(false);
        }
    }
}
