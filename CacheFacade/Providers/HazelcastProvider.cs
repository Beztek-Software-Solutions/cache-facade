// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.Threading.Tasks;
    using Hazelcast;
    using Hazelcast.DistributedObjects;

    /// <summary>
    /// Hazelcast-backed <see cref="ICacheProvider"/> using an <see cref="IHMap{TKey,TValue}"/> of JSON bytes.
    /// </summary>
    internal class HazelcastProvider : ICacheProvider, IAsyncDisposable
    {
        public const SerializationType SerType = SerializationType.Json;

        private readonly IHazelcastClient client;
        private readonly IHMap<string, byte[]> map;
        private readonly TimeSpan timeToLive;
        private readonly bool ownsClient;

        public HazelcastProvider(HazelcastProviderConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            this.timeToLive = TimeSpan.FromMilliseconds(configuration.TimeToLiveMillis);
            this.client = StartClient(configuration);
            this.ownsClient = true;
            this.map = this.client.GetMapAsync<string, byte[]>(configuration.CacheName).GetAwaiter().GetResult();
        }

        /// <summary>Test constructor.</summary>
        internal HazelcastProvider(IHMap<string, byte[]> map, TimeSpan timeToLive)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.timeToLive = timeToLive;
            this.client = null;
            this.ownsClient = false;
        }

        internal IHazelcastClient Client => this.client;

        internal IHMap<string, byte[]> Map => this.map;

        public T Get<T>(string key)
        {
            byte[] result = Await(this.map.GetAsync(key));
            return result == null ? default(T) : SerializationUtil.Deserialize<T>(SerType, result);
        }

        public void Put<T>(string key, T value)
        {
            byte[] payload = SerializationUtil.Serialize(SerType, value);
            Await(this.map.SetAsync(key, payload, this.timeToLive));
        }

        public T Remove<T>(string key)
        {
            byte[] removed = Await(this.map.RemoveAsync(key));
            return removed == null ? default(T) : SerializationUtil.Deserialize<T>(SerType, removed);
        }

        public bool Clear()
        {
            Await(this.map.ClearAsync());
            return true;
        }

        public async ValueTask DisposeAsync()
        {
            if (this.map != null)
            {
                await this.map.DisposeAsync().ConfigureAwait(false);
            }

            if (this.ownsClient && this.client != null)
            {
                await this.client.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static IHazelcastClient StartClient(HazelcastProviderConfiguration configuration)
        {
            return HazelcastClientFactory.StartNewClientAsync(options =>
            {
                options.ClusterName = configuration.ClusterName;
                options.Networking.Addresses.Add(configuration.Address);
            }).AsTask().GetAwaiter().GetResult();
        }

        private static T Await<T>(Task<T> task) => task.ConfigureAwait(false).GetAwaiter().GetResult();

        private static void Await(Task task) => task.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
