// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Hazelcast.DistributedObjects;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class HazelcastLockTests
    {
        private Mock<IHMap<string, byte[]>> lockMap;
        private HazelcastLock factory;

        [SetUp]
        public void SetUp()
        {
            this.lockMap = new Mock<IHMap<string, byte[]>>(MockBehavior.Strict);
            this.factory = new HazelcastLock(this.lockMap.Object);
        }

        [Test]
        public void Ctor_RejectsNullMap()
        {
            Assert.Throws<ArgumentNullException>(() => new HazelcastLock(null));
        }

        [Test]
        public void Acquire_UsesPutIfAbsent()
        {
            byte[] capturedToken = null;
            this.lockMap
                .Setup(m => m.PutIfAbsentAsync("k1", It.IsAny<byte[]>(), It.IsAny<TimeSpan>()))
                .Returns((string _, byte[] token, TimeSpan __) =>
                {
                    capturedToken = token;
                    return Task.FromResult<byte[]>(null);
                });
            this.lockMap
                .Setup(m => m.RemoveAsync("k1", It.IsAny<byte[]>()))
                .Returns(Task.FromResult(true));

            using IDisposable handle = this.factory.AcquireLock("k1", 50, 300, 1);
            Assert.That(handle, Is.Not.Null);
            Assert.That(capturedToken, Is.Not.Null);
            Assert.That(Encoding.UTF8.GetString(capturedToken).Length, Is.EqualTo(32));
        }

        [Test]
        public void Acquire_WhenBusyUntilTimeout_Throws()
        {
            this.lockMap
                .Setup(m => m.PutIfAbsentAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<TimeSpan>()))
                .Returns(Task.FromResult(Encoding.UTF8.GetBytes("held")));

            Assert.Throws<TimeoutException>(() => this.factory.AcquireLock("busy", 20, 100, 5));
        }

        [Test]
        public void Dispose_CompareAndRemoves_WhenStillOwner()
        {
            byte[] token = null;
            this.lockMap
                .Setup(m => m.PutIfAbsentAsync("k1", It.IsAny<byte[]>(), It.IsAny<TimeSpan>()))
                .Returns((string _, byte[] t, TimeSpan __) =>
                {
                    token = t;
                    return Task.FromResult<byte[]>(null);
                });
            this.lockMap
                .Setup(m => m.RemoveAsync("k1", It.IsAny<byte[]>()))
                .Returns(Task.FromResult(true));

            using (this.factory.AcquireLock("k1", 50, 300, 1))
            {
            }

            this.lockMap.Verify(m => m.RemoveAsync("k1", It.Is<byte[]>(b => ReferenceEquals(b, token))), Times.Once);
        }

        [Test]
        public void Dispose_SwallowsReleaseFailures()
        {
            this.lockMap
                .Setup(m => m.PutIfAbsentAsync("k1", It.IsAny<byte[]>(), It.IsAny<TimeSpan>()))
                .Returns(Task.FromResult<byte[]>(null));
            this.lockMap
                .Setup(m => m.RemoveAsync("k1", It.IsAny<byte[]>()))
                .ThrowsAsync(new InvalidOperationException("gone"));

            Assert.DoesNotThrow(() =>
            {
                using (this.factory.AcquireLock("k1", 50, 300, 1))
                {
                }
            });
        }

        [Test]
        public void Dispose_OnFactoryHandle_IsNoOp()
        {
            Assert.DoesNotThrow(() => this.factory.Dispose());
            Assert.DoesNotThrow(() => this.factory.Dispose());
        }
    }
}
