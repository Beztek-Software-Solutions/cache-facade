// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class CacheEdgeCoverageTests
    {
        private Mock<ICacheProvider> cacheProvider;
        private Cache cache;

        [SetUp]
        public void SetUp()
        {
            this.cacheProvider = new Mock<ICacheProvider>(MockBehavior.Strict);
            string cacheName = Guid.NewGuid().ToString("N");
            this.cache = (Cache)CacheFactory.GetOrCreateCache(
                new CacheConfiguration(new TestCacheProviderConfiguration(this.cacheProvider.Object, cacheName, 300000), CacheType.NonPersistent));
            this.cache.CacheProvider = this.cacheProvider.Object;
        }

        [TearDown]
        public void TearDown()
        {
            this.cache?.Dispose();
        }

        [Test]
        public void PeekAsync_WrapsProviderException()
        {
            this.cacheProvider.Setup(m => m.Get<TestCacheable>(It.IsAny<string>())).Throws(new InvalidOperationException("boom"));
            Assert.ThrowsAsync<IOException>(async () => await this.cache.PeekAsync<TestCacheable>("k").ConfigureAwait(false));
        }

        [Test]
        public void WarmAsync_WrapsProviderException()
        {
            this.cacheProvider.Setup(m => m.Put(It.IsAny<string>(), It.IsAny<TestCacheable>())).Throws(new InvalidOperationException("boom"));
            Assert.ThrowsAsync<IOException>(async () =>
                await this.cache.WarmAsync("k", new TestCacheable("k", "v")).ConfigureAwait(false));
        }

        [Test]
        public async Task FlushAsync_WithKeyList_RemovesEachKey()
        {
            this.cacheProvider.Setup(m => m.Remove<TestCacheable>("a")).Returns((TestCacheable)null);
            this.cacheProvider.Setup(m => m.Remove<TestCacheable>("b")).Returns((TestCacheable)null);

            bool ok = await this.cache.FlushAsync<TestCacheable>(new[] { "a", "b" }).ConfigureAwait(false);

            Assert.That(ok, Is.True);
            this.cacheProvider.Verify(m => m.Remove<TestCacheable>("a"), Times.Once);
            this.cacheProvider.Verify(m => m.Remove<TestCacheable>("b"), Times.Once);
            this.cacheProvider.Verify(m => m.Clear(), Times.Never);
        }

        [Test]
        public async Task FlushKeyAsync_NullKey_ReturnsFalse()
        {
            bool ok = await this.cache.FlushKeyAsync<TestCacheable>(null).ConfigureAwait(false);
            Assert.That(ok, Is.False);
            this.cacheProvider.Verify(m => m.Remove<TestCacheable>(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task GetAndReplaceAsync_NonEtagEntity_PutsReplacement()
        {
            var existing = new TestCacheable("k", "old");
            var replacement = new TestCacheable("k", "new");
            this.cacheProvider.Setup(m => m.Get<TestCacheable>("k")).Returns(existing);
            this.cacheProvider.Setup(m => m.Put("k", replacement));

            TestCacheable result = await this.cache.GetAndReplaceAsync("k", replacement).ConfigureAwait(false);

            Assert.That(result, Is.EqualTo(existing));
            this.cacheProvider.Verify(m => m.Put("k", replacement), Times.Once);
        }

        [Test]
        public void GetAndReplaceAsync_WhenPutFails_RollsBackAndWraps()
        {
            var existing = new TestCacheable("k", "old");
            var replacement = new TestCacheable("k", "new");
            this.cacheProvider.Setup(m => m.Get<TestCacheable>("k")).Returns(existing);
            this.cacheProvider.Setup(m => m.Put("k", replacement)).Throws(new InvalidOperationException("put-failed"));
            this.cacheProvider.Setup(m => m.Put("k", existing)); // rollback

            Assert.ThrowsAsync<IOException>(async () =>
                await this.cache.GetAndReplaceAsync("k", replacement).ConfigureAwait(false));

            this.cacheProvider.Verify(m => m.Put("k", existing), Times.Once);
        }

        [Test]
        public async Task GetAndReplaceAsync_WriteThrough_CallsUpdate()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache writeThrough = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                var entity = new TestEtagCacheable(Guid.NewGuid().ToString(), "v1", TestUtil.GetNow(), TestUtil.GetNow(), EtagUtil.GenerateEtag());
                await writeThrough.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);

                var updated = new TestEtagCacheable(entity.Id, "v2", entity.CreatedDate, TestUtil.GetNow(), entity.Etag);
                TestEtagCacheable previous = await writeThrough.GetAndReplaceAsync(entity.Id, updated).ConfigureAwait(false);

                Assert.That(previous.Value, Is.EqualTo("v1"));
                TestEtagCacheable persisted = (TestEtagCacheable)await writeThrough.PersistenceService.GetByIdAsync(entity.Id).ConfigureAwait(false);
                Assert.That(persisted.Value, Is.EqualTo("v2"));
                Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.GreaterThan(0));
            }
            finally
            {
                writeThrough.Dispose();
            }
        }

        [Test]
        public void CacheFactory_WriteBehind_RequiresQueueConfiguration()
        {
            string cacheName = Guid.NewGuid().ToString("N");
            var persistence = new Mock<IPersistenceService>();
            var config = new CacheConfiguration(
                new LocalMemoryProviderConfiguration(cacheName, 300000),
                CacheType.WriteBehind,
                persistence.Object);

            Assert.Throws<ArgumentException>(() => CacheFactory.GetOrCreateCache(config));
        }

        [Test]
        public void DisposableLock_RejectsInvalidArgs_AndDoubleDispose()
        {
            var factory = new DisposableLock();
            Assert.Throws<ArgumentException>(() => factory.AcquireLock("", 10, 10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.AcquireLock("k", -1, 10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.AcquireLock("k", 10, 0, 1));

            using IDisposable handle = factory.AcquireLock("edge-lock", 50, 200, 0);
            handle.Dispose();
            Assert.DoesNotThrow(() => handle.Dispose());
        }

        [Test]
        public void DisposableLock_TimesOut_WhenHeld()
        {
            var factory = new DisposableLock();
            using (factory.AcquireLock("busy-lock", 100, 5_000, 5))
            {
                Assert.Throws<TimeoutException>(() => factory.AcquireLock("busy-lock", 20, 100, 5));
            }
        }
    }
}
