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
        public void GetTest()
        {
            TestCacheable result = new TestCacheable("test-key", "get-result");
            this.cacheDatabase.Setup(m => m.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(Serialize(result));
            TestCacheable operationResult = this.redisCache.Get<TestCacheable>("test-key");
            Assert.That(result,  Is.EqualTo(operationResult));
        }

        [Test]
        public void PutAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputasync-result");
            this.cacheDatabase.Setup(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).Returns(true);
            
            this.redisCache.Put<TestCacheable>(result.Id, result);

            this.cacheDatabase.Verify(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Test]
        public void PutAsyncExceptionTest()
        {
            TestCacheable result = new TestCacheable("test-key", "getandputasync-result");
            this.cacheDatabase.Setup(m => m.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).Returns(false);
            Assert.Throws<IOException>(() => this.redisCache.Put<TestCacheable>(result.Id, result));
        }

        [Test]
        public Task RemoveAsyncTest()
        {
            TestCacheable result = new TestCacheable("test-key", "remove-result");
            this.cacheDatabase.Setup(m => m.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            this.cacheDatabase.Setup(m => m.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(Serialize(result));
            TestCacheable operationResult = this.redisCache.Remove<TestCacheable>("test-key");
            Assert.That(result,  Is.EqualTo(operationResult));
            return Task.CompletedTask;
        }

        [Test]
        public void ClearAsyncTest()
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