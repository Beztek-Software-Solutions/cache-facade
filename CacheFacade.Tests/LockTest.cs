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
            this.testLock = new DisposableLock();
        }

        [Test]
        public void AcquireTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test0Lock", 50, 300, 1))
            {
                Assert.That(lock1, Is.Not.Null);
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
                    using IDisposable lock2 = this.testLock.AcquireLock("test1Lock", 50, 300, 1);
                    Assert.That(lock2, Is.Not.Null);
                }).Wait();
            }
        }

        [Test]
        public void LockWaitTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test2Lock", 50, 300, 1))
            {
                Task.Run(() => {
                    // Lock should expire by timeout
                    using IDisposable lock2 = this.testLock.AcquireLock("test2Lock", 301, 300, 1);
                    Assert.That(lock2, Is.Not.Null);
                }).Wait();
            }
        }

        [Test]
        public void TimeeoutTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test3Lock", 50, 300, 1))
            {
                Assert.Throws<TimeoutException>(() =>
                    Task.Run(() => this.testLock.AcquireLock("test3Lock", 5, 100, 1)).GetAwaiter().GetResult());
            }
        }

        [Test]
        public void AcquireTestAfterImmediateRelease()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test4Lock", 50, 300, 1))
            {
                Assert.That(lock1, Is.Not.Null);
            }

            // Second time we should be able to get it again, because the other was disposed
            using (IDisposable lock1 = this.testLock.AcquireLock("test4Lock", 50, 300, 1))
            {
                Assert.That(lock1, Is.Not.Null);
            }
        }

        [Test]
        public void AcquireTestAfterImmediateRelease_DifferntThread()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test5Lock", 50, 300, 1))
            {
                Assert.That(lock1, Is.Not.Null);
            }
            // Second time we should be able to get it again, because the other was disposed
            Task.Run(() => {
                using (IDisposable lock1 = this.testLock.AcquireLock("test5Lock", 50, 300, 1))
                {
                    Assert.That(lock1, Is.Not.Null);
                }
            }).Wait();
        }

        [Test]
        public void AcquireSameThreadTest()
        {
            using (IDisposable lock1 = this.testLock.AcquireLock("test6Lock", 50, 300, 1))
            {
                Assert.That(lock1, Is.Not.Null);
                using IDisposable lock2 = this.testLock.AcquireLock("test6Lock", 50, 300, 1);
                Assert.That(lock2, Is.Not.Null);
            }
        }

        [Test]
        public void ReentrantAcquireRenewsExpiry()
        {
            using (IDisposable outer = this.testLock.AcquireLock("renewLock", 50, 300, 1))
            {
                Thread.Sleep(200);
                using (IDisposable inner = this.testLock.AcquireLock("renewLock", 50, 300, 1))
                {
                    Assert.That(inner, Is.Not.Null);
                }

                bool acquiredByOtherThread = false;
                Task.Run(() => {
                    Thread.Sleep(150);
                    try
                    {
                        this.testLock.AcquireLock("renewLock", 5, 100, 1);
                        acquiredByOtherThread = true;
                    }
                    catch (TimeoutException)
                    {
                    }
                }).Wait();

                Assert.That(acquiredByOtherThread, Is.False);
            }
        }

        [Test]
        public void DisposeFromDifferentThreadStillReleases()
        {
            // Mirrors Cache async paths: acquire on thread A, await ConfigureAwait(false), dispose on thread B.
            IDisposable owner = this.testLock.AcquireLock("asyncDisposeLock", 50, 3000, 1);
            Task.Run(() => owner.Dispose()).Wait();

            using IDisposable next = this.testLock.AcquireLock("asyncDisposeLock", 50, 300, 1);
            Assert.That(next, Is.Not.Null);
        }

        [Test]
        public async Task DisposeAfterAwaitConfigureAwaitFalse_Releases()
        {
            using (this.testLock.AcquireLock("awaitDisposeLock", 50, 3000, 1))
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            using IDisposable next = this.testLock.AcquireLock("awaitDisposeLock", 50, 300, 1);
            Assert.That(next, Is.Not.Null);
        }

        [Test]
        public void ConcurrentAcquireOnlyOneHolderAtATime()
        {
            int currentHolders = 0;
            int maxConcurrentHolders = 0;
            object tallyGate = new object();
            using ManualResetEventSlim startGate = new ManualResetEventSlim(false);

            Task task1 = Task.Run(() => {
                startGate.Wait();
                using (this.testLock.AcquireLock("contendedLock", 1000, 500, 1))
                {
                    lock (tallyGate)
                    {
                        currentHolders++;
                        maxConcurrentHolders = Math.Max(maxConcurrentHolders, currentHolders);
                    }

                    Thread.Sleep(100);

                    lock (tallyGate)
                    {
                        currentHolders--;
                    }
                }
            });

            Task task2 = Task.Run(() => {
                startGate.Wait();
                using (this.testLock.AcquireLock("contendedLock", 1000, 500, 1))
                {
                    lock (tallyGate)
                    {
                        currentHolders++;
                        maxConcurrentHolders = Math.Max(maxConcurrentHolders, currentHolders);
                    }

                    Thread.Sleep(100);

                    lock (tallyGate)
                    {
                        currentHolders--;
                    }
                }
            });

            startGate.Set();
            Task.WaitAll(task1, task2);
            Assert.That(maxConcurrentHolders, Is.EqualTo(1));
        }
    }
}
