// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Bundles provider settings, persistence mode, and optional write-behind queue configuration
    /// for <see cref="CacheFactory.GetOrCreateCache"/>.
    /// </summary>
    public class CacheConfiguration
    {
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
        }

        /// <summary>Provider-specific settings including the unique cache name.</summary>
        public ICacheProviderConfiguration CacheProviderConfiguration { get; }

        /// <summary>SQL (or other) persistence used for write-through and write-behind.</summary>
        public IPersistenceService PersistenceService { get; }

        /// <summary>Write-behind queue client, processor, and dequeue tuning.</summary>
        public QueueConfiguration QueueConfiguration { get; }

        /// <summary>Persistence coupling mode for this cache.</summary>
        public CacheType CacheType { get; }
    }
}
