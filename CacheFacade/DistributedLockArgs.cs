// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Shared argument checks for <see cref="IDistributedLock.AcquireLock"/>.
    /// </summary>
    internal static class DistributedLockArgs
    {
        /// <summary>
        /// Validates acquire parameters and returns a positive retry interval (defaults to 1 when ≤ 0).
        /// </summary>
        internal static int Normalize(
            string lockName,
            long timeoutMillis,
            long lockTimeMillis,
            int retryIntervalMillis)
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

            return retryIntervalMillis <= 0 ? 1 : retryIntervalMillis;
        }
    }
}
