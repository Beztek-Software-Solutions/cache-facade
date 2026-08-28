// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Write-behind queue payload carrying the operation intent and a snapshot of the value.
    /// Patterned after production OpenSearch CDC outbox rows (operation + payload + timestamp).
    /// <para>
    /// The legacy write-behind message was just the cache key, which forced <see cref="CacheWriteBehindProcessor{T}"/>
    /// to infer the intent (create/update/delete) from the live cache and DB state at drain time. That inference is
    /// corrupted whenever a key is evicted from the cache while its write is still pending (e.g. a read-cache
    /// invalidation via <c>FlushKeyAsync</c>/<c>FlushAsync</c>): the pending write is then silently skipped (never
    /// persisted) or, if the row already exists, wrongly deleted.
    /// </para>
    /// <para>
    /// By carrying the intent and the value snapshot, the drain is correct regardless of cache eviction: the value
    /// no longer has to survive in the cache until it is drained.
    /// </para>
    /// <para>
    /// For <see cref="IWriteBehindEntity"/>, <see cref="Sequence"/> is also written into <see cref="IEtagEntity.Etag"/>
    /// as a short sequential string (epoch ms). Soft deletes set <see cref="IWriteBehindEntity.IsDeleted"/> and
    /// upsert; they must not remove the row or the version clock is lost.
    /// </para>
    /// </summary>
    public class WriteBehindMessage
    {
        /// <summary>Cache key of the entity being persisted.</summary>
        public string Id { get; set; }

        /// <summary>Intent captured at write time.</summary>
        public WriteType WriteType { get; set; }

        /// <summary>
        /// Snapshot of the value at enqueue time (null when no snapshot is available on delete).
        /// Serialized inline; on dequeue it deserializes to a <see cref="System.Text.Json.JsonElement"/>,
        /// which the processor coerces to the cache's value type <c>T</c> before persistence.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// UTC epoch milliseconds assigned at enqueue. The processor keeps the highest sequence per key
        /// in a batch. For <see cref="IWriteBehindEntity"/>, the same value is stamped onto
        /// <see cref="IEtagEntity.Etag"/> as a compact sequential string for cross-batch last-write-wins.
        /// </summary>
        public long Sequence { get; set; }
    }
}
