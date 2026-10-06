// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NUnit.Framework;

    /// <summary>
    /// WriteThrough keys mutated under an ambient <see cref="TransactionScope"/> are evicted on abort.
    /// Write-behind does not participate. These tests use in-memory SQLite (no ambient SQL enlistment),
    /// so they assert provider eviction only — not database rollback.
    /// </summary>
    [TestFixture]
    public class AmbientWriteThroughTransactionTests
    {
        [Test]
        public async Task WriteThrough_Abort_EvictsProviderKey()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("ambient-abort");

                using (NewScope())
                {
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                    Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Not.Null);
                }

                Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task WriteThrough_Complete_KeepsProviderKey()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("ambient-commit");

                using (TransactionScope scope = NewScope())
                {
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                    scope.Complete();
                }

                TestEtagCacheable peeked = await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                Assert.That(peeked, Is.Not.Null);
                Assert.That(peeked.Value, Is.EqualTo("ambient-commit"));
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task WriteThrough_ExternalFailureBeforeComplete_EvictsProviderKey()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteThrough, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("ambient-cognito-fail");

                try
                {
                    using TransactionScope scope = NewScope();
                    await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
                    throw new InvalidOperationException("simulated external failure");
                }
                catch (InvalidOperationException)
                {
                    // expected
                }

                Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        [Test]
        public async Task WriteBehind_Abort_DoesNotEvictProviderKey()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            try
            {
                TestEtagCacheable entity = NewEntity("wb-ambient-abort");

                using (NewScope())
                {
                    await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
                }

                // Write-behind is not enlisted for ambient TX cache cleanup.
                Assert.That(await cache.PeekAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false), Is.Not.Null);
            }
            finally
            {
                cache.Dispose();
            }
        }

        private static TransactionScope NewScope() =>
            new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Enabled);

        private static TestEtagCacheable NewEntity(string value) =>
            new TestEtagCacheable(
                Guid.NewGuid().ToString("N"),
                value,
                TestUtil.GetNow(),
                TestUtil.GetNow(),
                null);
    }
}
