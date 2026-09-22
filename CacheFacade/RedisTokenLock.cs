// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Threading;
    using StackExchange.Redis;

    /// <summary>
    /// Redis-protocol lock that does not require Lua (unlike RedLock unlock).
    /// Acquire uses <c>SET NX</c> with TTL; release uses <see cref="IDatabase.LockRelease"/>
    /// (atomic CAD on Redis 8.4+, conditional transaction on older servers).
    /// If compare-and-delete is unavailable, the key is left to expire via TTL (never blindly deleted).
    /// </summary>
    internal class RedisTokenLock : IDisposable, IDistributedLock
    {
        private readonly IDatabase database;
        private readonly string keyPrefix;
        private readonly string lockKey;
        private readonly string token;
        private int disposed;

        /// <summary>Factory used by <see cref="Cache"/>.</summary>
        internal RedisTokenLock(IDatabase database, string keyPrefix)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.keyPrefix = string.IsNullOrEmpty(keyPrefix) ? "lock:" : keyPrefix.TrimEnd(':') + ":lock:";
            this.lockKey = null;
            this.token = null;
        }

        private RedisTokenLock(IDatabase database, string lockKey, string token)
        {
            this.database = database;
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

            string key = this.keyPrefix + lockName;
            string acquiredToken = Guid.NewGuid().ToString("N");
            TimeSpan lease = TimeSpan.FromMilliseconds(lockTimeMillis);
            long deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMillis;

            while (true)
            {
                if (this.database.StringSet(key, acquiredToken, lease, When.NotExists))
                {
                    return new RedisTokenLock(this.database, key, acquiredToken);
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

            try
            {
                // Prefer LockRelease over hand-rolled WATCH/MULTI: SE.Redis uses DELEX IFEQ on
                // Redis 8.4+ and a conditional transaction on older servers (SER301).
                this.database.LockRelease(this.lockKey, this.token);
            }
            catch
            {
                // RESP subsets may lack WATCH/MULTI (and CAD). Do not get-and-delete — that can
                // remove a newer owner's lock. Leave the key; lease TTL will release it.
            }

            GC.SuppressFinalize(this);
        }
    }
}
