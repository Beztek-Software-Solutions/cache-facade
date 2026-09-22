// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Moq;
    using NUnit.Framework;
    using StackExchange.Redis;

    [TestFixture]
    public class RedisTokenLockTests
    {
        private Mock<IDatabase> database;
        private RedisTokenLock factory;

        [SetUp]
        public void SetUp()
        {
            this.database = new Mock<IDatabase>(MockBehavior.Strict);
            this.factory = new RedisTokenLock(this.database.Object, "orders");
        }

        [Test]
        public void Acquire_UsesSetNxWithPrefix()
        {
            this.database
                .Setup(d => d.StringSet("orders:lock:k1", It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists))
                .Returns(true);
            this.database
                .Setup(d => d.LockRelease("orders:lock:k1", It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .Returns(true);

            using IDisposable handle = this.factory.AcquireLock("k1", 50, 300, 1);
            Assert.That(handle, Is.Not.Null);
        }

        [Test]
        public void Acquire_WhenBusyUntilTimeout_Throws()
        {
            this.database
                .Setup(d => d.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists))
                .Returns(false);

            Assert.Throws<TimeoutException>(() => this.factory.AcquireLock("busy", 20, 100, 5));
        }

        [Test]
        public void Dispose_UsesLockRelease_WithoutBlindDeleteFallback()
        {
            this.database
                .Setup(d => d.StringSet("orders:lock:k1", It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists))
                .Returns(true);
            this.database
                .Setup(d => d.LockRelease("orders:lock:k1", It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
                .Throws(new NotSupportedException("no WATCH/CAD"));

            using (this.factory.AcquireLock("k1", 50, 300, 1))
            {
            }

            this.database.Verify(d => d.LockRelease("orders:lock:k1", It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
            // Must not fall back to StringGet/KeyDelete (could remove another owner's lock).
            this.database.Verify(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
            this.database.Verify(d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
        }
    }
}
