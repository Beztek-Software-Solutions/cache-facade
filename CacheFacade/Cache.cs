// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;

    using Beztek.Facade.Cache.Providers;
    using Beztek.Facade.Queue;
    using Beztek.Facade.Sql;

    using Microsoft.Extensions.Logging;
    using RedLockNet.SERedis;
    using RedLockNet.SERedis.Configuration;

    /// <summary>
    /// Internal abstraction of the cache provider which provides structure of basic operations including the logging methods (trace logs and dependency metrics).
    /// </summary>
    /// <typeparam name="T">Data type of the cache item value. By default, the cache item will have a string based key.</typeparam>
    public class Cache : ICache
    {
        private const string LockCacheName = "lockCache";
        private const long LockTimeToLiveMillis = 3000;
        private const long LockAcquireTimeoutMillis = 1000;
        private readonly QueueClient queueClient;
        private readonly IDistributedLock DistributedLock;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class using cache configuration and logger object.
        /// </summary>
        /// <param name="cacheConfiguration">Cache configuration object.</param>
        /// <param name="logger">Logger object.</param>
        public Cache(CacheConfiguration cacheConfiguration, ILogger logger)
        {
            this.Logger = logger;

            ICacheProviderConfiguration lockProviderConfiguration = null;
            switch (cacheConfiguration.CacheProviderConfiguration)
            {
                // Cache Provider
                case RedisProviderConfiguration redisConfiguration:
                    this.CacheProvider = new RedisProvider(redisConfiguration);
                    string[] endpointParts = redisConfiguration.Endpoint.Split(":");
                    var azureEndpoint = new RedLockEndPoint {
                        EndPoint = new DnsEndPoint(endpointParts[0], Int32.Parse(endpointParts[1])),
                        Password = redisConfiguration.Password,
                        Ssl = redisConfiguration.UseSSL
                    };
                    this.DistributedLock = new RedisLock(RedLockFactory.Create(new List<RedLockEndPoint>() { azureEndpoint }));
                    break;
                case LocalMemoryProviderConfiguration localMemoryProviderConfiguration:
                    this.CacheProvider = new LocalMemoryProvider(localMemoryProviderConfiguration);
                    lockProviderConfiguration = new LocalMemoryProviderConfiguration(LockCacheName, LockTimeToLiveMillis);
                    break;
                default:
                    // This is useful for unit tests, and only unit tests can reach here
                    lockProviderConfiguration = new LocalMemoryProviderConfiguration(LockCacheName, LockTimeToLiveMillis);
                    break;
            }

            // Lock cache
            if (lockProviderConfiguration != null && !string.Equals(LockCacheName, cacheConfiguration.CacheProviderConfiguration.CacheName, StringComparison.Ordinal))
            {
                ICache lockCache = CacheFactory.GetOrCreateCache(new CacheConfiguration(lockProviderConfiguration, CacheType.NonPersistent));
                this.DistributedLock = new DisposableLock(lockCache, LockCacheName);
            }

            // Cache Type
            this.CacheType = cacheConfiguration.CacheType;

            if (this.CacheType == CacheType.WriteThrough || this.CacheType == CacheType.WriteBehind)
            {
                // Persistence
                this.PersistenceService = cacheConfiguration.PersistenceService;

                // Queue
                if (this.CacheType == CacheType.WriteBehind)
                {
                    this.queueClient = (QueueClient)cacheConfiguration.QueueConfiguration.QueueClient;

                    DefaultProcessorHandler handler = new DefaultProcessorHandler();
                    IQueueProcessorHandler queyeProcessorhandler = handler.AddProcessor(typeof(string), cacheConfiguration.QueueConfiguration.MessageProcessor);

                    // Setup of dequeueing for the write-behind cache
                    this.queueClient.DequeueAndProcess(
                        cacheConfiguration.QueueConfiguration.MaxProcessingRate,
                        cacheConfiguration.QueueConfiguration.MaxBackgroundTasks,
                        queyeProcessorhandler,
                        cacheConfiguration.QueueConfiguration.CancellationToken,
                        cacheConfiguration.QueueConfiguration.BatchSize,
                        cacheConfiguration.QueueConfiguration.PollIntervalMillis
                    );
                }
            }

            if (this.CacheType == CacheType.WriteThrough || this.CacheType == CacheType.WriteBehind)
            {
                // Persistence
                this.PersistenceService = cacheConfiguration.PersistenceService;
            }
        }

        internal IPersistenceService PersistenceService { get; }

        internal ICacheProvider CacheProvider { get; set; }

        public CacheType CacheType { get; }

        protected ILogger Logger { get; set; }

        public async Task<T> GetAsync<T>(string key)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            // Create a lock
            using IDisposable currLock = this.AcquireLock(key, LockAcquireTimeoutMillis, LockTimeToLiveMillis, CalculateRetryIntervalMillis(LockAcquireTimeoutMillis));
            try
            {
                this.Logger?.LogDebug($"Getting item from cache. Key: {key}");

                T result = await this.CacheProvider.GetAsync<T>(key).ConfigureAwait(false);

                bool isPersistent = this.CacheType == CacheType.WriteThrough || this.CacheType == CacheType.WriteBehind;

                // If the result is in the cache, or if this is not a persistent cache, we are done
                if (result != null || !isPersistent)
                {
                    return result;
                }

                // Result is not in the cache, so get it from the persistent store
                result = (T)await this.PersistenceService.GetByIdAsync(key).ConfigureAwait(false);

                // If we find a result, put it in the cache
                if (result != null)
                {
                    await this.CacheProvider.PutAsync<T>(key, result).ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception e)
            {
                string message = $"Error occurred when getting item from cache. Key: {key}";
                this.Logger?.LogError(e, message);
                throw new IOException(message, e);
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"GetAsync/{key}");
            }
        }

        public async Task<T> GetAndPutIfAbsentAsync<T>(string key, T value)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            using IDisposable currLock = this.AcquireLock(key, LockAcquireTimeoutMillis, LockTimeToLiveMillis, CalculateRetryIntervalMillis(LockAcquireTimeoutMillis));

            // Load the original object from the DB if it is there
            T result = await this.GetAsync<T>(key).ConfigureAwait(false);
            try
            {
                this.Logger?.LogDebug($"CacheProvider GetAndPutIfAbsentAsync. Key: {key}");

                // If there is an item in the cache, return the cached object back as we are done, since the item is not absent
                if (result != null)
                {
                    return result;
                }

                // There isn't anything in the cache if we get here. Delegate to the cache provider
                await this.CacheProvider.PutAsync(key, value).ConfigureAwait(false);

                switch (this.CacheType)
                {
                    case CacheType.WriteThrough:
                        await this.PersistenceService.CreateAsync(key, value).ConfigureAwait(false);
                        break;

                    case CacheType.WriteBehind:
                        await this.queueClient.Enqueue<string>(key, true).ConfigureAwait(false);
                        break;
                }

                return result;
            }
            catch (Exception e)
            {
                // revert the cache write
                await this.RollbackCacheValue<T>(key, result).ConfigureAwait(false);

                string message = $"Error occurred during GetAndPutIfAbsentAsync. Key: {key}";
                this.Logger?.LogError(e, message);
                throw new IOException(message, e);
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"GetAndPutIfAbsentAsync/{key}");
            }
        }

        public async Task<T> GetAndReplaceAsync<T>(string key, T value)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            using IDisposable currLock = this.AcquireLock(key, LockAcquireTimeoutMillis, LockTimeToLiveMillis, CalculateRetryIntervalMillis(LockAcquireTimeoutMillis));

            // Load the original object from the DB if it is there
            T result = await this.GetAsync<T>(key).ConfigureAwait(false);
            try
            {
                this.Logger?.LogDebug($"CacheProvider GetAndReplaceAsync. Key: {key}");

                if (result != null)
                {
                    IEtagEntity currObject = value as IEtagEntity;
                    if (currObject != null)
                    {
                        if (!string.Equals(((IEtagEntity)result).Etag, currObject.Etag, StringComparison.Ordinal))
                        {
                            throw new ConcurrencyException("Object was already updated first");
                        }

                        // Object is fresh, set with new Etag
                        currObject.Etag = EtagUtil.GenerateEtag();
                    }

                    await this.CacheProvider.PutAsync(key, value).ConfigureAwait(false);

                    switch (this.CacheType)
                    {
                        case CacheType.WriteThrough:
                            await this.PersistenceService.UpdateAsync(key, value).ConfigureAwait(false);
                            break;

                        case CacheType.WriteBehind:
                            await this.queueClient.Enqueue<string>(key, true).ConfigureAwait(false);
                            break;
                    }
                }

                return result;
            }
            catch (ConcurrencyException e)
            {
                this.Logger?.LogError(e.Message);
                throw;
            }
            catch (Exception e)
            {
                // revert the cache write
                await this.RollbackCacheValue<T>(key, result).ConfigureAwait(false);

                string message = $"Error occurred during GetAndReplaceAsync. Key: {key}";
                this.Logger?.LogError(e, message);
                throw new IOException(message);
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"GetAndReplaceAsync/{key}");
            }
        }

        public async Task<T> GetAndPutAsync<T>(string key, T value)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            using IDisposable currLock = this.AcquireLock(key, LockAcquireTimeoutMillis, LockTimeToLiveMillis, CalculateRetryIntervalMillis(LockAcquireTimeoutMillis));

            // Load the original object from the DB if it is there
            T result = await this.GetAsync<T>(key).ConfigureAwait(false);
            try
            {
                bool isCreate = result == null;

                if (isCreate)
                {
                    this.Logger?.LogDebug($"CacheProvider GetAndPutAsync : Create - Key: {key}");
                    await this.GetAndPutIfAbsentAsync<T>(key, value).ConfigureAwait(false);
                }
                else
                {
                    this.Logger?.LogDebug($"CacheProvider GetAndPutAsync : Update - Key: {key}");
                    await this.GetAndReplaceAsync<T>(key, value).ConfigureAwait(false);
                }

                return result;
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"GetAndPutAsync/{key}");
            }
        }

        public async Task<T> RemoveAsync<T>(string key)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            using IDisposable currLock = this.AcquireLock(key, LockAcquireTimeoutMillis, LockTimeToLiveMillis, CalculateRetryIntervalMillis(LockAcquireTimeoutMillis));

            // Load the original object from the DB if it is there
            T result = await this.GetAsync<T>(key).ConfigureAwait(false);
            try
            {
                this.Logger?.LogDebug($"CacheProvider RemoveAsync. id: {key}");

                await this.CacheProvider.RemoveAsync<T>(key).ConfigureAwait(false);

                switch (this.CacheType)
                {
                    case CacheType.WriteThrough:
                        await this.PersistenceService.DeleteAsync(key).ConfigureAwait(false);
                        break;

                    case CacheType.WriteBehind:
                        await this.queueClient.Enqueue<string>(key, true).ConfigureAwait(false);
                        break;
                }

                return result;
            }
            catch (Exception e)
            {
                // revert the cache write
                if (result != null)
                {
                    await this.CacheProvider.PutAsync(key, result).ConfigureAwait(false);
                }

                string message = $"Error occurred when removing item from cache. Key: {key}";
                this.Logger?.LogError(e, message);
                throw new IOException(message, e);
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"RemoveAsync/{key}");
            }
        }

        public async Task<PagedResults<T>> SearchByQueryAsync<T>(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false)
        {
            if (this.CacheType == CacheType.WriteThrough || this.CacheType == CacheType.WriteBehind)
            {
                PagedResults<string> pagedIds = await this.PersistenceService.SearchIdsByQueryAsync(query, pageNum, pageSize, retrieveTotalNumResults).ConfigureAwait(false);

                List<Task<T>> tasks = new List<Task<T>>();
                foreach (string id in pagedIds.PagedList)
                {
                    tasks.Add(this.GetAsync<T>(id));
                }

                List<T> results = new List<T>();
                foreach (Task<T> task in tasks)
                {
                    results.Add(await task.ConfigureAwait(false));
                }

                PagedResultsWithTotal<string> pagedIdsWIthTotal = pagedIds as PagedResultsWithTotal<string>;
                if (pagedIdsWIthTotal != null)
                {
                    return new PagedResultsWithTotal<T>(pagedIds.PageNum, pagedIds.PageSize, results, pagedIdsWIthTotal.TotalResults);
                }

                return new PagedResults<T>(pagedIds.PageNum, pagedIds.PageSize, results);
            }

            throw new NotSupportedException();
        }

        public async Task<bool> FlushKeyAsync<T>(string key)
        {
            if (key != null)
            {
                return await this.FlushAsync<T>(new string[] { key }).ConfigureAwait(false);
            }

            return false;
        }

        public async Task<bool> FlushAsync<T>(ICollection<string> keysToFlush = null)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            Stopwatch stopWatch = Stopwatch.StartNew();

            try
            {
                if (keysToFlush == null)
                {
                    try
                    {
                        return await this.CacheProvider.ClearAsync().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        string message = "Error occurred when flushing the cache. Ke";
                        this.Logger?.LogError(e, message);
                        throw new IOException(message, e);
                    }
                }

                // Remove each id individually
                IList<Task> results = new List<Task>();
                foreach (string key in keysToFlush)
                {
                    results.Add(this.CacheProvider.RemoveAsync<T>(key));
                }

                // Wait for all the removals to compplete
                foreach (Task refreshTask in results)
                {
                    await refreshTask.ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                stopWatch.Stop();
                this.Logger?.LogDebug($"FlushAsync");
            }
        }

        public IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis)
        {
            // The LockCache itself does not have an internal lockCache, so it should not acquire a lock
            if (this.DistributedLock == null)
            {
                return null;
            }

            return DistributedLock.AcquireLock(lockName, timeoutMillis, lockTimeMillis, retryIntervalMillis);
        }

        // Internal

        private static int CalculateRetryIntervalMillis(long timeoutMillis)
        {
            return (int)Math.Min(timeoutMillis / 100, 1);
        }

        /// <summary>
        /// This method is called when we want the cache provider to roll back to the provided value, or remove it from the cache if it is null
        /// </summary>
        /// <typeparam name="T">The Type of the value object</typeparam>
        /// <param name="key">The key to be used in the cache</param>
        /// <param name="value">The object to be stored into or removed from the cache</param>
        /// <returns></returns>
        private async Task RollbackCacheValue<T>(string key, T value)
        {
            if (value != null)
            {
                await this.CacheProvider.PutAsync(key, value).ConfigureAwait(false);
            }
            else
            {
                await this.CacheProvider.RemoveAsync<T>(key).ConfigureAwait(false);
            }
        }
    }
}
