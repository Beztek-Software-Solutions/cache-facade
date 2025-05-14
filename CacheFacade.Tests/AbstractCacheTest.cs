// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    /// <summary>
    /// Facilitates test classes for using in-memory versions for all the cache types
    /// </summary>
    public abstract class AbstractCacheTest
    {
        protected const int WaitTimeForWriteBehindMillis = 50;
        protected CacheType CacheType { get; set; }

        [Test]
        public async Task ClearAsyncTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable oldResult = new TestEtagCacheable(Guid.NewGuid().ToString(), "old-result", GetNow(), GetNow(), Guid.NewGuid().ToString());

            // Put the old result in the cache and validate
            await cache.GetAndPutAsync(oldResult.Id, oldResult).ConfigureAwait(false);
            Assert.That(oldResult,  Is.EqualTo(await cache.GetAsync<TestEtagCacheable>(oldResult.Id).ConfigureAwait(false)));
            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(oldResult,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(oldResult.Id).ConfigureAwait(false)));
                }).Wait();
            }

            // Flush the cache
            await cache.FlushKeyAsync<TestEtagCacheable>(oldResult.Id).ConfigureAwait(false);

            if (cache.PersistenceService == null)
            {
                // Object should not exist in the cache any more since it is a non-persistence cache
                Assert.That(await cache.GetAsync<TestEtagCacheable>(oldResult.Id).ConfigureAwait(false), Is.Null);
            }
            else
            {
                // We should get the version that is in the database
                Assert.That(oldResult,  Is.EqualTo(await cache.GetAsync<TestEtagCacheable>(oldResult.Id).ConfigureAwait(false)));
            }
        }

        [Test]
        public async Task GetAsyncTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable result = new TestEtagCacheable(Guid.NewGuid().ToString(), "get-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            await cache.GetAndPutIfAbsentAsync(result.Id, result).ConfigureAwait(false);
            TestEtagCacheable operationResult = await cache.GetAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false);
            Assert.That(result,  Is.EqualTo(operationResult));

            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(result,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(result.Id).ConfigureAwait(false)));
                }).Wait();
            }
        }

        [Test]
        public async Task GetAsyncKeyNotExistTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            string key = Guid.NewGuid().ToString();
            TestEtagCacheable operationResult = await cache.GetAsync<TestEtagCacheable>(key).ConfigureAwait(false);
            Assert.That(operationResult, Is.Null);

            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(await cache.PersistenceService.GetByIdAsync(key).ConfigureAwait(false), Is.Null);
                }).Wait();
            }
        }

        [Test]
        public async Task GetAndPutIfAbsentKeyNotPresentAsyncTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable result = new TestEtagCacheable(Guid.NewGuid().ToString(), "getandputifabsentasync-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            TestEtagCacheable operationResult = await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(result.Id, result).ConfigureAwait(false);
            Assert.That(operationResult, Is.Null);

            Assert.That(result,  Is.EqualTo(await cache.GetAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false)));
            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(result,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(result.Id).ConfigureAwait(false)));
                }).Wait();
            }
        }

        [Test]
        public async Task GetAndPutIfAbsentAsyncKeyPresentTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable oldResult = new TestEtagCacheable(Guid.NewGuid().ToString(), "old-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            TestEtagCacheable newResult = new TestEtagCacheable(oldResult.Id, "new-result", GetNow(), GetNow(), oldResult.Etag);
            _ = cache.GetAndPutAsync(oldResult.Id, oldResult).Result;
            TestEtagCacheable operationResult = await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(newResult.Id, newResult).ConfigureAwait(false);
            Assert.That(oldResult,  Is.EqualTo(operationResult));

            Assert.That(oldResult,  Is.EqualTo(await cache.GetAsync<TestEtagCacheable>(newResult.Id).ConfigureAwait(false)));
            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(oldResult,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(newResult.Id).ConfigureAwait(false)));
                }).Wait();
            }
        }

        [Test]
        public async Task GetAndReplaceKeyNotPresentAsyncTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable result = new TestEtagCacheable(Guid.NewGuid().ToString(), "result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            TestEtagCacheable operationResult = await cache.GetAndReplaceAsync<TestEtagCacheable>(result.Id, result).ConfigureAwait(false);
            Assert.That(operationResult, Is.Null);

            Assert.That(await cache.GetAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false), Is.Null);
            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(await cache.PersistenceService.GetByIdAsync(result.Id).ConfigureAwait(false), Is.Null);
                }).Wait();
            }
        }

        [Test]
        public async Task GetAndReplaceKeyPresentAsyncTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable oldResult = new TestEtagCacheable(Guid.NewGuid().ToString(), "old-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            TestEtagCacheable newResult = new TestEtagCacheable(oldResult.Id, "new-result", GetNow(), GetNow(), oldResult.Etag);
            await cache.GetAndPutAsync<TestEtagCacheable>(oldResult.Id, oldResult).ConfigureAwait(false);
            if (cache.PersistenceService != null)
            {
                Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(oldResult,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(oldResult.Id).ConfigureAwait(false)));
                }).Wait();
            }

            TestEtagCacheable operationResult = await cache.GetAndReplaceAsync<TestEtagCacheable>(oldResult.Id, newResult).ConfigureAwait(false);
            Assert.That(oldResult,  Is.EqualTo(operationResult));
            Assert.That(newResult,  Is.EqualTo(cache.CacheProvider.Get<TestEtagCacheable>(oldResult.Id)));

            if (cache.PersistenceService != null)
            {
                await Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(newResult,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(oldResult.Id).ConfigureAwait(false)));
                }).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task GetAndPutAsyncKeyPresentTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable oldResult = new TestEtagCacheable(Guid.NewGuid().ToString(), "old-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            TestEtagCacheable newResult = new TestEtagCacheable(oldResult.Id, "new-result", GetNow(), GetNow(), oldResult.Etag);
            await cache.GetAndPutAsync(oldResult.Id, oldResult).ConfigureAwait(false);
            TestEtagCacheable operationResult = await cache.GetAndPutAsync<TestEtagCacheable>(newResult.Id, newResult).ConfigureAwait(false);
            Assert.That(oldResult,  Is.EqualTo(operationResult));

            Assert.That(newResult,  Is.EqualTo(cache.CacheProvider.Get<TestEtagCacheable>(newResult.Id)));
            if (cache.PersistenceService != null)
            {
                await Task.Run(async () => {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(newResult,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(newResult.Id).ConfigureAwait(false)));
                }).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task RemoveAsyncKeyExistsTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable result = new TestEtagCacheable(Guid.NewGuid().ToString(), "getandputasync-result", GetNow(), GetNow(), Guid.NewGuid().ToString());
            await cache.GetAndPutAsync(result.Id, result).ConfigureAwait(false);
            Assert.That(result,  Is.EqualTo(await cache.GetAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false)));

            TestEtagCacheable operationResult = await cache.RemoveAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false);
            Assert.That(result,  Is.EqualTo(operationResult));

            Task.Run(async () => {
                if (cache.PersistenceService != null)
                {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(await cache.PersistenceService.GetByIdAsync(result.Id).ConfigureAwait(false), Is.Null);
                }

                // Needs to be called at the end, to make sure it is cleared from the DB
                Assert.That(await cache.GetAsync<TestEtagCacheable>(result.Id).ConfigureAwait(false), Is.Null);
            }).Wait();
        }

        [Test]
        public async Task RemoveAsyncKeyNotExistsTest()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            string key = Guid.NewGuid().ToString();
            TestEtagCacheable operationResult = await cache.RemoveAsync<TestEtagCacheable>(key).ConfigureAwait(false);
            Assert.That(operationResult, Is.Null);

            Assert.That(await cache.GetAsync<TestEtagCacheable>(key).ConfigureAwait(false), Is.Null);
            if (cache.PersistenceService != null)
            {
                if (cache.CacheType == CacheType.WriteBehind)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                }

                Assert.That(await cache.PersistenceService.GetByIdAsync(key).ConfigureAwait(false), Is.Null);
            }
        }

        [Test]
        public async Task TestManyCacheWrites_UpdateLast()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable v1 = new TestEtagCacheable(Guid.NewGuid().ToString(), "v1", TestUtil.GetNow(), TestUtil.GetNow(), Guid.NewGuid().ToString());

            // Use the EtagUpdateHelper to ensure that the Etags match with each update
            await cache.GetAndPutAsync<TestEtagCacheable>(v1.Id, v1).ConfigureAwait(false);
            TestEtagCacheable v2 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v2" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v3 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v3" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);
            await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(v2.Id, v2).ConfigureAwait(false);
            TestEtagCacheable v4 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v4" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v5 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v5" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);

            Assert.That(v5,  Is.EqualTo(cache.CacheProvider.Get<TestEtagCacheable>(v1.Id)));
            Assert.That("v5",  Is.EqualTo(v5.Value));

            // Should match what is in the DB
            await Task.Run(async () => {
                if (cache.CacheType == CacheType.WriteThrough || cache.CacheType == CacheType.WriteBehind)
                {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(v5,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(v1.Id).ConfigureAwait(false)));
                }
            }).ConfigureAwait(false);
        }

        [Test]
        public async Task TestManyCacheWrites_CreateLast()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable v1 = new TestEtagCacheable(Guid.NewGuid().ToString(), "v1", TestUtil.GetNow(), TestUtil.GetNow(), Guid.NewGuid().ToString());

            // Use the EtagUpdateHelper to ensure that the Etags match with each update
            await cache.GetAndPutAsync<TestEtagCacheable>(v1.Id, v1).ConfigureAwait(false);
            TestEtagCacheable v2 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v2" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v3 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v3" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);
            await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(v2.Id, v2).ConfigureAwait(false);
            TestEtagCacheable v4 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v4" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v5 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v5" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);
            await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(v2.Id, v2).ConfigureAwait(false);

            Assert.That(v2,  Is.EqualTo(cache.CacheProvider.Get<TestEtagCacheable>(v1.Id)));

            // Should match what is in the DB
            await Task.Run(async () => {
                if (cache.CacheType == CacheType.WriteThrough || cache.CacheType == CacheType.WriteBehind)
                {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis)).ConfigureAwait(false);
                    }

                    Assert.That(v2,  Is.EqualTo(await cache.PersistenceService.GetByIdAsync(v1.Id).ConfigureAwait(false)));
                }
            }).ConfigureAwait(false);
        }

        [Test]
        public async Task TestManyCacheWrites_DeleteLast()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(this.CacheType, cts.Token);
            TestEtagCacheable v1 = new TestEtagCacheable(Guid.NewGuid().ToString(), "v1", TestUtil.GetNow(), TestUtil.GetNow(), Guid.NewGuid().ToString());

            // Use the EtagUpdateHelper to ensure that the Etags match with each update
            await cache.GetAndPutAsync<TestEtagCacheable>(v1.Id, v1).ConfigureAwait(false);
            TestEtagCacheable v2 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v2" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v3 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v3" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);
            await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(v2.Id, v2).ConfigureAwait(false);
            TestEtagCacheable v4 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v4" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            TestEtagCacheable v5 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "v5" },
                (e, p) => {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }
            ).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);
            await cache.GetAndPutIfAbsentAsync<TestEtagCacheable>(v2.Id, v2).ConfigureAwait(false);
            await cache.RemoveAsync<TestEtagCacheable>(v2.Id).ConfigureAwait(false);

            // Should match what is in the DB
            await Task.Run(() => {
                if (cache.CacheType == CacheType.WriteThrough || cache.CacheType == CacheType.WriteBehind)
                {
                    if (cache.CacheType == CacheType.WriteBehind)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(WaitTimeForWriteBehindMillis));
                    }

                    Assert.That(cache.PersistenceService.GetByIdAsync(v1.Id).Result, Is.Null);
                }

                // Need to read this after ensuring that it is cleard from the DB
                Assert.That(cache.GetAsync<TestEtagCacheable>(v1.Id).Result, Is.Null);
            }).ConfigureAwait(false);
        }

        protected static DateTime GetNow()
        {
            DateTime dt = DateTime.Now;
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
        }
    }
}
