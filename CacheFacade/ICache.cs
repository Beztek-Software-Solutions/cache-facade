// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Beztek.Facade.Sql;

    /// <summary>
    /// Unified cache API: get/put/remove, optional persistence modes, SQL search, flush, and distributed locks.
    /// Obtain instances via <see cref="CacheFactory"/>.
    /// </summary>
    public interface ICache : IDistributedLock
    {
        /// <summary>
        /// Persistence coupling for this cache (<see cref="CacheType.NonPersistent"/>,
        /// <see cref="CacheType.WriteThrough"/>, or <see cref="CacheType.WriteBehind"/>).
        /// </summary>
        public CacheType CacheType { get; }

        /// <summary>
        /// Returns the value for the key, and null if it is not in the cache.
        /// For write-through / write-behind caches, a miss loads from persistence and fills the provider.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <returns>Cached (or loaded) value; null if absent.</returns>
        Task<T> GetAsync<T>(string key);

        /// <summary>
        /// Returns the value only if it is already in the cache provider. Does not load from persistence and does not write.
        /// Use for paged list hydration: peek hits, batch-load misses from SQL, then <see cref="WarmAsync{T}"/>.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <returns>Cached value, or null if absent from the provider.</returns>
        Task<T> PeekAsync<T>(string key);

        /// <summary>
        /// Puts a value into the cache provider only (no persistence Create/Update).
        /// Same fill used after a read-through miss in <see cref="GetAsync{T}"/>; not a DB write.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <param name="value">Value already loaded from persistence (e.g. batch GetByIds).</param>
        Task WarmAsync<T>(string key, T value);

        /// <summary>
        /// If the cache does not have the key, put the value for the key and return null, otherwise just return the old value and do not overwrite.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <param name="value">Value to insert if absent.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> GetAndPutIfAbsentAsync<T>(string key, T value);

        /// <summary>
        /// Replaces the entry for a key only if currently mapped to some value. Does nothing and returns null if it does not exist, and returns the old value if it exists.
        /// For <see cref="IEtagEntity"/> values, the incoming etag must match the cached etag or a <see cref="ConcurrencyException"/> is thrown.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <param name="value">Replacement value.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> GetAndReplaceAsync<T>(string key, T value);

        /// <summary>
        /// If the cache has the key, replace the value for the key and return the old value, otherwise put the value corresponding to the key and return null.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <param name="value">Value to put.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not already exist.</returns>
        Task<T> GetAndPutAsync<T>(string key, T value);

        /// <summary>
        /// Removes the value and returns it if it exists, and null if it doesn't.
        /// Write-through deletes from persistence; write-behind enqueues a delete snapshot.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> RemoveAsync<T>(string key);

        /// <summary>
        /// Returns paged results of typed objects for the given SQL select and pagination parameters.
        /// Supported to the extent that <see cref="IPersistenceService.SearchIdsByQueryAsync"/> is implemented;
        /// results are hydrated via <see cref="GetAsync{T}"/>.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
        /// <param name="query">SQL select that returns entity ids (and optionally more columns).</param>
        /// <param name="pageNum">1-based page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="retrieveTotalNumResults">When true, also compute total row count.</param>
        /// <returns>Paged entity results.</returns>
        Task<PagedResults<T>> SearchByQueryAsync<T>(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false);

        /// <summary>
        /// Removes the item for the key from the cache provider only (does not write to persistence).
        /// Pending write-behind messages for the key are still drained from their snapshots.
        /// </summary>
        /// <typeparam name="T">Value type (unused; present for API consistency).</typeparam>
        /// <param name="key">Key to flush.</param>
        /// <returns><c>true</c> if the flush completed.</returns>
        Task<bool> FlushKeyAsync<T>(string key);

        /// <summary>
        /// Flushes the entire cache, or only the given keys, from the provider (no persistence writes).
        /// </summary>
        /// <typeparam name="T">Value type (unused; present for API consistency).</typeparam>
        /// <param name="keysToFlush">Keys to flush; when null, clears the entire provider.</param>
        /// <returns><c>true</c> if the flush completed.</returns>
        Task<bool> FlushAsync<T>(ICollection<string> keysToFlush = null);
    }
}
