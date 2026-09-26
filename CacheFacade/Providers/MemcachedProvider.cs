// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.IO;
    using Enyim.Caching;
    using Enyim.Caching.Configuration;
    using Enyim.Caching.Memcached;

    /// <summary>
    /// Memcached-backed <see cref="ICacheProvider"/> using EnyimMemcachedCore.
    /// Keys are prefixed with the configured cache name.
    /// </summary>
    internal class MemcachedProvider : ICacheProvider
    {
        public const SerializationType SerType = SerializationType.Json;

        private readonly IMemcachedClient client;
        private readonly string keyPrefix;
        private readonly TimeSpan timeToLive;

        public MemcachedProvider(MemcachedProviderConfiguration configuration)
            : this(CreateClient(configuration), configuration.CacheName, TimeSpan.FromMilliseconds(configuration.TimeToLiveMillis))
        {
        }

        /// <summary>Test constructor.</summary>
        internal MemcachedProvider(IMemcachedClient client, string cacheName, TimeSpan timeToLive)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.keyPrefix = string.IsNullOrEmpty(cacheName) ? string.Empty : cacheName + ":";
            this.timeToLive = timeToLive;
        }

        internal IMemcachedClient Client => this.client;

        internal string KeyPrefix => this.keyPrefix;

        public T Get<T>(string key)
        {
            string json = this.client.Get<string>(this.Prefixed(key));
            if (json == null)
            {
                return default(T);
            }

            return SerializationUtil.Deserialize<T>(SerType, SerializationUtil.StringToByte(json));
        }

        public void Put<T>(string key, T value)
        {
            string payload = SerializationUtil.ByteToString(SerializationUtil.Serialize(SerType, value));
            if (!this.client.Store(StoreMode.Set, this.Prefixed(key), payload, this.timeToLive))
            {
                throw new IOException($"Unable to save the value in the cache for key: {key}.");
            }
        }

        public T Remove<T>(string key)
        {
            string prefixed = this.Prefixed(key);
            T current = this.Get<T>(key);
            this.client.Remove(prefixed);
            return current;
        }

        public bool Clear()
        {
            // FlushAll would wipe the entire Memcached process (all key prefixes / apps).
            // Callers should FlushAsync with an explicit key list, or FlushKeyAsync per key.
            throw new NotSupportedException(
                "Memcached does not support scoped clear. Pass an explicit key list to ICache.FlushAsync, or use FlushKeyAsync.");
        }

        internal string Prefixed(string key) => this.keyPrefix + key;

        /// <summary>
        /// Opens an Enyim client against a live Memcached endpoint. Covered by live Memcached tests.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private static IMemcachedClient CreateClient(MemcachedProviderConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            ParseEndpoint(configuration.Endpoint, out string host, out int port);
            var options = new MemcachedClientOptions();
            options.AddServer(host, port);
            var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            var memcachedConfig = new MemcachedClientConfiguration(
                loggerFactory,
                Microsoft.Extensions.Options.Options.Create(options),
                configuration: null,
                transcoder: null,
                keyTransformer: null);
            return new MemcachedClient(loggerFactory, memcachedConfig);
        }

        /// <summary>Parses <c>host:port</c> Memcached endpoints.</summary>
        internal static void ParseEndpoint(string endpoint, out string host, out int port)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Memcached endpoint is required.", nameof(endpoint));
            }

            string[] parts = endpoint.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out port))
            {
                throw new ArgumentException($"Invalid Memcached endpoint '{endpoint}'. Expected host:port.", nameof(endpoint));
            }

            host = parts[0];
        }
    }
}
