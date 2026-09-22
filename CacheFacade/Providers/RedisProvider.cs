// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using StackExchange.Redis;

    /// <summary>
    /// Provides implementation for a Redis based cache.
    /// </summary>
    internal class RedisProvider : ICacheProvider
    {
        public const SerializationType SerType = SerializationType.Json;

        /// <summary>
        /// Shared multiplexers keyed by connection identity so distinct endpoints
        /// (Redis vs Dragonfly vs Garnet in live tests) do not collide.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>> Multiplexers =
            new ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>>(StringComparer.Ordinal);

        private readonly IDatabase cacheDatabase;
        private readonly string connectionKey;
        private readonly int databaseIndex;
        private TimeSpan TimeToLive;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProvider"/> class using redis cache configuration.
        /// </summary>
        /// <param name="redisCacheConfiguration">Redis cache configuration.</param>
        public RedisProvider(RedisProviderConfiguration redisCacheConfiguration)
        {
            ConfigurationOptions connectionConfig = string.IsNullOrWhiteSpace(redisCacheConfiguration.Options) ?
                new ConfigurationOptions() : ConfigurationOptions.Parse(redisCacheConfiguration.Options);

            connectionConfig.Password = redisCacheConfiguration.Password;
            connectionConfig.Ssl = redisCacheConfiguration.UseSSL;
            connectionConfig.AbortOnConnectFail = redisCacheConfiguration.AbortConnection;
            connectionConfig.AllowAdmin = true;
            connectionConfig.EndPoints.Add(redisCacheConfiguration.Endpoint);

            this.connectionKey = BuildConnectionKey(redisCacheConfiguration);
            this.databaseIndex = redisCacheConfiguration.NameIndex;
            ConfigurationOptions connectOptions = connectionConfig;
            ConnectionMultiplexer multiplexer = Multiplexers.GetOrAdd(
                this.connectionKey,
                _ => new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connectOptions))).Value;

            this.cacheDatabase = multiplexer.GetDatabase(this.databaseIndex);
            this.TimeToLive = TimeSpan.FromMilliseconds(redisCacheConfiguration.TimeToLiveMillis);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProvider"/> class using redis cache database and JsonUtil. This constructor is used for unit testing.
        /// </summary>
        /// <param name="cacheDatabase">Redis cache database.</param>
        internal RedisProvider(IDatabase cacheDatabase)
        {
            this.cacheDatabase = cacheDatabase;
            this.connectionKey = null;
            this.databaseIndex = 0;
        }

        /// <summary>Underlying Redis database used for cache operations and token locks.</summary>
        internal IDatabase Database => this.cacheDatabase;

        /// <summary>Shared multiplexer for this endpoint (also used by RedLock).</summary>
        internal IConnectionMultiplexer Multiplexer =>
            this.connectionKey != null && Multiplexers.TryGetValue(this.connectionKey, out Lazy<ConnectionMultiplexer> lazy)
                ? lazy.Value
                : this.cacheDatabase?.Multiplexer;

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

        public bool Clear()
        {
            if (this.connectionKey == null || !Multiplexers.TryGetValue(this.connectionKey, out Lazy<ConnectionMultiplexer> lazy))
            {
                return false;
            }

            // Flush only this logical DB — never FlushAllDatabases (would wipe every DB on the server).
            ConnectionMultiplexer redis = lazy.Value;
            foreach (var endpoint in redis.GetEndPoints())
            {
                IServer server = redis.GetServer(endpoint);
                server.FlushDatabase(this.databaseIndex);
            }
            return true;
        }

        private static string BuildConnectionKey(RedisProviderConfiguration configuration)
        {
            return string.Join(
                "|",
                configuration.Endpoint ?? "",
                configuration.Password ?? "",
                configuration.UseSSL,
                configuration.AbortConnection,
                configuration.Options ?? "");
        }
    }
}
