// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using Moq;
    using NUnit.Framework;
    using StackExchange.Redis;

    [TestFixture]
    public class RedisProviderEdgeTests
    {
        [Test]
        public void Clear_WithoutMultiplexer_ReturnsFalse()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Clear(), Is.False);
        }

        [Test]
        public void Remove_WhenKeyMissing_ReturnsDefault()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database.Setup(d => d.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(false);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Remove<TestCacheable>("missing"), Is.Null);
        }

        [Test]
        public void Remove_WhenValueNull_ReturnsDefaultWithoutDelete()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database.Setup(d => d.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(RedisValue.Null);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Remove<TestCacheable>("k"), Is.Null);
            database.Verify(d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public void BuildConnectionKey_IncludesIdentityFields()
        {
            var config = new RedisProviderConfiguration(
                "127.0.0.1:6379",
                "secret",
                "orders",
                useSSL: true,
                abortConnection: false,
                timeToLiveMillis: 1000);
            config.Options = "connectTimeout=100";
            string key = RedisProvider.BuildConnectionKey(config);
            Assert.That(key, Does.Contain("127.0.0.1:6379"));
            Assert.That(key, Does.Contain("secret"));
            Assert.That(key, Does.Contain("True"));
            Assert.That(key, Does.Contain("False"));
            Assert.That(key, Does.Contain("connectTimeout=100"));
        }

        [Test]
        public void Multiplexer_WithoutConnectionKey_UsesDatabaseMultiplexer()
        {
            var multiplexer = new Mock<IConnectionMultiplexer>();
            var database = new Mock<IDatabase>();
            database.SetupGet(d => d.Multiplexer).Returns(multiplexer.Object);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Multiplexer, Is.SameAs(multiplexer.Object));
        }
    }
}
