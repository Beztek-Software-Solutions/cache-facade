// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Queue <see cref="IMessageProcessor"/> that drains <see cref="WriteBehindMessage"/> snapshots
    /// into <see cref="IPersistenceService.BatchPersistAsync"/> without re-reading the live cache.
    /// </summary>
    /// <typeparam name="T">Entity type stored in the named cache.</typeparam>
    public class CacheWriteBehindProcessor<T> : IMessageProcessor
    {
        private static readonly bool SupportsWriteBehindEntity = typeof(IWriteBehindEntity).IsAssignableFrom(typeof(T));
        private static readonly PropertyInfo IdProperty = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        private readonly string cacheName;
        private Cache cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheWriteBehindProcessor{T}"/> class.
        /// </summary>
        /// <param name="cacheName">Cache name registered with <see cref="CacheFactory"/> (must match the write-behind cache).</param>
        public CacheWriteBehindProcessor(string cacheName)
        {
            this.cacheName = cacheName;
        }

        /// <summary>
        /// Processes a single queue message by delegating to the batch overload.
        /// </summary>
        /// <param name="message">Queue message whose payload is a <see cref="WriteBehindMessage"/>.</param>
        /// <returns><c>true</c> when processing succeeds.</returns>
        public virtual async Task<bool> Process(Message message)
        {
            return (await this.Process(new List<Message> { message }).ConfigureAwait(false))[0];
        }

        /// <summary>
        /// Drains write-behind messages from the queue snapshot (intent + value + sequence).
        /// Does not read the live cache. Create/update map to upsert. When <typeparamref name="T"/>
        /// implements <see cref="IWriteBehindEntity"/>, deletes are soft-upserts that retain the
        /// sequential etag clock.
        /// </summary>
        public virtual async Task<List<bool>> Process(List<Message> messageList)
        {
            Dictionary<string, WriteBehindMessage> winnersById = SelectLatestById(messageList);
            BuildPersistenceBatch(winnersById, out List<PersistenceAction> actions, out Dictionary<string, object> items);

            if (actions.Count > 0)
            {
                await this.GetCache().PersistenceService.BatchPersistAsync(actions, items).ConfigureAwait(false);
            }

            return Enumerable.Repeat(true, messageList.Count).ToList();
        }

        private Dictionary<string, WriteBehindMessage> SelectLatestById(List<Message> messageList)
        {
            Dictionary<string, WriteBehindMessage> winnersById = new Dictionary<string, WriteBehindMessage>(StringComparer.Ordinal);

            foreach (Message message in messageList)
            {
                WriteBehindMessage writeBehindMessage = ParseWriteBehindMessage(message);
                if (writeBehindMessage == null || string.IsNullOrEmpty(writeBehindMessage.Id))
                {
                    continue;
                }

                ConsiderCandidate(winnersById, writeBehindMessage);
            }

            return winnersById;
        }

        private void ConsiderCandidate(Dictionary<string, WriteBehindMessage> winnersById, WriteBehindMessage candidate)
        {
            if (!winnersById.TryGetValue(candidate.Id, out WriteBehindMessage existing))
            {
                winnersById[candidate.Id] = candidate;
                return;
            }

            if (candidate.Sequence >= existing.Sequence)
            {
                LogDiscarded(existing.Id, existing.Sequence, candidate.Sequence, keptIsCandidate: true);
                winnersById[candidate.Id] = candidate;
                return;
            }

            LogDiscarded(candidate.Id, candidate.Sequence, existing.Sequence, keptIsCandidate: false);
        }

        private void LogDiscarded(string id, long discardedSequence, long keptSequence, bool keptIsCandidate)
        {
            // Message text differs only for log clarity; both paths discard the older snapshot.
            string format = keptIsCandidate
                ? "Write-behind discarded queued snapshot for {CacheKey} (not latest in batch): sequence {DiscardedSequence} <= {KeptSequence}"
                : "Write-behind discarded queued snapshot for {CacheKey} (not latest in batch): sequence {DiscardedSequence} < {KeptSequence}";

            this.GetCache().FacadeLogger?.LogDebug(format, id, discardedSequence, keptSequence);
        }

        private static void BuildPersistenceBatch(
            Dictionary<string, WriteBehindMessage> winnersById,
            out List<PersistenceAction> actions,
            out Dictionary<string, object> items)
        {
            actions = new List<PersistenceAction>();
            items = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (WriteBehindMessage winner in winnersById.Values)
            {
                if (winner.WriteType == WriteType.Delete)
                {
                    TryAddDeleteAction(winner, actions, items);
                    continue;
                }

                T value = CoerceValue(winner.Value);
                ApplyWriteBehindMetadata(value, winner.Sequence, isDeleted: false);
                actions.Add(new PersistenceAction(winner.Id, WriteType.Upsert));
                items[winner.Id] = value;
            }
        }

        private static void TryAddDeleteAction(
            WriteBehindMessage winner,
            List<PersistenceAction> actions,
            Dictionary<string, object> items)
        {
            if (SupportsWriteBehindEntity)
            {
                T tombstone = BuildSoftDeleteSnapshot(winner);
                if (tombstone == null)
                {
                    return;
                }

                actions.Add(new PersistenceAction(winner.Id, WriteType.Upsert));
                items[winner.Id] = tombstone;
                return;
            }

            actions.Add(new PersistenceAction(winner.Id, WriteType.Delete));
            items[winner.Id] = default(T);
        }

        private static T BuildSoftDeleteSnapshot(WriteBehindMessage winner)
        {
            T value = CoerceValue(winner.Value);
            if (value == null)
            {
                value = TryCreateEmptyInstance(winner.Id);
                if (value == null)
                {
                    return default;
                }
            }

            ApplyWriteBehindMetadata(value, winner.Sequence, isDeleted: true);
            return value;
        }

        private static T TryCreateEmptyInstance(string id)
        {
            try
            {
                T value = Activator.CreateInstance<T>();
                IdProperty?.SetValue(value, id);
                return value;
            }
            catch (MissingMethodException)
            {
                return default;
            }
        }

        private static void ApplyWriteBehindMetadata(T value, long sequence, bool isDeleted)
        {
            if (value is IWriteBehindEntity entity)
            {
                entity.Etag = sequence.ToString(CultureInfo.InvariantCulture);
                entity.IsDeleted = isDeleted;
            }
        }

        private static WriteBehindMessage ParseWriteBehindMessage(Message message)
        {
            if (message?.RawMessage == null)
            {
                return null;
            }

            if (message.RawMessage is WriteBehindMessage writeBehindMessage)
            {
                return writeBehindMessage;
            }

            return message.GetMessageObject<WriteBehindMessage>();
        }

        private static T CoerceValue(object value)
        {
            if (value == null)
            {
                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            if (value is JsonElement jsonElement)
            {
                return CoerceJsonElement(jsonElement);
            }

            return SerializationUtil.JsonDeserialize<T>(Convert.ToString(value));
        }

        private static T CoerceJsonElement(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null || jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            return jsonElement.Deserialize<T>();
        }

        private Cache GetCache()
        {
            if (this.cache == null)
            {
                this.cache = (Cache)CacheFactory.GetCache(this.cacheName);
            }

            return this.cache;
        }
    }
}
