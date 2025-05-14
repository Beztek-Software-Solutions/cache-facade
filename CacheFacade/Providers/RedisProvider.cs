// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.IO;
    using StackExchange.Redis;

    /// <summary>
    /// Provides implementation for a Redis based cache.
    /// </summary>
    internal class RedisProvider : ICacheProvider
    {
        public const SerializationType SerType = SerializationType.Json;

        /// <summary>
        /// Gets configuration reader for Redis CacheProvider.
        /// </summary>
        private static ConfigurationOptions ConnectionConfig { get; set; }

        /// <summary>
        /// Redis CacheProvider connection that will be thread safe lazy initialize.
        /// </summary>
        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(ConnectionConfig));

        private readonly IDatabase cacheDatabase;
        private TimeSpan TimeToLive;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProvider"/> class using redis cache configuration.
        /// </summary>
        /// <param name="redisCacheConfiguration">Redis cache configuration.</param>
        public RedisProvider(RedisProviderConfiguration redisCacheConfiguration)
        {
            ConnectionConfig = string.IsNullOrWhiteSpace(redisCacheConfiguration.Options) ?
                new ConfigurationOptions() : ConfigurationOptions.Parse(redisCacheConfiguration.Options);

            ConnectionConfig.Password = redisCacheConfiguration.Password;
            ConnectionConfig.Ssl = redisCacheConfiguration.UseSSL;
            ConnectionConfig.AbortOnConnectFail = redisCacheConfiguration.AbortConnection;
            ConnectionConfig.AllowAdmin = true;
            ConnectionConfig.EndPoints.Add(redisCacheConfiguration.Endpoint);
    
            this.cacheDatabase = LazyConnection.Value.GetDatabase(redisCacheConfiguration.NameIndex);
            //this.Endpoint = redisCacheConfiguration.Endpoint;
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

        public T Get<T>(string key)
        {
            var result = this.cacheDatabase.StringGet(key);
            return SerializationUtil.Deserialize<T>(SerType, result);
        }

        public void Put<T>(string key, T value)
        {
            bool result = this.cacheDatabase.StringSet(key, (string)SerializationUtil.ByteToString(SerializationUtil.Serialize(SerType, value)), this.TimeToLive);
            if (!result)
            {
                throw new IOException($"Unable to save the value 'in the cache for key: {key}.");
            }
        }

        public T Remove<T>(string key)
        {
            T currentValue = default(T);
            if (this.cacheDatabase.KeyExists(key))
            {
                string currentValueString = this.cacheDatabase.StringGet(key);
                if (currentValueString != null)
                {
                    this.cacheDatabase.KeyDelete(key);
                    currentValue = SerializationUtil.Deserialize<T>(SerType, SerializationUtil.StringToByte(currentValueString));
                }
            }

            return currentValue;
        }

        public bool Clear() {
            ConnectionMultiplexer redis = LazyConnection.Value;
            foreach (var endpoint in redis.GetEndPoints())
            {
                IServer server = redis.GetServer(endpoint);
                server.FlushAllDatabases();
            }
            return true;
        }
    }
}
