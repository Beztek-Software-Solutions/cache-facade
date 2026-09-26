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
        private readonly Func<string, TimeSpan, TimeSpan, TimeSpan, IRedLock> createLock;

        internal RedisLock(RedLockFactory redlockFactory)
            : this(CreateViaFactory(redlockFactory))
        {
        }

        /// <summary>Test constructor — injects the RedLock create delegate without a live multiplexer.</summary>
        internal RedisLock(Func<string, TimeSpan, TimeSpan, TimeSpan, IRedLock> createLock)
        {
            this.createLock = createLock ?? throw new ArgumentNullException(nameof(createLock));
        }

        public IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis)
        {
            retryIntervalMillis = DistributedLockArgs.Normalize(lockName, timeoutMillis, lockTimeMillis, retryIntervalMillis);

            // RedLock CreateLock(resource, expiryTime, waitTime, retryTime)
            IRedLock redlock = this.createLock(
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

        private static Func<string, TimeSpan, TimeSpan, TimeSpan, IRedLock> CreateViaFactory(RedLockFactory redlockFactory)
        {
            if (redlockFactory == null)
            {
                throw new ArgumentNullException(nameof(redlockFactory));
            }

            return (name, expiry, wait, retry) => redlockFactory.CreateLock(name, expiry, wait, retry);
        }
    }
}
