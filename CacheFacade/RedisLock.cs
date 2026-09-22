// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using RedLockNet;
    using RedLockNet.SERedis;

    /// <summary>
    /// Distributed lock using the RedLock algorithm (requires Redis Lua scripts for safe unlock).
    /// </summary>
    internal class RedisLock : IDistributedLock
    {
        private readonly RedLockFactory redlockFactory;

        internal RedisLock(RedLockFactory redlockFactory)
        {
            this.redlockFactory = redlockFactory ?? throw new ArgumentNullException(nameof(redlockFactory));
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

            // RedLock CreateLock(resource, expiryTime, waitTime, retryTime)
            IRedLock redlock = this.redlockFactory.CreateLock(
                lockName,
                TimeSpan.FromMilliseconds(lockTimeMillis),
                TimeSpan.FromMilliseconds(timeoutMillis),
                TimeSpan.FromMilliseconds(retryIntervalMillis));

            if (!redlock.IsAcquired)
            {
                redlock.Dispose();
                throw new TimeoutException($"Unable to acquire lock: {lockName}");
            }

            return redlock;
        }
    }
}
