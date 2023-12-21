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
        private IDistributedLock testLock;

        [SetUp]
        public void SetUp()
        {
            ICacheProviderConfiguration lockProviderConfiguration = new LocalMemoryProviderConfiguration("lockCache", 300000);
            ICache lockCache = CacheFactory.GetOrCreateCache(new CacheConfiguration(lockProviderConfiguration, CacheType.NonPersistent));
            testLock = new DisposableLock(lockCache, "lockCache");
        }

        [Test]
        public void AcquireTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test0Lock", 50, 300, 1))
            {
                Assert.IsNotNull(lock1);
            }
        }

        [Test]
        public void TimeAutoReleaseTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test1Lock", 50, 300, 1))
            {
                Task.Run(() => {
                    // Wait for lock to expire
                    Thread.Sleep(301);
                    // Lock has expired
                    IDisposable lock2 = this.testLock.AcquireLock("test1Lock", 50, 300, 1);
                    Assert.IsNotNull(lock2);
                }).Wait();
            }
        }

        [Test]
        public void LockWaitTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test2Lock", 50, 300, 1))
            {
                Task.Run(async () => {
                    // Lock should expire by timeeout
                    IDisposable lock2 = this.testLock.AcquireLock("test2Lock", 301, 300, 1);
                    Assert.IsNotNull(lock2);
                }).Wait();
            }
        }

        [Test]
        public void TimeeoutTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test3Lock", 50, 300, 1))
                Assert.Throws<AggregateException>(() => {
                    Task.Run(() => {
                        // Try to acquire the lock from a different thread. Should timeout
                        IDisposable lock2 = this.testLock.AcquireLock("test3Lock", 5, 100, 1);
                        Assert.IsNotNull(lock2);
                    }).Wait();
                });
        }

        [Test]
        public void AcquireTestAfterImmediateRelease()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test4Lock", 50, 300, 1))
            {
                Assert.IsNotNull(lock1);
            }

            // Second time we should be able to get it again, because the other was disposed
            using (IDisposable lock1 = this.testLock.AcquireLock("test4Lock", 50, 300, 1))
            {
                Assert.IsNotNull(lock1);
            }
        }

        [Test]
        public void AcquireTestAfterImmediateRelease_DifferntThread()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test5Lock", 50, 300, 1))
            {
                Assert.IsNotNull(lock1);
            }
            // Second time we should be able to get it again, because the other was disposed
            Task.Run(() => {
                using (IDisposable lock1 = this.testLock.AcquireLock("test5Lock", 50, 300, 1))
                {
                    Assert.IsNotNull(lock1);
                }
            }).Wait();
        }

        [Test]
        public void AcquireSameThreadTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test6Lock", 50, 300, 1))
            {
                Assert.IsNotNull(lock1);
                IDisposable lock2 = this.testLock.AcquireLock("test6Lock", 50, 300, 1);
            }
        }
    }
}
