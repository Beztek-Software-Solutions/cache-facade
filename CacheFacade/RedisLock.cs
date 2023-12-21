// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using RedLockNet.SERedis;

    /// <summary>
    /// This is a wrapper around the popular RedLock algorithm for Redis locks.
    /// </summary>
    internal class RedisLock : IDistributedLock
    {
        private readonly RedLockFactory redlockFactory;

        internal RedisLock(RedLockFactory redlockFactory)
        {
            this.redlockFactory = redlockFactory;
        }
    
        public IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis)
        {
            return redlockFactory.CreateLock(lockName, TimeSpan.FromMilliseconds(timeoutMillis), TimeSpan.FromMilliseconds(lockTimeMillis), TimeSpan.FromMilliseconds(retryIntervalMillis));
        }
    }
}
