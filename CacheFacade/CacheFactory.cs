// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Static registry of named <see cref="ICache"/> instances.
    /// Caches are keyed by <see cref="ICacheProviderConfiguration.CacheName"/> and created at most once.
    /// </summary>
    public static class CacheFactory
    {
        private static readonly ConcurrentDictionary<string, ICache> CacheDictionary = new ConcurrentDictionary<string, ICache>();

        /// <summary>
        /// Returns an existing cache for the configuration's cache name, or creates and registers a new one.
        /// Write-through requires <see cref="CacheConfiguration.PersistenceService"/>;
        /// write-behind also requires <see cref="CacheConfiguration.QueueConfiguration"/>.
        /// </summary>
        /// <param name="cacheConfiguration">Provider, cache type, and optional persistence/queue settings.</param>
        /// <param name="logger">Optional logger for facade diagnostics.</param>
        /// <returns>The registered <see cref="ICache"/> for the configuration's cache name.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when write-through/write-behind is missing required persistence or queue configuration.
        /// </exception>
        public static ICache GetOrCreateCache(CacheConfiguration cacheConfiguration, ILogger logger = null)
        {
            // Sets the minimum number of threads the .NET ThreadPool keeps "warm" before it starts introducing delays.
            // If the app sees high load bursts, a low MinThreads value can cause latency spikes, including timeouts.
            // Default values are often very low (sometimes 1 on Linux containers or Azure App Service),
            // which is too little for I/O-heavy workloads.
            ThreadPool.SetMinThreads(workerThreads: 256, completionPortThreads: 256);

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

        /// <summary>
        /// Looks up a previously registered cache by name.
        /// </summary>
        /// <param name="cacheName">Name matching <see cref="ICacheProviderConfiguration.CacheName"/>.</param>
        /// <returns>The cache, or <c>null</c> if none has been created for that name.</returns>
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
