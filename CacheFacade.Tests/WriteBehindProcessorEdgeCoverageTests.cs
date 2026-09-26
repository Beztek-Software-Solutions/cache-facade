// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Queue;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class WriteBehindProcessorEdgeCoverageTests
    {
        [Test]
        public async Task Process_HardDeletes_WhenTypeIsNotWriteBehindEntity()
        {
            await using ProcessorHarness<TestCacheable> harness = await ProcessorHarness<TestCacheable>.CreateAsync().ConfigureAwait(false);
            List<PersistenceAction> captured = null;
            harness.Persistence
                .Setup(p => p.BatchPersistAsync(It.IsAny<List<PersistenceAction>>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<List<PersistenceAction>, Dictionary<string, object>>((actions, _) => captured = actions)
                .ReturnsAsync(new Dictionary<PersistenceAction, int>());

            string id = Guid.NewGuid().ToString();
            await harness.Processor.Process(new List<Message>
            {
                Wrap(new WriteBehindMessage { Id = id, WriteType = WriteType.Delete, Value = null, Sequence = 10 }),
            }).ConfigureAwait(false);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(captured[0].WriteType, Is.EqualTo(WriteType.Delete));
            Assert.That(captured[0].Id, Is.EqualTo(id));
        }

        [Test]
        public async Task Process_SoftDelete_BuildsEmptyInstance_WhenValueMissing()
        {
            await using ProcessorHarness<TestEtagCacheable> harness = await ProcessorHarness<TestEtagCacheable>.CreateAsync().ConfigureAwait(false);
            Dictionary<string, object> capturedItems = null;
            harness.Persistence
                .Setup(p => p.BatchPersistAsync(It.IsAny<List<PersistenceAction>>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<List<PersistenceAction>, Dictionary<string, object>>((_, items) => capturedItems = items)
                .ReturnsAsync(new Dictionary<PersistenceAction, int>());

            string id = Guid.NewGuid().ToString();
            await harness.Processor.Process(new List<Message>
            {
                Wrap(new WriteBehindMessage { Id = id, WriteType = WriteType.Delete, Value = null, Sequence = 42 }),
            }).ConfigureAwait(false);

            Assert.That(capturedItems, Is.Not.Null);
            Assert.That(capturedItems[id], Is.InstanceOf<TestEtagCacheable>());
            var tombstone = (TestEtagCacheable)capturedItems[id];
            Assert.That(tombstone.Id, Is.EqualTo(id));
            Assert.That(tombstone.IsDeleted, Is.True);
            Assert.That(tombstone.Etag, Is.EqualTo("42"));
        }

        [Test]
        public async Task Process_SoftDelete_Skips_WhenTypeHasNoParameterlessCtor()
        {
            await using ProcessorHarness<NoDefaultCtorWriteBehind> harness =
                await ProcessorHarness<NoDefaultCtorWriteBehind>.CreateAsync().ConfigureAwait(false);
            bool batchCalled = false;
            harness.Persistence
                .Setup(p => p.BatchPersistAsync(It.IsAny<List<PersistenceAction>>(), It.IsAny<Dictionary<string, object>>()))
                .Callback(() => batchCalled = true)
                .ReturnsAsync(new Dictionary<PersistenceAction, int>());

            await harness.Processor.Process(new List<Message>
            {
                Wrap(new WriteBehindMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    WriteType = WriteType.Delete,
                    Value = null,
                    Sequence = 7,
                }),
            }).ConfigureAwait(false);

            Assert.That(batchCalled, Is.False);
        }

        [Test]
        public async Task Process_CoercesJsonStringValue()
        {
            await using ProcessorHarness<TestEtagCacheable> harness = await ProcessorHarness<TestEtagCacheable>.CreateAsync().ConfigureAwait(false);
            Dictionary<string, object> capturedItems = null;
            harness.Persistence
                .Setup(p => p.BatchPersistAsync(It.IsAny<List<PersistenceAction>>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<List<PersistenceAction>, Dictionary<string, object>>((_, items) => capturedItems = items)
                .ReturnsAsync(new Dictionary<PersistenceAction, int>());

            string id = Guid.NewGuid().ToString();
            var entity = new TestEtagCacheable(id, "from-json", TestUtil.GetNow(), TestUtil.GetNow(), "99");
            string json = SerializationUtil.JsonSerialize(entity);

            await harness.Processor.Process(new List<Message>
            {
                new Message
                {
                    MessageType = typeof(WriteBehindMessage).ToString(),
                    RawMessage = new WriteBehindMessage
                    {
                        Id = id,
                        WriteType = WriteType.Create,
                        Value = json,
                        Sequence = 99,
                    },
                },
            }).ConfigureAwait(false);

            Assert.That(capturedItems[id], Is.InstanceOf<TestEtagCacheable>());
            Assert.That(((TestEtagCacheable)capturedItems[id]).Value, Is.EqualTo("from-json"));
        }

        [Test]
        public async Task Process_CoercesNullJsonElement_AsDefault()
        {
            await using ProcessorHarness<TestEtagCacheable> harness = await ProcessorHarness<TestEtagCacheable>.CreateAsync().ConfigureAwait(false);
            Dictionary<string, object> capturedItems = null;
            harness.Persistence
                .Setup(p => p.BatchPersistAsync(It.IsAny<List<PersistenceAction>>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<List<PersistenceAction>, Dictionary<string, object>>((_, items) => capturedItems = items)
                .ReturnsAsync(new Dictionary<PersistenceAction, int>());

            string id = Guid.NewGuid().ToString();
            using JsonDocument doc = JsonDocument.Parse("null");

            await harness.Processor.Process(new List<Message>
            {
                new Message
                {
                    MessageType = typeof(WriteBehindMessage).ToString(),
                    RawMessage = new WriteBehindMessage
                    {
                        Id = id,
                        WriteType = WriteType.Create,
                        Value = doc.RootElement.Clone(),
                        Sequence = 1,
                    },
                },
            }).ConfigureAwait(false);

            Assert.That(capturedItems[id], Is.Null);
        }

        private static Message Wrap(WriteBehindMessage writeBehindMessage)
        {
            return new Message
            {
                MessageType = typeof(WriteBehindMessage).ToString(),
                RawMessage = writeBehindMessage,
            };
        }

        /// <summary>IWriteBehindEntity without a parameterless constructor (Activator path fails).</summary>
        public sealed class NoDefaultCtorWriteBehind : IWriteBehindEntity
        {
            public NoDefaultCtorWriteBehind(string id)
            {
                this.Id = id;
            }

            public string Id { get; set; }

            public string Etag { get; set; }

            public bool IsDeleted { get; set; }
        }

        private sealed class ProcessorHarness<T> : IAsyncDisposable
        {
            private ProcessorHarness(string cacheName, CacheWriteBehindProcessor<T> processor, Mock<IPersistenceService> persistence)
            {
                this.CacheName = cacheName;
                this.Processor = processor;
                this.Persistence = persistence;
            }

            public string CacheName { get; }

            public CacheWriteBehindProcessor<T> Processor { get; }

            public Mock<IPersistenceService> Persistence { get; }

            public static Task<ProcessorHarness<T>> CreateAsync()
            {
                string cacheName = Guid.NewGuid().ToString("N");
                var persistence = new Mock<IPersistenceService>(MockBehavior.Strict);
                var cache = (Cache)CacheFactory.GetOrCreateCache(
                    new CacheConfiguration(
                        new LocalMemoryProviderConfiguration(cacheName, 300_000),
                        CacheType.WriteThrough,
                        persistence.Object));

                Assert.That(cache.PersistenceService, Is.SameAs(persistence.Object));
                return Task.FromResult(new ProcessorHarness<T>(
                    cacheName,
                    new CacheWriteBehindProcessor<T>(cacheName),
                    persistence));
            }

            public ValueTask DisposeAsync()
            {
                Cache existing = (Cache)CacheFactory.GetCache(this.CacheName);
                existing?.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
