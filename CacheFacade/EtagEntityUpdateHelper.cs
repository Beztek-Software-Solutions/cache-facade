// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Threading.Tasks;

    public static class EtagEntityUpdateHelper
    {
        public const int MaxRetryCount = 3;

        public static async Task<T> UpdateEntityAsync<T>(ICache cache, string key, object[] parameters, Func<T, object[], T> updateFunction)
            where T : IEtagEntity
        {
            T cachedEntity = await cache.GetAsync<T>(key).ConfigureAwait(false);
            ConcurrencyException rootException = null;

            // Try an additional RetryCount times. i.e. if MaxRetryCount is 3, then try 1+3 times
            for (int retryCount = 0; retryCount <= MaxRetryCount; retryCount++)
            {
                try
                {
                    // Attempt to update the entity
                    T updatedEntity = updateFunction(cachedEntity, parameters);
                    await cache.GetAndPutAsync<T>(key, updatedEntity).ConfigureAwait(false);

                    // Return with the value that was saved (with the updated Etag)
                    return await cache.GetAsync<T>(key).ConfigureAwait(false);
                }
                catch (ConcurrencyException ce)
                {
                    rootException = ce;

                    // Get the latest cachedEntity for retry
                    if (retryCount < MaxRetryCount)
                    {
                        cachedEntity = await cache.GetAsync<T>(key).ConfigureAwait(false);
                    }
                }
            }

            throw new ConcurrencyException($"Unable to updated entiry after {MaxRetryCount} retries", rootException);
        }
    }
}
