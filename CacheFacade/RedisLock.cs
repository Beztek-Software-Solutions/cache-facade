// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using RedLockNet.SERedis;

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
