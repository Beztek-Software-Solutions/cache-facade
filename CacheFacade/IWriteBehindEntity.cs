// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Entity contract for robust write-behind persistence (OpenSearch CDC-style last-write-wins).
    /// <b>Recommended for write-behind caches.</b> Write-through can use <see cref="IEtagEntity"/> alone.
    /// <para>
    /// Extends <see cref="IEtagEntity"/>. <see cref="IEtagEntity.Etag"/> is always a short sequential string
    /// (<see cref="EtagUtil.GenerateEtag"/>) for both cache modes; here it also acts as the drain version clock.
    /// </para>
    /// <para>
    /// Persist <see cref="IsDeleted"/> as an <c>is_deleted</c> column. Write-behind deletes soft-upsert
    /// the row (<see cref="IsDeleted"/> = true) so the sequential etag survives; a newer create/update
    /// clears the flag (undelete). <see cref="SqlPersistenceService{T}"/> treats soft-deleted rows as missing on read.
    /// </para>
    /// <para>
    /// Using this type on write-through works (sequential etags already match write-behind). Using write-behind
    /// without this interface falls back to hard delete and loses cross-batch create/delete safety—see README.
    /// </para>
    /// </summary>
    public interface IWriteBehindEntity : IEtagEntity
    {
        /// <summary>
        /// Soft-delete tombstone (OpenSearch <c>_deleted</c> analogue).
        /// </summary>
        bool IsDeleted { get; set; }
    }
}
