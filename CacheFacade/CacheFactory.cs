// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;

    public static class CacheFactory
    {
        private static readonly ConcurrentDictionary<string, ICache> CacheDictionary = new ConcurrentDictionary<string, ICache>();

        public static ICache GetOrCreateCache(CacheConfiguration cacheConfiguration, ILogger logger = null)
        {
            string cacheName = cacheConfiguration.CacheProviderConfiguration.CacheName;
            ICache result;
            if (!CacheDictionary.TryGetValue(cacheName, out result))
            {
                if (cacheConfiguration.CacheType == CacheType.WriteThrough || cacheConfiguration.CacheType == CacheType.WriteBehind)
                {
                    if (cacheConfiguration.PersistenceService == null)
                    {
                        throw new ArgumentException($"{cacheConfiguration.CacheType} needs a PersistenceService");
                    }

                    if (cacheConfiguration.QueueConfiguration == null && cacheConfiguration.CacheType == CacheType.WriteBehind)
                    {
                        throw new ArgumentException($"{cacheConfiguration.CacheType} needs a QueueConfiguration");
                    }
                }

                result = new Cache(cacheConfiguration, logger);
                result = CacheDictionary.GetOrAdd(cacheName, result);
            }

            return result;
        }

        public static ICache GetCache(string cacheName)
        {
            ICache result;
            if (CacheDictionary.TryGetValue(cacheName, out result))
            {
                return result;
            }

            return null;
        }
    }
}
