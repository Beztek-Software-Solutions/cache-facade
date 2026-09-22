// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Bundles provider settings, persistence mode, and optional write-behind queue configuration
    /// for <see cref="CacheFactory.GetOrCreateCache"/>.
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>Default time to wait when acquiring a key lock (milliseconds).</summary>
        public const long DefaultLockAcquireTimeoutMillis = 2000;

        /// <summary>
        /// Default lock lease (milliseconds). Must cover provider I/O plus optional write-through persistence;
        /// raise for slow SQL paths.
        /// </summary>
        public const long DefaultLockTimeToLiveMillis = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheConfiguration"/> class.
        /// </summary>
        /// <param name="cacheProviderConfiguration">Redis or local-memory provider settings (and cache name).</param>
        /// <param name="cacheType">Non-persistent, write-through, or write-behind.</param>
        /// <param name="persistenceService">Required for write-through and write-behind.</param>
        /// <param name="queueConfiguration">Required for write-behind (queue client + message processor).</param>
        public CacheConfiguration(ICacheProviderConfiguration cacheProviderConfiguration, CacheType cacheType, IPersistenceService persistenceService = null, QueueConfiguration queueConfiguration = null)
        {
            this.CacheProviderConfiguration = cacheProviderConfiguration;
            this.CacheType = cacheType;
            this.PersistenceService = persistenceService;
            this.QueueConfiguration = queueConfiguration;
            this.LockAcquireTimeoutMillis = DefaultLockAcquireTimeoutMillis;
            this.LockTimeToLiveMillis = DefaultLockTimeToLiveMillis;
        }

        /// <summary>Provider-specific settings including the unique cache name.</summary>
        public ICacheProviderConfiguration CacheProviderConfiguration { get; }

        /// <summary>SQL (or other) persistence used for write-through and write-behind.</summary>
        public IPersistenceService PersistenceService { get; }

        /// <summary>Write-behind queue client, processor, and dequeue tuning.</summary>
        public QueueConfiguration QueueConfiguration { get; }

        /// <summary>Persistence coupling mode for this cache.</summary>
        public CacheType CacheType { get; }

        /// <summary>
        /// Max time to wait when acquiring a per-key lock used by get/put/remove.
        /// </summary>
        public long LockAcquireTimeoutMillis { get; set; }

        /// <summary>
        /// Lock lease duration. If a write-through persistence call can exceed this, increase it
        /// or risk another instance acquiring the same key lock mid-operation.
        /// </summary>
        public long LockTimeToLiveMillis { get; set; }
    }
}
