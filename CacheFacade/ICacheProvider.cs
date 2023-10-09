// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for cache provider which provides signatures for the basic operations needed by the implementaion of the Cache abstratcion.
    /// </summary
    public interface ICacheProvider
    {
        /// <summary>
        /// Returns the value for the key, and null if it is not in the cache.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <returns>CacheProvider item value corresponding to the key; null if it is not in the cache.</returns>
        Task<T> GetAsync<T>(string key);

        /// <summary>
        /// Put the current value in the cache for the key, regardless of whether the cache already has a value for the key or not.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <param name="value">CacheProvider item value.</param>
        Task PutAsync<T>(string key, T value);

        /// <summary>
        /// Removes the value and returns it if it exists, and null if it doesn't.
        /// </summary>
        /// <param name="key">CacheProvider item key.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        Task<T> RemoveAsync<T>(string key);

        /// <summary>
        /// Clears the entire cached contents
        /// </summary>
        /// <returns>Success or failure.</returns>
        Task<bool> ClearAsync();
    }
}
