// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class LockTest
    {
        private ICache testLockCache;

        [SetUp]
        public void SetUp()
        {
            ICacheProviderConfiguration lockProviderConfiguration = new LocalMemoryProviderConfiguration("localCache", 300000);
            this.testLockCache = CacheFactory.GetOrCreateCache(new CacheConfiguration(lockProviderConfiguration, CacheType.NonPersistent));
        }

        [Test]
        public void AcquireTest()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test0Lock", 50, 300))
            {
                Assert.IsNotNull(lock1);
            }
        }

        [Test]
        public void TimeAutoReleaseTest()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test1Lock", 50, 300))
            {
                Task.Run(() => {
                    // Wait for lock to expire
                    Thread.Sleep(301);
                    // Lock has expired
                    IDisposable lock2 = this.testLockCache.AcquireLock("test1Lock", 50, 300);
                    Assert.IsNotNull(lock2);
                }).Wait();
            }
        }

        [Test]
        public void LockWaitTest()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test2Lock", 50, 300))
            {
                Task.Run(async () => {
                    // Lock should expire by timeeout
                    IDisposable lock2 = this.testLockCache.AcquireLock("test2Lock", 301, 300);
                    Assert.IsNotNull(lock2);
                }).Wait();
            }
        }

        [Test]
        public void TimeeoutTest()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test3Lock", 50, 300))
                Assert.Throws<AggregateException>(() => {
                    Task.Run(() => {
                        // Try to acquire the lock from a different thread. Should timeout
                        IDisposable lock2 = this.testLockCache.AcquireLock("test3Lock", 5, 100);
                        Assert.IsNotNull(lock2);
                    }).Wait();
                });
        }

        [Test]
        public void AcquireTestAfterImmediateRelease()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test4Lock", 50, 300))
            {
                Assert.IsNotNull(lock1);
            }

            // Second time we should be able to get it again, because the other was disposed
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test4Lock", 50, 300))
            {
                Assert.IsNotNull(lock1);
            }
        }

        [Test]
        public void AcquireTestAfterImmediateRelease_DifferntThread()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test5Lock", 50, 300))
            {
                Assert.IsNotNull(lock1);
            }
            // Second time we should be able to get it again, because the other was disposed
            Task.Run(() => {
                using (IDisposable lock1 = this.testLockCache.AcquireLock("test5Lock", 50, 300))
                {
                    Assert.IsNotNull(lock1);
                }
            }).Wait();
        }

        [Test]
        public void AcquireSameThreadTest()
        {
            using (IDisposable lock1 = this.testLockCache.AcquireLock("test6Lock", 50, 300))
            {
                Assert.IsNotNull(lock1);
                IDisposable lock2 = this.testLockCache.AcquireLock("test6Lock", 50, 300);
            }
        }
    }
}
