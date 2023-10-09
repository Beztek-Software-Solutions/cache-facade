// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    public class CacheConfiguration
    {
        public CacheConfiguration(ICacheProviderConfiguration cacheProviderConfiguration, CacheType cacheType, IPersistenceService persistenceService = null, QueueConfiguration queueConfiguration = null)
        {
            this.CacheProviderConfiguration = cacheProviderConfiguration;
            this.CacheType = cacheType;
            this.PersistenceService = persistenceService;
            this.QueueConfiguration = queueConfiguration;
        }

        public ICacheProviderConfiguration CacheProviderConfiguration { get; }

        public IPersistenceService PersistenceService { get; }

        public QueueConfiguration QueueConfiguration { get; }

        public CacheType CacheType { get; }
    }
}
