// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Moq;
    using NUnit.Framework;
    using RedLockNet;

    [TestFixture]
    public class RedisLockTests
    {
        [Test]
        public void Ctor_RejectsNullRedLockFactory()
        {
            Assert.Throws<ArgumentNullException>(() => new RedisLock((RedLockNet.SERedis.RedLockFactory)null));
        }

        [Test]
        public void Ctor_RejectsNullFactoryDelegate()
        {
            Assert.Throws<ArgumentNullException>(() => new RedisLock((Func<string, TimeSpan, TimeSpan, TimeSpan, IRedLock>)null));
        }

        [Test]
        public void Acquire_WhenAcquired_ReturnsHandle()
        {
            var redLock = new Mock<IRedLock>(MockBehavior.Strict);
            redLock.SetupGet(r => r.IsAcquired).Returns(true);
            redLock.Setup(r => r.Dispose());

            var sut = new RedisLock((_, __, ___, ____) => redLock.Object);
            using IDisposable handle = sut.AcquireLock("k1", 50, 300, 0);
            Assert.That(handle, Is.SameAs(redLock.Object));
        }

        [Test]
        public void Acquire_WhenNotAcquired_DisposesAndThrows()
        {
            var redLock = new Mock<IRedLock>(MockBehavior.Strict);
            redLock.SetupGet(r => r.IsAcquired).Returns(false);
            redLock.Setup(r => r.Dispose());

            var sut = new RedisLock((_, __, ___, ____) => redLock.Object);
            Assert.Throws<TimeoutException>(() => sut.AcquireLock("busy", 50, 300, 1));
            redLock.Verify(r => r.Dispose(), Times.Once);
        }

        [Test]
        public void Acquire_RejectsInvalidArgs()
        {
            var sut = new RedisLock((_, __, ___, ____) => Mock.Of<IRedLock>());
            Assert.Throws<ArgumentException>(() => sut.AcquireLock("", 50, 300, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.AcquireLock("k", -1, 300, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => sut.AcquireLock("k", 50, 0, 1));
        }
    }
}
