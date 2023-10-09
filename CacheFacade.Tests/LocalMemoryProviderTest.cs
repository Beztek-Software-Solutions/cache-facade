// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System.Threading.Tasks;

    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class LocalMemoryProviderTest
    {
        private LocalMemoryProvider localMemoryCacheProvider;

        [SetUp]
        public void SetUp()
        {
            this.localMemoryCacheProvider = new LocalMemoryProvider(new LocalMemoryProviderConfiguration("LocalMemoryProviderTest", 300000));
        }

        [Test]
        public async Task GetAsyncTest()
        {
            TestCacheable result = new TestCacheable("GetAsyncTest-key", "get-result");
            await this.localMemoryCacheProvider.PutAsync(result.Id, result).ConfigureAwait(false);
            TestCacheable operationResult = await this.localMemoryCacheProvider.GetAsync<TestCacheable>(result.Id).ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
        }

        [Test]
        public async Task GetAsyncKeyNotExistTest()
        {
            TestCacheable operationResult = await this.localMemoryCacheProvider.GetAsync<TestCacheable>("GetAsyncKeyNotExistTest-key").ConfigureAwait(false);
            Assert.IsNull(operationResult);
        }

        [Test]
        public async Task PutAsyncTest()
        {
            TestCacheable result = new TestCacheable("PutAsyncTest-key", "putasync-result");
            await this.localMemoryCacheProvider.PutAsync<TestCacheable>(result.Id, result).ConfigureAwait(false);
            Assert.AreEqual(result, await this.localMemoryCacheProvider.GetAsync<TestCacheable>(result.Id).ConfigureAwait(false));
        }

        [Test]
        public async Task RemoveAsyncKeyExistsTest()
        {
            TestCacheable result = new TestCacheable("RemoveAsyncKeyExistsTest-key", "getandputasync-result");
            await this.localMemoryCacheProvider.PutAsync(result.Id, result).ConfigureAwait(false);
            TestCacheable operationResult = await this.localMemoryCacheProvider.RemoveAsync<TestCacheable>("RemoveAsyncKeyExistsTest-key").ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
            Assert.IsNull(await this.localMemoryCacheProvider.GetAsync<TestCacheable>(result.Id).ConfigureAwait(false));
        }

        [Test]
        public async Task RemoveAsyncKeyNotExistsTest()
        {
            TestCacheable operationResult = await this.localMemoryCacheProvider.RemoveAsync<TestCacheable>("RemoveAsyncKeyNotExistsTest-key").ConfigureAwait(false);
            Assert.IsNull(operationResult);
            Assert.IsNull(await this.localMemoryCacheProvider.GetAsync<TestCacheable>("RemoveAsyncKeyNotExistsTest-key").ConfigureAwait(false));
        }

        [Test]
        public async Task ClearAsyncTest()
        {
            TestCacheable result = new TestCacheable("ClearAsyncTest-key", "ClearAsyncTest-result");
            await this.localMemoryCacheProvider.PutAsync(result.Id, result).ConfigureAwait(false);
            Assert.AreEqual(result, await this.localMemoryCacheProvider.GetAsync<TestCacheable>("ClearAsyncTest-key").ConfigureAwait(false));
            await this.localMemoryCacheProvider.ClearAsync().ConfigureAwait(false);
            Assert.IsNull(await this.localMemoryCacheProvider.GetAsync<TestCacheable>("ClearAsyncTest-key").ConfigureAwait(false));
        }
    }
}
