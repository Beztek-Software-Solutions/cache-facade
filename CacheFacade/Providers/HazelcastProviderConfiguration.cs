// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for a Hazelcast-backed cache provider.
    /// </summary>
    public class HazelcastProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HazelcastProviderConfiguration"/> class.
        /// </summary>
        /// <param name="clusterName">Hazelcast cluster name (must match the cluster).</param>
        /// <param name="address">Member address as host:port (e.g. <c>127.0.0.1:5701</c>).</param>
        /// <param name="cacheName">Logical map name and <see cref="CacheFactory"/> registry key.</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        public HazelcastProviderConfiguration(string clusterName, string address, string cacheName, long timeToLiveMillis = 3600000)
        {
            this.ClusterName = clusterName;
            this.Address = address;
            this.CacheName = cacheName;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.Hazelcast;
        }

        /// <summary>Hazelcast cluster name.</summary>
        public string ClusterName { get; }

        /// <summary>Cluster member address (host:port).</summary>
        public string Address { get; }

        /// <inheritdoc />
        public CacheProviderType ProviderType { get; set; }

        /// <inheritdoc />
        public string CacheName { get; set; }

        /// <inheritdoc />
        public long TimeToLiveMillis { get; set; }
    }
}
