// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Acquires a named disposable lock (Redis RedLock or Redis token lock for RESP subsets,
    /// Hazelcast/Memcached token locks, or an in-process non-reentrant lock for local memory).
    /// </summary>
    public interface IDistributedLock
    {

        /// <summary>
        /// Attempts to hold a disposable lock by name within the specified timeout period, to be held for the given lock time.
        /// When a different process or thread tries to acquire the lock, it blocks until the lock is acquired, and throws a
        /// <see cref="TimeoutException"/> if it cannot be obtained in the timeout specified.
        /// Locks are non-reentrant (including local memory): a second acquire while held waits or times out.
        /// </summary>
        /// <param name="lockName">Name of the distributed lock.</param>
        /// <param name="timeoutMillis">Time in milliseconds to try to acquire the lock.</param>
        /// <param name="lockTimeMillis">Time in milliseconds to hold the lock before automatic release.</param>
        /// <param name="retryIntervalMillis">Interval between acquisition retries.</param>
        /// <returns>An <see cref="IDisposable"/> that releases the lock when disposed.</returns>
        IDisposable AcquireLock(string lockName, long timeoutMillis, long lockTimeMillis, int retryIntervalMillis);
    }
}
