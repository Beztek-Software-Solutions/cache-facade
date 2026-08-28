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

    public class CacheWriteBehindProcessor<T> : IMessageProcessor
    {
        private static readonly bool SupportsWriteBehindEntity = typeof(IWriteBehindEntity).IsAssignableFrom(typeof(T));
        private static readonly PropertyInfo IdProperty = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);

        private readonly string cacheName;
        private Cache cache;

        public CacheWriteBehindProcessor(string cacheName)
        {
            this.cacheName = cacheName;
        }

        public virtual async Task<bool> Process(Message message)
        {
            return (await this.Process(new List<Message> { message }).ConfigureAwait(false))[0];
        }

        /// <summary>
        /// Drains write-behind messages from the queue snapshot (intent + value + sequence).
        /// Does not read the live cache. Create/update map to upsert. When <typeparamref name="T"/>
        /// implements <see cref="IWriteBehindEntity"/>, deletes are soft-upserts that retain the
        /// sequential etag clock (OpenSearch soft-delete analogue).
        /// </summary>
        public virtual async Task<List<bool>> Process(List<Message> messageList)
        {
            Dictionary<string, WriteBehindMessage> winnersById = new Dictionary<string, WriteBehindMessage>(StringComparer.Ordinal);

            foreach (Message message in messageList)
            {
                WriteBehindMessage writeBehindMessage = ParseWriteBehindMessage(message);
                if (writeBehindMessage == null || string.IsNullOrEmpty(writeBehindMessage.Id))
                {
                    continue;
                }

                if (!winnersById.TryGetValue(writeBehindMessage.Id, out WriteBehindMessage existing)
                    || writeBehindMessage.Sequence >= existing.Sequence)
                {
                    winnersById[writeBehindMessage.Id] = writeBehindMessage;
                }
            }

            List<PersistenceAction> uniquePersistenceActionList = new List<PersistenceAction>();
            Dictionary<string, object> actionableItems = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (WriteBehindMessage winner in winnersById.Values)
            {
                if (winner.WriteType == WriteType.Delete)
                {
                    if (SupportsWriteBehindEntity)
                    {
                        T tombstone = BuildSoftDeleteSnapshot(winner);
                        if (tombstone == null)
                        {
                            continue;
                        }

                        uniquePersistenceActionList.Add(new PersistenceAction(winner.Id, WriteType.Upsert));
                        actionableItems[winner.Id] = tombstone;
                    }
                    else
                    {
                        uniquePersistenceActionList.Add(new PersistenceAction(winner.Id, WriteType.Delete));
                        actionableItems[winner.Id] = default(T);
                    }

                    continue;
                }

                T value = CoerceValue(winner.Value);
                ApplyWriteBehindMetadata(value, winner.Sequence, isDeleted: false);

                uniquePersistenceActionList.Add(new PersistenceAction(winner.Id, WriteType.Upsert));
                actionableItems[winner.Id] = value;
            }

            if (uniquePersistenceActionList.Count > 0)
            {
                await this.GetCache().PersistenceService.BatchPersistAsync(uniquePersistenceActionList, actionableItems).ConfigureAwait(false);
            }

            return Enumerable.Repeat(true, messageList.Count).ToList();
        }

        private static T BuildSoftDeleteSnapshot(WriteBehindMessage winner)
        {
            T value = CoerceValue(winner.Value);
            if (value == null)
            {
                try
                {
                    value = Activator.CreateInstance<T>();
                }
                catch (MissingMethodException)
                {
                    return default;
                }

                IdProperty?.SetValue(value, winner.Id);
            }

            ApplyWriteBehindMetadata(value, winner.Sequence, isDeleted: true);
            return value;
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
                if (jsonElement.ValueKind == JsonValueKind.Null || jsonElement.ValueKind == JsonValueKind.Undefined)
                {
                    return default;
                }

                return jsonElement.Deserialize<T>();
            }

            return SerializationUtil.JsonDeserialize<T>(Convert.ToString(value));
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
