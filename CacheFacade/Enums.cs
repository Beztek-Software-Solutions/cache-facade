// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Supported cache provider backends. Only <see cref="Redis"/> and <see cref="LocalMemory"/>
    /// are implemented; <see cref="Hazelcast"/> and <see cref="Ignite"/> are reserved for future use.
    /// </summary>
    public enum CacheProviderType
    {
        /// <summary>Redis via StackExchange.Redis (distributed).</summary>
        Redis,

        /// <summary>Reserved. Not implemented.</summary>
        Hazelcast,

        /// <summary>Reserved. Not implemented.</summary>
        Ignite,

        /// <summary>In-process <c>System.Runtime.Caching.MemoryCache</c> (non-distributed).</summary>
        LocalMemory
    }

    /// <summary>
    /// How the facade couples the cache provider to persistence.
    /// </summary>
    public enum CacheType
    {
        /// <summary>Cache only; no persistence reads or writes.</summary>
        NonPersistent,

        /// <summary>Synchronous create/update/delete against <see cref="IPersistenceService"/> on each write.</summary>
        WriteThrough,

        /// <summary>
        /// Asynchronous persistence via a queue of <see cref="WriteBehindMessage"/> snapshots.
        /// Requires <see cref="QueueConfiguration"/> and <see cref="IPersistenceService"/>.
        /// </summary>
        WriteBehind,
    }

    /// <summary>
    /// Intent of a persistence write (API write or write-behind drain).
    /// </summary>
    public enum WriteType
    {
        /// <summary>Insert a new row (write-through create path).</summary>
        Create,

        /// <summary>Update an existing row (write-through update path).</summary>
        Update,

        /// <summary>Delete a row (hard delete) or soft-delete for write-behind entities.</summary>
        Delete,

        /// <summary>
        /// Insert-or-update used by write-behind batch drain (create/update queue intents map here).
        /// </summary>
        Upsert
    }

    /// <summary>
    /// How values are encoded when stored in the cache provider.
    /// </summary>
    public enum SerializationType
    {
        /// <summary>JSON via <see cref="System.Text.Json"/> (ASCII bytes).</summary>
        Json,

        /// <summary>Not supported; <see cref="SerializationUtil"/> throws <see cref="System.NotSupportedException"/>.</summary>
        None
    }
}
