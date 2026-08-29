// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Threading.Tasks;

    public static class EtagEntityUpdateHelper
    {
        private static readonly object DefaultsGate = new object();
        private static EtagEntityUpdateOptions defaults = new EtagEntityUpdateOptions();

        /// <summary>Default retry count used when <see cref="Configure"/> has not overridden options.</summary>
        public const int DefaultMaxRetryCount = 5;

        /// <summary>Default initial retry delay (ms).</summary>
        public const int DefaultInitialRetryDelayMillis = 5;

        /// <summary>Default max retry delay (ms).</summary>
        public const int DefaultMaxRetryDelayMillis = 200;

        /// <summary>
        /// Backward-compatible aliases for the current process-wide defaults
        /// (prefer <see cref="Defaults"/> or an explicit <see cref="EtagEntityUpdateOptions"/>).
        /// </summary>
        public static int MaxRetryCount => Defaults.MaxRetryCount;

        public static int InitialRetryDelayMillis => Defaults.InitialRetryDelayMillis;

        public static int MaxRetryDelayMillis => Defaults.MaxRetryDelayMillis;

        /// <summary>Process-wide defaults applied when no per-call options are supplied.</summary>
        public static EtagEntityUpdateOptions Defaults
        {
            get
            {
                lock (DefaultsGate)
                {
                    return defaults.Clone();
                }
            }
        }

        /// <summary>
        /// Replace process-wide defaults (e.g. from appsettings at startup).
        /// Null resets to built-in defaults.
        /// </summary>
        public static void Configure(EtagEntityUpdateOptions options)
        {
            lock (DefaultsGate)
            {
                defaults = (options ?? new EtagEntityUpdateOptions()).Normalized();
            }
        }

        public static Task<T> UpdateEntityAsync<T>(
            ICache cache,
            string key,
            object[] parameters,
            Func<T, object[], T> updateFunction)
            where T : IEtagEntity
            => UpdateEntityAsync(cache, key, parameters, updateFunction, options: null);

        public static async Task<T> UpdateEntityAsync<T>(
            ICache cache,
            string key,
            object[] parameters,
            Func<T, object[], T> updateFunction,
            EtagEntityUpdateOptions options)
            where T : IEtagEntity
        {
            EtagEntityUpdateOptions opts;
            lock (DefaultsGate)
            {
                opts = (options ?? defaults).Normalized();
            }

            T cachedEntity = await cache.GetAsync<T>(key).ConfigureAwait(false);
            ConcurrencyException rootException = null;
            int maxRetry = opts.MaxRetryCount;

            // Try an additional maxRetry times. i.e. if maxRetry is 5, then try 1+5 times.
            for (int retryCount = 0; retryCount <= maxRetry; retryCount++)
            {
                try
                {
                    T updatedEntity = updateFunction(cachedEntity, parameters);
                    await cache.GetAndPutAsync<T>(key, updatedEntity).ConfigureAwait(false);
                    return await cache.GetAsync<T>(key).ConfigureAwait(false);
                }
                catch (ConcurrencyException ce)
                {
                    rootException = ce;

                    if (retryCount < maxRetry)
                    {
                        int delayMillis = CalculateRetryDelayMillis(retryCount, opts);
                        if (delayMillis > 0)
                        {
                            await Task.Delay(delayMillis).ConfigureAwait(false);
                        }

                        cachedEntity = await cache.GetAsync<T>(key).ConfigureAwait(false);
                    }
                }
            }

            throw new ConcurrencyException($"Unable to update entity after {maxRetry} retries", rootException);
        }

        /// <summary>
        /// Delay before retry after <paramref name="failedAttemptIndex"/> failures (0-based).
        /// Uses process-wide <see cref="Defaults"/>.
        /// </summary>
        public static int CalculateRetryDelayMillis(int failedAttemptIndex)
            => CalculateRetryDelayMillis(failedAttemptIndex, Defaults);

        /// <summary>
        /// Delay before retry after <paramref name="failedAttemptIndex"/> failures (0-based).
        /// Exponential: initial * 2^index (capped). Fixed: initial each time (capped).
        /// </summary>
        public static int CalculateRetryDelayMillis(int failedAttemptIndex, EtagEntityUpdateOptions options)
        {
            var opts = (options ?? new EtagEntityUpdateOptions()).Normalized();
            int initial = opts.InitialRetryDelayMillis;
            int max = opts.MaxRetryDelayMillis;

            if (failedAttemptIndex < 0)
            {
                return Math.Min(initial, max);
            }

            if (!opts.UseExponentialBackoff)
            {
                return Math.Min(initial, max);
            }

            int shift = Math.Min(failedAttemptIndex, 16);
            long delay = (long)initial << shift;
            return (int)Math.Min(delay, max);
        }
    }
}
