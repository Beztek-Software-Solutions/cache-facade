// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    /// <summary>
    /// Cross-provider live suite. Discovered only when <c>CACHEFACADE_LIVE_PROVIDERS</c> is set.
    /// <para>
    /// One provider: <c>CACHEFACADE_LIVE_PROVIDERS=redis</c><br/>
    /// All providers: <c>CACHEFACADE_LIVE_PROVIDERS=all</c>
    /// </para>
    /// </summary>
    [TestFixtureSource(typeof(LiveProviderFixtureSource), nameof(LiveProviderFixtureSource.Providers))]
    [Category("Live")]
    public class LiveProviderTests
    {
        private readonly CacheProviderType _providerType;
        private LiveProviderHost _host;

        public LiveProviderTests(CacheProviderType providerType)
        {
            _providerType = providerType;
        }

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                _host = await LiveProviderHost.StartAsync(_providerType).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (IsImageOrStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping {_providerType}: container image/startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_host, Is.Not.Null);
        }

        [Test]
        public async Task PutGetRemove_RoundTrips()
        {
            ICache cache = _host.Cache;
            string key = "rt-" + Guid.NewGuid().ToString("N");
            var value = new TestCacheable(key, "live-value");

            await cache.GetAndPutAsync(key, value).ConfigureAwait(false);
            TestCacheable got = await cache.GetAsync<TestCacheable>(key).ConfigureAwait(false);
            Assert.That(got, Is.EqualTo(value));

            TestCacheable removed = await cache.RemoveAsync<TestCacheable>(key).ConfigureAwait(false);
            Assert.That(removed, Is.EqualTo(value));
            Assert.That(await cache.GetAsync<TestCacheable>(key).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task Get_MissingKey_ReturnsNull()
        {
            TestCacheable missing = await _host.Cache.GetAsync<TestCacheable>("missing-" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            Assert.That(missing, Is.Null);
        }

        [Test]
        public async Task PeekAndWarm_DoNotRequirePersistence()
        {
            ICache cache = _host.Cache;
            string key = "warm-" + Guid.NewGuid().ToString("N");
            var value = new TestCacheable(key, "warmed");

            await cache.WarmAsync(key, value).ConfigureAwait(false);
            TestCacheable peeked = await cache.PeekAsync<TestCacheable>(key).ConfigureAwait(false);
            Assert.That(peeked, Is.EqualTo(value));
        }

        [Test]
        public void AcquireLock_ExclusiveAndReleases()
        {
            ICache cache = _host.Cache;
            string lockName = "lock-" + Guid.NewGuid().ToString("N");

            using (IDisposable held = cache.AcquireLock(lockName, timeoutMillis: 1000, lockTimeMillis: 3000, retryIntervalMillis: 20))
            {
                Assert.That(held, Is.Not.Null);
                // Other thread must time out while this thread holds the lock (locks are non-reentrant).
                Exception otherThreadError = null;
                Task.Run(() =>
                {
                    try
                    {
                        cache.AcquireLock(lockName, timeoutMillis: 150, lockTimeMillis: 1000, retryIntervalMillis: 20);
                    }
                    catch (Exception ex)
                    {
                        otherThreadError = ex;
                    }
                }).Wait();
                Assert.That(otherThreadError, Is.InstanceOf<TimeoutException>());
            }

            // Memcached release CAS-expires the key immediately when still owned.
            using IDisposable again = cache.AcquireLock(lockName, timeoutMillis: 1000, lockTimeMillis: 3000, retryIntervalMillis: 20);
            Assert.That(again, Is.Not.Null);
        }

        [Test]
        public async Task FlushAsync_ClearsProviderContents()
        {
            // Memcached has no scoped Clear — skip full flush.
            if (_providerType == CacheProviderType.Memcached)
            {
                Assert.Ignore("Memcached Clear is not supported; use FlushKeyAsync / FlushAsync(keys).");
            }

            ICache cache = _host.Cache;
            string key = "flush-" + Guid.NewGuid().ToString("N");
            var value = new TestCacheable(key, "to-clear");

            await cache.GetAndPutAsync(key, value).ConfigureAwait(false);
            Assert.That(await cache.GetAsync<TestCacheable>(key).ConfigureAwait(false), Is.EqualTo(value));

            bool cleared = await cache.FlushAsync<TestCacheable>().ConfigureAwait(false);
            Assert.That(cleared, Is.True);
            Assert.That(await cache.GetAsync<TestCacheable>(key).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public void ConcurrentLock_OnlyOneHolder()
        {
            ICache cache = _host.Cache;
            string lockName = "contend-" + Guid.NewGuid().ToString("N");
            var state = new HolderState();
            using var start = new ManualResetEventSlim(false);

            Task t1 = Task.Run(() => Hold(cache, lockName, start, state));
            Task t2 = Task.Run(() => Hold(cache, lockName, start, state));
            start.Set();
            Task.WaitAll(t1, t2);
            Assert.That(state.MaxConcurrent, Is.EqualTo(1));
        }

        private static void Hold(ICache cache, string lockName, ManualResetEventSlim start, HolderState state)
        {
            start.Wait();
            using (cache.AcquireLock(lockName, timeoutMillis: 5000, lockTimeMillis: 5000, retryIntervalMillis: 25))
            {
                lock (state.Gate)
                {
                    state.CurrentHolders++;
                    state.MaxConcurrent = Math.Max(state.MaxConcurrent, state.CurrentHolders);
                }

                Thread.Sleep(150);

                lock (state.Gate)
                {
                    state.CurrentHolders--;
                }
            }
        }

        private sealed class HolderState
        {
            public readonly object Gate = new object();
            public int CurrentHolders;
            public int MaxConcurrent;
        }

        private static bool IsImageOrStartupFailure(Exception ex)
        {
            for (Exception cur = ex; cur != null; cur = cur.InnerException)
            {
                string msg = cur.Message ?? "";
                if (msg.Contains("pull access denied", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such image", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Unable to find image", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
