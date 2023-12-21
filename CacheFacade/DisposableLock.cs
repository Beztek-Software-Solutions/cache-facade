// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Threading;

    internal class DisposableLock : IDisposable, IDistributedLock
    {
        private readonly ICache lockCache;
        private readonly string name;

        internal DisposableLock(ICache lockCache, string name)
        {
            this.lockCache = lockCache;
            this.name = name;
        }
    
        public IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis)
        {
            long numAttempts = ((timeoutMillis - 1) / retryIntervalMillis) + 1;
            for (long attempt = 0; attempt < numAttempts; attempt++)
            {
                // Check if the lock has been released or if it has expired
                long[] lockData = this.lockCache.GetAsync<long[]>(lockName).Result;
                bool isReleasedOrExpired = false;
                bool isDifferentThread = true;
                if (lockData == null)
                {
                    isReleasedOrExpired = true;
                }
                else
                {
                    long expiryTimeMillis = lockData[0];
                    long threadId = lockData[1];
                    isDifferentThread = threadId != Thread.CurrentThread.ManagedThreadId;
                    long currTimeMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    isReleasedOrExpired = isDifferentThread && currTimeMillis >= expiryTimeMillis;
                }

                // If there is a lock that is fathered by the same thread, return
                if (lockData != null && !isDifferentThread)
                {
                    lockData[2] = lockData[2] + 1;
                    this.lockCache.GetAndPutAsync<long[]>(lockName, lockData).Wait();
                    return new DisposableLock(this.lockCache, lockName);
                }
                else if (isReleasedOrExpired)
                {
                    // Create one and return
                    long currTimeMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    long expiryTimeMillis = currTimeMillis + lockTimeMillis;
                    lockData = new long[] { expiryTimeMillis, Thread.CurrentThread.ManagedThreadId, 1 };
                    this.lockCache.GetAndPutAsync<long[]>(lockName, lockData).Wait();
                    return new DisposableLock(this.lockCache, lockName);
                }

                // Could not acquire lock, so sleep and try again in the next iteration
                Thread.Sleep(retryIntervalMillis);
            }

            // Could not acquire the lock within the timeout, so throw an exception
            throw new TimeoutException($"Unable to acquire lock: {lockName}");
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        // Protected

        protected virtual void Dispose(bool disposing)
        {
            long[] lockData = this.lockCache.GetAsync<long[]>(this.name).Result;
            if (lockData != null)
            {
                if (lockData[2] > 1)
                {
                    lockData[2] = lockData[2] - 1;
                    this.lockCache.GetAndPutAsync<long[]>(this.name, lockData).Wait();
                }
                else
                {
                    this.lockCache.RemoveAsync<long[]>(this.name).Wait();
                }
            }
        }
    }
}
