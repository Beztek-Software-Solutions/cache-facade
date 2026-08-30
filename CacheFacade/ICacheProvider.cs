// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    /// <summary>
    /// Low-level cache provider used by the facade implementation (get/put/remove/clear).
    /// Applications use <see cref="ICache"/>; providers are selected via configuration.
    /// </summary>
    public interface ICacheProvider
    {
        /// <summary>
        /// Returns the value for the key, and null if it is not in the cache.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <returns>Value corresponding to the key; null if it is not in the cache.</returns>
        T Get<T>(string key);

        /// <summary>
        /// Puts the current value in the cache for the key, regardless of whether the cache already has a value for the key.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <param name="value">Cache item value.</param>
        void Put<T>(string key, T value);

        /// <summary>
        /// Removes the value and returns it if it exists, and null if it doesn't.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Cache item key.</param>
        /// <returns>Old value corresponding to the cache item key; null if key does not exist.</returns>
        T Remove<T>(string key);

        /// <summary>
        /// Clears the entire cached contents.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        bool Clear();
    }
}
