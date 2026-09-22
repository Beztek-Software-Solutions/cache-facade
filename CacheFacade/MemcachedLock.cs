// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Threading;
    using Enyim.Caching;
    using Enyim.Caching.Memcached;

    /// <summary>
    /// Distributed lock using Memcached <see cref="StoreMode.Add"/> (set-if-absent) with TTL.
    /// <para>
    /// Release uses CAS with a past absolute expiry so the key is deleted only if we still own it
    /// (Memcached treats a past Unix expiry as immediately expired). Lease durations are clamped to
    /// whole seconds because Memcached relative TTLs are second-granularity (sub-second becomes 0 = never expire).
    /// </para>
    /// </summary>
    internal class MemcachedLock : IDisposable, IDistributedLock
    {
        /// <summary>Memcached relative expiry is in seconds; values under 1s round to 0 (never expire).</summary>
        private static readonly TimeSpan MinimumLease = TimeSpan.FromSeconds(1);

        private readonly IMemcachedClient client;
        private readonly string keyPrefix;
        private readonly string lockKey;
        private readonly string token;
        private int disposed;

        /// <summary>Factory used by <see cref="Cache"/>.</summary>
        internal MemcachedLock(IMemcachedClient client, string keyPrefix)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.keyPrefix = keyPrefix ?? string.Empty;
            this.lockKey = null;
            this.token = null;
        }

        private MemcachedLock(IMemcachedClient client, string lockKey, string token)
        {
            this.client = client;
            this.lockKey = lockKey;
            this.token = token;
        }

        public IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis)
        {
            if (string.IsNullOrEmpty(lockName))
            {
                throw new ArgumentException("Lock name is required.", nameof(lockName));
            }

            if (timeoutMillis < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMillis));
            }

            if (lockTimeMillis <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lockTimeMillis));
            }

            if (retryIntervalMillis <= 0)
            {
                retryIntervalMillis = 1;
            }

            string key = this.keyPrefix + "lock:" + lockName;
            string acquiredToken = Guid.NewGuid().ToString("N");
            TimeSpan lease = ClampLease(TimeSpan.FromMilliseconds(lockTimeMillis));
            long deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMillis;

            while (true)
            {
                if (this.client.Store(StoreMode.Add, key, acquiredToken, lease))
                {
                    return new MemcachedLock(this.client, key, acquiredToken);
                }

                long remaining = deadline - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (remaining <= 0)
                {
                    break;
                }

                Thread.Sleep((int)Math.Min(remaining, retryIntervalMillis));
            }

            throw new TimeoutException($"Unable to acquire lock: {lockName}");
        }

        public void Dispose()
        {
            if (this.lockKey == null || Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            // Compare-and-expire: CAS succeeds only with our cas unique. A past absolute expiry
            // immediately removes the item — without a non-atomic Remove that could hit a new owner.
            if (this.client.TryGetWithCas(this.lockKey, out CasResult<string> cas) && cas.Result == this.token)
            {
                DateTime alreadyExpired = DateTime.UtcNow.AddMinutes(-1);
                this.client.Cas(StoreMode.Set, this.lockKey, this.token, alreadyExpired, cas.Cas);
            }

            GC.SuppressFinalize(this);
        }

        private static TimeSpan ClampLease(TimeSpan requested)
        {
            return requested < MinimumLease ? MinimumLease : requested;
        }
    }
}
