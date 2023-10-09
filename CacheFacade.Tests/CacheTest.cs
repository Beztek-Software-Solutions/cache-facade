// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache.Providers;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class CacheTest
    {
        private ICache Cache { get; set; }

        private Mock<ICacheProvider> CacheProvider { get; set; }

        private ILogger Logger { get; set; }

        [SetUp]
        public void SetUp()
        {
            this.CacheProvider = new Mock<ICacheProvider>();
            this.Logger = new LoggerFactory().CreateLogger("Setup");
            string cacheName = Guid.NewGuid().ToString();
            ICacheProviderConfiguration testCacheProviderConfiguration = new TestCacheProviderConfiguration(this.CacheProvider.Object, cacheName, 300000);
            this.Cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(new TestCacheProviderConfiguration(this.CacheProvider.Object, cacheName, 300000), CacheType.NonPersistent), default(ILogger));
            ((Cache)this.Cache).CacheProvider = this.CacheProvider.Object;
        }

        [Test]
        public async Task GetAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "get-result");
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(result);
            TestCacheable operationResult = await this.Cache.GetAsync<TestCacheable>("test-key").ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
        }

        [Test]
        public async Task GetAsyncExceptionTest()
        {
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).Throws(new Exception("dummy-exception"));
            Assert.ThrowsAsync<IOException>(async () => await this.Cache.GetAsync<TestCacheable>("test-key").ConfigureAwait(false));
        }

        [Test]
        public async Task GetAndPutAsyncKeyExistsTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputifabsentasync-result");
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(result);
            TestCacheable operationResult = await this.Cache.GetAndPutIfAbsentAsync<TestCacheable>(result.Id, result).ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
        }

        [Test]
        public async Task GetAndPutAsyncKeyNotExistsTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputifabsentasync-result");
            TestCacheable operationResult = await this.Cache.GetAndPutIfAbsentAsync<TestCacheable>(result.Id, result).ConfigureAwait(false);
            Assert.IsNull(operationResult);
        }

        [Test]
        public async Task GetAndPutAsyncExceptionTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputifabsentasync-result");
            this.CacheProvider.Setup(m => m.PutAsync(It.IsAny<string>(), It.IsAny<TestCacheable>())).Throws(new Exception("dummy-exception"));
            Assert.ThrowsAsync<IOException>(async () => await this.Cache.GetAndPutIfAbsentAsync<TestCacheable>(result.Id, result).ConfigureAwait(false));
        }

        [Test]
        public async Task RemoveAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputasync-result");
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(result);
            this.CacheProvider.Setup(m => m.RemoveAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(result);
            TestCacheable operationResult = await this.Cache.RemoveAsync<TestCacheable>("test-key").ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
        }

        [Test]
        public async Task RemoveAsyncExceptionTest()
        {
            this.CacheProvider.Setup(m => m.RemoveAsync<TestCacheable>(It.IsAny<string>())).Throws(new Exception("dummy-exception"));
            Assert.ThrowsAsync<IOException>(async () => await this.Cache.RemoveAsync<TestCacheable>("test-key").ConfigureAwait(false));
        }

        [Test]
        public async Task FlushAlleAsyncTest()
        {
            this.CacheProvider.Setup(m => m.ClearAsync()).ReturnsAsync(true);
            bool result = await this.Cache.FlushAsync<object>().ConfigureAwait(false);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task FlushAlleAsyncExceptionTest()
        {
            this.CacheProvider.Setup(m => m.ClearAsync()).Throws(new Exception("dummy-exception"));
            Assert.ThrowsAsync<IOException>(async () => await this.Cache.FlushAsync<object>().ConfigureAwait(false));
        }

        [Test]
        public async Task FlushSpecificeAsyncTest()
        {
            TestCacheable cacheable = new TestCacheable("test-key", "getandputasync-result");
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(cacheable);
            this.CacheProvider.Setup(m => m.RemoveAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(cacheable);
            bool result = await this.Cache.FlushKeyAsync<TestCacheable>(cacheable.Id).ConfigureAwait(false);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task FlushAsyncExceptionTest()
        {
            TestCacheable cacheable = new TestCacheable("test-key", "getandputasync-result");
            this.CacheProvider.Setup(m => m.GetAsync<TestCacheable>(It.IsAny<string>())).ReturnsAsync(cacheable);
            this.CacheProvider.Setup(m => m.RemoveAsync<TestCacheable>(It.IsAny<string>())).Throws(new IOException("dummy-exception"));
            Assert.ThrowsAsync<IOException>(async () => await this.Cache.FlushKeyAsync<TestCacheable>(cacheable.Id).ConfigureAwait(false));
        }
    }
}
