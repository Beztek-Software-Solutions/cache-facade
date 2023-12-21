// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Beztek.Facade.Sql;

    /// <summary>
    /// Interface for cache which provides signatures for the basic operations which we can do on a cache.
    /// </summary>
    public interface ICache : IDistributedLock
    {
        public CacheType CacheType { get; }

        /// <summary>
        /// Returns the value for the key, and null if it is not in the cache.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <returns>CacheProvider item value corresponding to the key; null if it is not in the cache.</returns>
        Task<T> GetAsync<T>(string key);

        /// <summary>
        /// If the cache does not have the key, put the value for the key and return null, otherwise just return the old value and do not overwrite.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <param name="value">CacheProvider item value.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> GetAndPutIfAbsentAsync<T>(string key, T value);

        /// <summary>
        /// Replaces the entry for a key only if currently mapped to some value. Does nothing and returns null if it does not exist, and returns the old value if it exists.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> GetAndReplaceAsync<T>(string key, T value);

        /// <summary>
        /// If the cache has the key, replace the value for the key and return the old value, otherwise put the value corresponding to the key and return null.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <param name="value">CacheProvider item value.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not already exist.</returns>
        Task<T> GetAndPutAsync<T>(string key, T value);

        /// <summary>
        /// Removes the value and returns it if it exists, and null if it doesn't.
        /// </summary>
        /// <param name="id">CacheProvider item key.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> RemoveAsync<T>(string key);

        /// <summary>
        /// Gets PagedResults of typed objects based on the given query and pagination parameters. The cache will support queries to the extent
        /// that this method is supported by the implementation of  the IPersistenceServoce interface.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pageNum"></param>
        /// <param name="pageSize"></param>
        /// <param name="retrieveTotalNumResults"></param>
        /// <param name="useTransaction"></param>
        /// <returns>PagedResults of typed object based on the given query and pagination parameters</returns>
        Task<PagedResults<T>> SearchByQueryAsync<T>(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false);

        /// <summary>
        /// Flushes the item corresponding to the key from the cache without writing to the persistent store (optional argument)
        /// </summary>
        /// <param name="key" is the key to be fluhed</param>
        /// <returns>Sccuess or Failure.</returns>
        Task<bool> FlushKeyAsync<T>(string key);

        /// <summary>
        /// Flushes the entire cache, or the items corresponding to the given collection of ids (optional argument)
        /// </summary>
        /// <param name="keysToFlush" is the collection of keys to be fluhed. If null, the entire cache is flushed></param>
        /// <returns>Sccuess or Failure.</returns>
        Task<bool> FlushAsync<T>(ICollection<string> keysToFlush = null);
    }
}
