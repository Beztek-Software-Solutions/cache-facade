// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Acquires a named disposable lock (Redis RedLock for distributed caches, or an in-process
    /// reentrant lock backed by a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
    /// for single-instance local-memory caches).
    /// </summary>
    public interface IDistributedLock
    {

        /// <summary>
        /// Attempts to hold a disposable lock by name within the specified timeout period, to be held for the given lock time.
        /// When a different process or thread tries to acquire the lock, it blocks until the lock is acquired, and throws a
        /// <see cref="TimeoutException"/> if it cannot be obtained in the timeout specified. Multiple methods in the same
        /// call stack on the same thread may re-enter the same lock (local memory lock implementation).
        /// </summary>
        /// <param name="lockName">Name of the distributed lock.</param>
        /// <param name="timeoutMillis">Time in milliseconds to try to acquire the lock.</param>
        /// <param name="lockTimeMillis">Time in milliseconds to hold the lock before automatic release.</param>
        /// <param name="retryIntervalMillis">Interval between acquisition retries.</param>
        /// <returns>An <see cref="IDisposable"/> that releases the lock when disposed.</returns>
        IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis);
    }
}
