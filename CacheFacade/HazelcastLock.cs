// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Hazelcast.DistributedObjects;

    /// <summary>
    /// Distributed lock using Hazelcast map <c>PutIfAbsent</c> with TTL (token-based, async-safe).
    /// Prefer this over map key locks so dispose after <c>await</c> does not depend on thread affinity.
    /// </summary>
    internal class HazelcastLock : IDisposable, IDistributedLock
    {
        private readonly IHMap<string, byte[]> lockMap;
        private readonly string lockKey;
        private readonly byte[] token;
        private int disposed;

        /// <summary>Factory used by <see cref="Cache"/>.</summary>
        internal HazelcastLock(IHMap<string, byte[]> lockMap)
        {
            this.lockMap = lockMap ?? throw new ArgumentNullException(nameof(lockMap));
            this.lockKey = null;
            this.token = null;
        }

        private HazelcastLock(IHMap<string, byte[]> lockMap, string lockKey, byte[] token)
        {
            this.lockMap = lockMap;
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

            TimeSpan lease = TimeSpan.FromMilliseconds(lockTimeMillis);
            byte[] acquiredToken = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"));
            long deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMillis;

            while (true)
            {
                // PutIfAbsent returns null when the put succeeded (no previous value).
                byte[] previous = Await(this.lockMap.PutIfAbsentAsync(lockName, acquiredToken, lease));
                if (previous == null)
                {
                    return new HazelcastLock(this.lockMap, lockName, acquiredToken);
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
                // Compare-and-remove: only deletes if the value still matches our token.
                Await(this.lockMap.RemoveAsync(this.lockKey, this.token));
            }
            catch
            {
                // Best-effort; lease TTL will release if unlock fails.
            }

            GC.SuppressFinalize(this);
        }

        private static T Await<T>(Task<T> task) => task.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
