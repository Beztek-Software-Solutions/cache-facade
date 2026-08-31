// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// In-process reentrant lock for single-instance local-memory caches.
    /// Lock state is held in a static <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by lock name.
    /// Dispose is thread-agnostic so <c>await</c> / <c>ConfigureAwait(false)</c> resumptions still release.
    /// </summary>
    internal class DisposableLock : IDisposable, IDistributedLock
    {
        private static readonly ConcurrentDictionary<string, LockState> Locks =
            new ConcurrentDictionary<string, LockState>(StringComparer.Ordinal);

        private readonly string lockName;
        private int disposed;

        /// <summary>Factory entry point used by <see cref="Cache"/> (not an acquired handle).</summary>
        internal DisposableLock()
        {
            this.lockName = null;
        }

        private DisposableLock(string lockName)
        {
            this.lockName = lockName;
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

            int currentThreadId = Environment.CurrentManagedThreadId;
            LockState state = Locks.GetOrAdd(lockName, _ => new LockState());
            long deadlineMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMillis;

            lock (state.Sync)
            {
                while (true)
                {
                    long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (TryAcquireLocked(state, currentThreadId, nowMillis, lockTimeMillis))
                    {
                        return new DisposableLock(lockName);
                    }

                    long remainingMillis = deadlineMillis - nowMillis;
                    if (remainingMillis <= 0)
                    {
                        break;
                    }

                    int waitMillis = (int)Math.Min(remainingMillis, retryIntervalMillis);
                    Monitor.Wait(state.Sync, waitMillis);
                }
            }

            throw new TimeoutException($"Unable to acquire lock: {lockName}");
        }

        public void Dispose()
        {
            if (this.lockName == null || Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            this.Release();
            GC.SuppressFinalize(this);
        }

        private static bool TryAcquireLocked(LockState state, int currentThreadId, long nowMillis, long lockTimeMillis)
        {
            if (state.RefCount == 0 || nowMillis >= state.ExpiryTimeMillis)
            {
                state.OwnerThreadId = currentThreadId;
                state.RefCount = 1;
                state.ExpiryTimeMillis = nowMillis + lockTimeMillis;
                return true;
            }

            if (state.OwnerThreadId == currentThreadId)
            {
                state.RefCount++;
                state.ExpiryTimeMillis = nowMillis + lockTimeMillis;
                return true;
            }

            return false;
        }

        private void Release()
        {
            if (!Locks.TryGetValue(this.lockName, out LockState state))
            {
                return;
            }

            lock (state.Sync)
            {
                if (state.RefCount == 0)
                {
                    return;
                }

                state.RefCount--;
                if (state.RefCount == 0)
                {
                    state.OwnerThreadId = 0;
                    state.ExpiryTimeMillis = 0;
                }

                Monitor.PulseAll(state.Sync);
            }
        }

        private sealed class LockState
        {
            internal readonly object Sync = new object();

            internal int OwnerThreadId { get; set; }

            internal int RefCount { get; set; }

            internal long ExpiryTimeMillis { get; set; }
        }
    }
}
