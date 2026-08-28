// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Queue;
    using Beztek.Facade.Sql;
    using NUnit.Framework;

    [TestFixture]
    public class WriteBehindRobustnessTests
    {
        private const int WaitTimeForWriteBehindMillis = 250;

        // --- Flush / eviction before drain (API path) ---

        [Test]
        public async Task FlushBeforeDrain_CreateStillPersists()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable entity = NewEntity("flush-create");

            await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
            await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
            Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(entity.Id), Is.Null);

            await WaitForDrain().ConfigureAwait(false);

            TestEtagCacheable persisted = await GetPersisted(cache, entity.Id).ConfigureAwait(false);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Value, Is.EqualTo(entity.Value));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(EtagUtil.ParseSequentialEtag(entity.Etag)));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.GreaterThan(0));
        }

        [Test]
        public async Task FlushBeforeDrain_UpdateStillPersists()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable original = NewEntity("v1");
            await cache.GetAndPutAsync(original.Id, original).ConfigureAwait(false);
            await WaitForDrain().ConfigureAwait(false);

            TestEtagCacheable updated = new TestEtagCacheable(original.Id, "v2", original.CreatedDate, TestUtil.GetNow(), original.Etag);
            await cache.GetAndReplaceAsync(original.Id, updated).ConfigureAwait(false);
            await cache.FlushKeyAsync<TestEtagCacheable>(original.Id).ConfigureAwait(false);

            await WaitForDrain().ConfigureAwait(false);

            TestEtagCacheable persisted = await GetPersisted(cache, original.Id).ConfigureAwait(false);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Value, Is.EqualTo("v2"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.GreaterThan(EtagUtil.ParseSequentialEtag(original.Etag)));
        }

        [Test]
        public async Task FlushBeforeDrain_DeleteStillPersists()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable entity = NewEntity("to-delete");
            await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
            await WaitForDrain().ConfigureAwait(false);

            await cache.RemoveAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
            await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);

            await WaitForDrain().ConfigureAwait(false);

            Assert.That(await cache.PersistenceService.GetByIdAsync(entity.Id).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task FlushAllBeforeDrain_MultipleKeysStillPersist()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable a = NewEntity("a");
            TestEtagCacheable b = NewEntity("b");
            await cache.GetAndPutAsync(a.Id, a).ConfigureAwait(false);
            await cache.GetAndPutAsync(b.Id, b).ConfigureAwait(false);
            await cache.FlushAsync<TestEtagCacheable>().ConfigureAwait(false);

            Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(a.Id), Is.Null);
            Assert.That(cache.CacheProvider.Get<TestEtagCacheable>(b.Id), Is.Null);

            await WaitForDrain().ConfigureAwait(false);

            Assert.That((await GetPersisted(cache, a.Id).ConfigureAwait(false)).Value, Is.EqualTo("a"));
            Assert.That((await GetPersisted(cache, b.Id).ConfigureAwait(false)).Value, Is.EqualTo("b"));
        }

        [Test]
        public async Task FlushBeforeDrain_GetAsyncReloadsPersistedValue()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable entity = NewEntity("reload");
            await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);
            await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
            await WaitForDrain().ConfigureAwait(false);

            TestEtagCacheable reloaded = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
            Assert.That(reloaded.Value, Is.EqualTo("reload"));
            Assert.That(reloaded.Etag, Is.EqualTo(entity.Etag));
        }

        // --- In-batch Sequence dedupe ---

        [Test]
        public async Task SequenceDedupe_InBatch_AppliesLatestOnly()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "older", 100), 100)),
                Wrap(Msg(id, WriteType.Update, Entity(id, "newer", 200), 200)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo("newer"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(200));
        }

        [Test]
        public async Task SequenceDedupe_InBatch_ReverseArrivalOrder_StillLatestWins()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Update, Entity(id, "newer", 200), 200)),
                Wrap(Msg(id, WriteType.Create, Entity(id, "older", 100), 100)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("newer"));
        }

        [Test]
        public async Task SequenceDedupe_InBatch_DeleteWithHighestSequenceWins()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "temp", 100), 100)),
                Wrap(Msg(id, WriteType.Update, Entity(id, "temp2", 150), 150)),
                Wrap(Msg(id, WriteType.Delete, null, 200)),
            }).ConfigureAwait(false);

            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task SequenceDedupe_InBatch_UpsertAfterDelete_HigherSequenceWins()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "first", 100), 100)),
                Wrap(Msg(id, WriteType.Delete, null, 150)),
                Wrap(Msg(id, WriteType.Create, Entity(id, "recreated", 200), 200)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Value, Is.EqualTo("recreated"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(200));
        }

        [Test]
        public async Task SequenceDedupe_EqualSequence_LastInBatchWins()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "first", 100), 100)),
                Wrap(Msg(id, WriteType.Update, Entity(id, "second", 100), 100)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("second"));
        }

        [Test]
        public async Task SequenceDedupe_MultiKeyBatch_IndependentWinners()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string idA = Guid.NewGuid().ToString();
            string idB = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(idA, WriteType.Create, Entity(idA, "a1", 10), 10)),
                Wrap(Msg(idB, WriteType.Create, Entity(idB, "b1", 10), 10)),
                Wrap(Msg(idA, WriteType.Update, Entity(idA, "a2", 20), 20)),
                Wrap(Msg(idB, WriteType.Delete, null, 20)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(idA).ConfigureAwait(false)).Value, Is.EqualTo("a2"));
            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(idB).ConfigureAwait(false), Is.Null);
        }

        // --- Cross-batch version / stale redelivery ---

        [Test]
        public async Task VersionGuard_StaleUpsertDoesNotOverwrite()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "newer", 200), 200)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Update, Entity(id, "older", 100), 100)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo("newer"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(200));
        }

        [Test]
        public async Task VersionGuard_EqualSequenceUpsertIsNoOp()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "original", 200), 200)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Update, Entity(id, "same-seq", 200), 200)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("original"));
        }

        [Test]
        public async Task VersionGuard_NewerUpsertAfterOlderSucceeds()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "v1", 100), 100)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Update, Entity(id, "v2", 250), 250)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo("v2"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(250));
        }

        [Test]
        public async Task CrossBatch_DeleteThenStaleCreate_DoesNotResurrect()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, Entity(id, "snapshot", 200), 200)),
            }).ConfigureAwait(false);
            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false), Is.Null);

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "stale", 100), 100)),
            }).ConfigureAwait(false);

            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task CrossBatch_DeleteThenNewerCreate_Undeletes()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, Entity(id, "snapshot", 200), 200)),
            }).ConfigureAwait(false);

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "fresh", 300), 300)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Value, Is.EqualTo("fresh"));
            Assert.That(persisted.IsDeleted, Is.False);
        }

        [Test]
        public async Task VersionGuard_StaleDeleteDoesNotRemoveNewerUpsert()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "recreated", 300), 300)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, null, 150)),
            }).ConfigureAwait(false);

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Value, Is.EqualTo("recreated"));
        }

        [Test]
        public async Task VersionGuard_DeleteWithEqualSequenceIsSkipped()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "keep", 200), 200)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, null, 200)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("keep"));
        }

        [Test]
        public async Task VersionGuard_DeleteWithHigherSequenceRemoves()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, "gone", 100), 100)),
            }).ConfigureAwait(false);
            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, null, 200)),
            }).ConfigureAwait(false);

            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false), Is.Null);
        }

        // --- Edge / resilience ---

        [Test]
        public async Task DeleteMissingRow_IsIdempotent()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            List<bool> results = await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Delete, null, 100)),
            }).ConfigureAwait(false);

            Assert.That(results, Is.EqualTo(new[] { true }));
            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false), Is.Null);
        }

        [Test]
        public async Task SkipsNullAndEmptyIdMessages_StillProcessesValid()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            List<bool> results = await harness.Processor.Process(new List<Message>
            {
                new Message { MessageType = typeof(WriteBehindMessage).ToString(), RawMessage = null },
                Wrap(Msg(string.Empty, WriteType.Create, Entity("x", "nope", 1), 1)),
                Wrap(Msg(id, WriteType.Create, Entity(id, "ok", 50), 50)),
            }).ConfigureAwait(false);

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results.All(r => r), Is.True);
            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("ok"));
        }

        [Test]
        public async Task ProcessSingleMessage_OverloadWorks()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            bool ok = await harness.Processor.Process(
                Wrap(Msg(id, WriteType.Create, Entity(id, "single", 10), 10))).ConfigureAwait(false);

            Assert.That(ok, Is.True);
            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("single"));
        }

        [Test]
        public async Task DirectObjectRawMessage_WithoutJsonRoundTrip_Works()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();
            Message message = new Message
            {
                MessageType = typeof(WriteBehindMessage).ToString(),
                RawMessage = Msg(id, WriteType.Create, Entity(id, "direct", 42), 42)
            };

            await harness.Processor.Process(new List<Message> { message }).ConfigureAwait(false);
            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo("direct"));
        }

        [Test]
        public async Task Upsert_EscapesSpecialCharactersInValue()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();
            string value = "O'Brien \"quoted\" \\ slash";

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(id, WriteType.Create, Entity(id, value, 10), 10)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo(value));
        }

        [Test]
        public async Task ShuffledOutOfOrderBatch_StillEndsAtHighestSequence()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync(useShuffleProcessor: true).ConfigureAwait(false);
            string id = Guid.NewGuid().ToString();

            List<Message> messages = new List<Message>();
            for (int seq = 1; seq <= 20; seq++)
            {
                messages.Add(Wrap(Msg(id, WriteType.Update, Entity(id, $"v{seq}", seq), seq)));
            }

            // Run several shuffled passes; final persisted state must be the max sequence.
            for (int pass = 0; pass < 5; pass++)
            {
                await harness.ShuffledProcessor.Process(messages).ConfigureAwait(false);
            }

            TestEtagCacheable persisted = await harness.Get(id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo("v20"));
            Assert.That(EtagUtil.ParseSequentialEtag(persisted.Etag), Is.EqualTo(20));
        }

        [Test]
        public async Task MultiKeyBatch_UpsertsAndDelete_AllPersistCorrectly()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string[] ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid().ToString()).ToArray();

            List<Message> messages = ids.Select((id, index) =>
                Wrap(Msg(id, WriteType.Create, Entity(id, $"p{index}", index + 1), index + 1))).ToList();

            await harness.Processor.Process(messages).ConfigureAwait(false);

            foreach ((string id, int index) in ids.Select((id, index) => (id, index)))
            {
                Assert.That((await harness.Get(id).ConfigureAwait(false)).Value, Is.EqualTo($"p{index}"));
            }
        }

        [Test]
        public async Task VersionGatedUpsert_DoesNotClobberOtherKeys_WhenBatching()
        {
            // Regression: unparenthesized "seq IS NULL OR seq < N" with AND id=X becomes
            // (id=X AND seq IS NULL) OR seq < N due to SQL precedence, overwriting other keys.
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string idA = Guid.NewGuid().ToString();
            string idB = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(idA, WriteType.Create, Entity(idA, "alpha", 1), 1)),
                Wrap(Msg(idB, WriteType.Create, Entity(idB, "beta", 2), 2)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(idA).ConfigureAwait(false)).Value, Is.EqualTo("alpha"));
            Assert.That((await harness.Get(idB).ConfigureAwait(false)).Value, Is.EqualTo("beta"));
        }

        [Test]
        public async Task BatchPersist_MixedUpsertAndDelete_DoesNotThrow()
        {
            await using WriteBehindHarness harness = await WriteBehindHarness.CreateAsync().ConfigureAwait(false);
            string idKeep = Guid.NewGuid().ToString();
            string idDelete = Guid.NewGuid().ToString();

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(idDelete, WriteType.Create, Entity(idDelete, "doomed", 5), 5)),
            }).ConfigureAwait(false);

            await harness.Processor.Process(new List<Message>
            {
                Wrap(Msg(idKeep, WriteType.Create, Entity(idKeep, "keep", 10), 10)),
                Wrap(Msg(idDelete, WriteType.Delete, null, 10)),
            }).ConfigureAwait(false);

            Assert.That((await harness.Get(idKeep).ConfigureAwait(false)).Value, Is.EqualTo("keep"));
            Assert.That(await harness.Cache.PersistenceService.GetByIdAsync(idDelete).ConfigureAwait(false), Is.Null);
        }

        // --- Full API churn ---

        [Test]
        public async Task ApiPath_RapidUpdates_LastValuePersists_EvenIfFlushedMidway()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable entity = NewEntity("v0");
            await cache.GetAndPutAsync(entity.Id, entity).ConfigureAwait(false);

            TestEtagCacheable current = entity;
            for (int i = 1; i <= 8; i++)
            {
                current = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                    cache,
                    entity.Id,
                    new object[] { $"v{i}" },
                    (e, p) =>
                    {
                        e.Value = p[0].ToString();
                        e.UpdatedDate = TestUtil.GetNow();
                        return e;
                    }).ConfigureAwait(false);

                if (i == 4)
                {
                    await cache.FlushKeyAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                    // Reload so subsequent etag updates have a base object
                    current = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                    if (current == null)
                    {
                        // Drain may not have finished; wait once and retry get
                        await WaitForDrain().ConfigureAwait(false);
                        current = await cache.GetAsync<TestEtagCacheable>(entity.Id).ConfigureAwait(false);
                    }
                }
            }

            await WaitForDrain().ConfigureAwait(false);
            TestEtagCacheable persisted = await GetPersisted(cache, entity.Id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo(current.Value));
        }

        [Test]
        public async Task ApiPath_CreateUpdateDeleteRecreate_FinalCreatePersists()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable v1 = NewEntity("one");
            await cache.GetAndPutAsync(v1.Id, v1).ConfigureAwait(false);

            TestEtagCacheable v2 = await EtagEntityUpdateHelper.UpdateEntityAsync<TestEtagCacheable>(
                cache,
                v1.Id,
                new object[] { "two" },
                (e, p) =>
                {
                    e.Value = p[0].ToString();
                    e.UpdatedDate = TestUtil.GetNow();
                    return e;
                }).ConfigureAwait(false);

            await cache.RemoveAsync<TestEtagCacheable>(v1.Id).ConfigureAwait(false);
            TestEtagCacheable v3 = new TestEtagCacheable(v1.Id, "three", TestUtil.GetNow(), TestUtil.GetNow(), Guid.NewGuid().ToString());
            await cache.GetAndPutIfAbsentAsync(v1.Id, v3).ConfigureAwait(false);

            await WaitForDrain().ConfigureAwait(false);

            TestEtagCacheable persisted = await GetPersisted(cache, v1.Id).ConfigureAwait(false);
            Assert.That(persisted.Value, Is.EqualTo("three"));
            Assert.That(persisted.Etag, Is.EqualTo(v3.Etag));
            Assert.That(v2.Value, Is.EqualTo("two")); // sanity: update path ran
        }

        [Test]
        public async Task ApiPath_EnqueueStampsSequentialEtagOnCacheValue()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Cache cache = TestUtil.GetCache(CacheType.WriteBehind, cts.Token);
            TestEtagCacheable entity = NewEntity("seq-stamp");

            await cache.GetAndPutIfAbsentAsync(entity.Id, entity).ConfigureAwait(false);
            TestEtagCacheable cached = cache.CacheProvider.Get<TestEtagCacheable>(entity.Id);
            Assert.That(EtagUtil.ParseSequentialEtag(cached.Etag), Is.GreaterThan(0));
            Assert.That(cached.Etag, Is.EqualTo(entity.Etag));
        }

        // --- Helpers ---

        private static async Task WaitForDrain()
        {
            await Task.Delay(WaitTimeForWriteBehindMillis).ConfigureAwait(false);
        }

        private static async Task<TestEtagCacheable> GetPersisted(Cache cache, string id)
        {
            return (TestEtagCacheable)await cache.PersistenceService.GetByIdAsync(id).ConfigureAwait(false);
        }

        private static TestEtagCacheable NewEntity(string value)
        {
            return new TestEtagCacheable(Guid.NewGuid().ToString(), value, TestUtil.GetNow(), TestUtil.GetNow(), Guid.NewGuid().ToString());
        }

        private static TestEtagCacheable Entity(string id, string value, long sequence)
        {
            return new TestEtagCacheable(id, value, TestUtil.GetNow(), TestUtil.GetNow(), sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), sequence);
        }

        private static WriteBehindMessage Msg(string id, WriteType writeType, object value, long sequence)
        {
            return new WriteBehindMessage
            {
                Id = id,
                WriteType = writeType,
                Value = value,
                Sequence = sequence
            };
        }

        private static Message Wrap(WriteBehindMessage writeBehindMessage)
        {
            // Round-trip through JSON like the real queue so Value arrives as JsonElement.
            Message envelope = new Message
            {
                MessageType = typeof(WriteBehindMessage).ToString(),
                RawMessage = writeBehindMessage
            };
            return JsonSerializer.Deserialize<Message>(envelope.ToString());
        }

        private sealed class WriteBehindHarness : IAsyncDisposable
        {
            private readonly CancellationTokenSource cts;

            private WriteBehindHarness(Cache cache, CacheWriteBehindProcessor<TestEtagCacheable> processor, TestCacheWriteBehindProcessor shuffled, CancellationTokenSource cts)
            {
                this.Cache = cache;
                this.Processor = processor;
                this.ShuffledProcessor = shuffled;
                this.cts = cts;
            }

            public Cache Cache { get; }

            public CacheWriteBehindProcessor<TestEtagCacheable> Processor { get; }

            public TestCacheWriteBehindProcessor ShuffledProcessor { get; }

            public static Task<WriteBehindHarness> CreateAsync(bool useShuffleProcessor = false)
            {
                string cacheName = Guid.NewGuid().ToString();
                CancellationTokenSource cts = new CancellationTokenSource();

                ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
                IPersistenceService persistenceService = new SqlPersistenceService<TestEtagCacheable>(sqlFacade, new TestSqlGenerator());
                TestUtil.InitializeDB(sqlFacade);

                ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);
                IQueueProviderConfig queueProviderConfig = new LocalMemoryQueueProviderConfig($"{cacheName}Queue");
                IQueueClient queueClient = QueueClientFactory.GetQueueClient(queueProviderConfig);

                IMessageProcessor backgroundProcessor = useShuffleProcessor
                    ? new TestCacheWriteBehindProcessor(cacheName)
                    : new CacheWriteBehindProcessor<TestEtagCacheable>(cacheName);

                QueueConfiguration queueConfiguration = new QueueConfiguration(queueClient, backgroundProcessor, cts.Token, 1000, 5, 100, 10);
                Cache cache = (Cache)CacheFactory.GetOrCreateCache(
                    new CacheConfiguration(cacheProviderConfiguration, CacheType.WriteBehind, persistenceService, queueConfiguration));

                return Task.FromResult(new WriteBehindHarness(
                    cache,
                    new CacheWriteBehindProcessor<TestEtagCacheable>(cacheName),
                    new TestCacheWriteBehindProcessor(cacheName),
                    cts));
            }

            public Task<TestEtagCacheable> Get(string id) => GetPersisted(this.Cache, id);

            public ValueTask DisposeAsync()
            {
                this.cts.Cancel();
                this.cts.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
