// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
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
        public void GetTest()
        {
            TestCacheable result = new TestCacheable("GetAsyncTest-key", "get-result");
            this.localMemoryCacheProvider.Put(result.Id, result);
            TestCacheable operationResult = this.localMemoryCacheProvider.Get<TestCacheable>(result.Id);
            Assert.That(result, Is.EqualTo(operationResult));
        }

        [Test]
        public void GetKeyNotExistTest()
        {
            TestCacheable operationResult = this.localMemoryCacheProvider.Get<TestCacheable>("GetAsyncKeyNotExistTest-key");
            Assert.That(operationResult, Is.Null);
        }

        [Test]
        public void PutTest()
        {
            TestCacheable result = new TestCacheable("PutAsyncTest-key", "putasync-result");
            this.localMemoryCacheProvider.Put<TestCacheable>(result.Id, result);
            Assert.That(result,  Is.EqualTo(this.localMemoryCacheProvider.Get<TestCacheable>(result.Id)));
        }

        [Test]
        public void RemoveKeyExistsTest()
        {
            TestCacheable result = new TestCacheable("RemoveAsyncKeyExistsTest-key", "getandputasync-result");
            this.localMemoryCacheProvider.Put(result.Id, result);
            TestCacheable operationResult = this.localMemoryCacheProvider.Remove<TestCacheable>("RemoveAsyncKeyExistsTest-key");
            Assert.That(result,  Is.EqualTo(operationResult));
            Assert.That(this.localMemoryCacheProvider.Get<TestCacheable>(result.Id), Is.Null);
        }

        [Test]
        public void RemoveKeyNotExistsTest()
        {
            TestCacheable operationResult = this.localMemoryCacheProvider.Remove<TestCacheable>("RemoveAsyncKeyNotExistsTest-key");
            Assert.That(operationResult, Is.Null);
            Assert.That(this.localMemoryCacheProvider.Get<TestCacheable>("RemoveAsyncKeyNotExistsTest-key"), Is.Null);
        }

        [Test]
        public void ClearTest()
        {
            TestCacheable result = new TestCacheable("ClearAsyncTest-key", "ClearAsyncTest-result");
            this.localMemoryCacheProvider.Put(result.Id, result);
            Assert.That(result,  Is.EqualTo(this.localMemoryCacheProvider.Get<TestCacheable>("ClearAsyncTest-key")));
            this.localMemoryCacheProvider.Clear();
            Assert.That(this.localMemoryCacheProvider.Get<TestCacheable>("ClearAsyncTest-key"), Is.Null);
        }
    }
}
