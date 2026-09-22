// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Enyim.Caching;
    using Enyim.Caching.Memcached;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MemcachedLockTests
    {
        private Mock<IMemcachedClient> client;
        private MemcachedLock factory;

        [SetUp]
        public void SetUp()
        {
            this.client = new Mock<IMemcachedClient>(MockBehavior.Strict);
            this.factory = new MemcachedLock(this.client.Object, "orders:");
        }

        [Test]
        public void Acquire_UsesAddWithAtLeastOneSecondLease()
        {
            string token = null;
            this.client
                .Setup(c => c.Store(StoreMode.Add, "orders:lock:k1", It.IsAny<object>(), TimeSpan.FromSeconds(1)))
                .Callback<StoreMode, string, object, TimeSpan>((_, _, value, _) => token = (string)value)
                .Returns(true);

            CasResult<string> ignored = default;
            this.client
                .Setup(c => c.TryGetWithCas("orders:lock:k1", out ignored))
                .Returns((string key, out CasResult<string> result) =>
                {
                    result = new CasResult<string> { Result = token, Cas = 1 };
                    return true;
                });
            this.client
                .Setup(c => c.Cas(StoreMode.Set, "orders:lock:k1", It.IsAny<object>(), It.IsAny<DateTime>(), 1UL))
                .Returns(new CasResult<bool> { Result = true });

            // Sub-second lease must be clamped (Memcached second granularity).
            using IDisposable handle = this.factory.AcquireLock("k1", 50, lockTimeMillis: 200, retryIntervalMillis: 1);
            Assert.That(handle, Is.Not.Null);
        }

        [Test]
        public void Acquire_WhenBusy_TimesOut()
        {
            this.client
                .Setup(c => c.Store(StoreMode.Add, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()))
                .Returns(false);

            Assert.Throws<TimeoutException>(() =>
                this.factory.AcquireLock("busy", timeoutMillis: 30, lockTimeMillis: 1000, retryIntervalMillis: 5));
        }

        [Test]
        public void Dispose_CompareAndExpiresWithPastDate_WhenStillOwner()
        {
            string token = null;
            this.client
                .Setup(c => c.Store(StoreMode.Add, "orders:lock:k1", It.IsAny<object>(), It.IsAny<TimeSpan>()))
                .Callback<StoreMode, string, object, TimeSpan>((_, _, value, _) => token = (string)value)
                .Returns(true);

            CasResult<string> casResult = default;
            this.client
                .Setup(c => c.TryGetWithCas("orders:lock:k1", out casResult))
                .Returns((string key, out CasResult<string> result) =>
                {
                    result = new CasResult<string> { Result = token, Cas = 42 };
                    return true;
                });

            this.client
                .Setup(c => c.Cas(
                    StoreMode.Set,
                    "orders:lock:k1",
                    It.IsAny<object>(),
                    It.Is<DateTime>(d => d < DateTime.UtcNow),
                    42UL))
                .Returns(new CasResult<bool> { Result = true, Cas = 42 });

            using (this.factory.AcquireLock("k1", 50, 2000, 1))
            {
            }

            this.client.Verify(c => c.Remove(It.IsAny<string>()), Times.Never);
            this.client.Verify(
                c => c.Cas(StoreMode.Set, "orders:lock:k1", It.IsAny<object>(), It.Is<DateTime>(d => d < DateTime.UtcNow), 42UL),
                Times.Once);
        }

        [Test]
        public void Dispose_DoesNotExpire_WhenTokenNoLongerMatches()
        {
            this.client
                .Setup(c => c.Store(StoreMode.Add, "orders:lock:k1", It.IsAny<object>(), It.IsAny<TimeSpan>()))
                .Returns(true);

            CasResult<string> casResult = default;
            this.client
                .Setup(c => c.TryGetWithCas("orders:lock:k1", out casResult))
                .Returns((string key, out CasResult<string> result) =>
                {
                    result = new CasResult<string> { Result = "someone-else", Cas = 99 };
                    return true;
                });

            using (this.factory.AcquireLock("k1", 50, 2000, 1))
            {
            }

            this.client.Verify(
                c => c.Cas(It.IsAny<StoreMode>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<DateTime>(), It.IsAny<ulong>()),
                Times.Never);
        }
    }
}
