// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// In-process non-reentrant lock for single-instance local-memory caches.
    /// Matches distributed providers: a second acquire while held (including on the same thread) waits or times out.
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
            retryIntervalMillis = DistributedLockArgs.Normalize(lockName, timeoutMillis, lockTimeMillis, retryIntervalMillis);

            LockState state = Locks.GetOrAdd(lockName, _ => new LockState());
            long deadlineMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + timeoutMillis;

            lock (state.Sync)
            {
                while (true)
                {
                    long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (!state.Held || nowMillis >= state.ExpiryTimeMillis)
                    {
                        state.Held = true;
                        state.ExpiryTimeMillis = nowMillis + lockTimeMillis;
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

        private void Release()
        {
            if (!Locks.TryGetValue(this.lockName, out LockState state))
            {
                return;
            }

            lock (state.Sync)
            {
                state.Held = false;
                state.ExpiryTimeMillis = 0;
                Monitor.PulseAll(state.Sync);
            }
        }

        private sealed class LockState
        {
            internal readonly object Sync = new object();

            internal bool Held { get; set; }

            internal long ExpiryTimeMillis { get; set; }
        }
    }
}
