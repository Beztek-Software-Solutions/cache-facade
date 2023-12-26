// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using Moq;
    using NUnit.Framework;
    using StackExchange.Redis;

    [TestFixture]
    public class RedisProviderTests
    {
        private RedisProvider redisCache;
        private Mock<IDatabase> cacheDatabase;

        [SetUp]
        public void SetUp()
        {
            this.cacheDatabase = new Mock<IDatabase>();
            this.redisCache = new RedisProvider(this.cacheDatabase.Object);
        }

        [Test]
        public async Task GetAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "get-result");
            this.cacheDatabase.Setup(m => m.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(Serialize(result));
            TestCacheable operationResult = await this.redisCache.GetAsync<TestCacheable>("test-key").ConfigureAwait(false);
            Assert.IsTrue(object.Equals(result, operationResult));
        }

        [Test]
        public async Task PutAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputasync-result");
            this.cacheDatabase.Setup(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).Returns(true);
            await this.redisCache.PutAsync<TestCacheable>(result.Id, result).ConfigureAwait(false);

            this.cacheDatabase.Verify(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public async Task PutAsyncExceptionTest()
        {
            await Task.Run(async () => {
                TestCacheable result = new TestCacheable("test-key", "getandputasync-result");
                this.cacheDatabase.Setup(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).Returns(false);
                Assert.ThrowsAsync<IOException>(async () => await this.redisCache.PutAsync<TestCacheable>(result.Id, result));
            }).ConfigureAwait(false);
        }

        [Test]
        public async Task RemoveAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "remove-result");
            this.cacheDatabase.Setup(m => m.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            this.cacheDatabase.Setup(m => m.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(Serialize(result));
            TestCacheable operationResult = await this.redisCache.RemoveAsync<TestCacheable>("test-key").ConfigureAwait(false);
            Assert.AreEqual(result, operationResult);
        }

        [Test]
        public async Task ClearAsyncTest()
        {
            // Note: we cannot mock this method, because some objects are not interfaces or sealed.
        }

        private static string Serialize(TestCacheable obj)
        {
            try
            {
                string result = SerializationUtil.ByteToString(SerializationUtil.Serialize(RedisProvider.SerType, obj));
                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}