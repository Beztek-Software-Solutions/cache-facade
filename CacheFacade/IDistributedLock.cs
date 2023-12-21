// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Interface for a distributed lock
    /// </summary>
    public interface IDistributedLock
    {

        /// <summary>
        /// Attempts to hold a disposable lock by name within the specified timeout period, to be held for the given lock time. When a different process
        /// tries to acqire the lock, it blocks until the lock is acquired, and throws a TimeoutException if it cannot be otained in the timeout
        /// specified. However, multiple methods in the same call stack can call this method, as long as all calls are managed by the same thread.
        /// </summary>
        /// <param name="lockName">Name of the distributed lock.</param>
        /// <param name="timoeutMillis">The time in milliseconds to try to acquire the lock.</param>
        /// <param name="lockTimeMillis">The time in milliseconds to hold the lock. It will automatically be released after this time</param>
        /// <param name="retryIntervalMillis">the interval after which the lock acquisition should be retried</param>
        IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis);
    }
}
