// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Obsolete name for <see cref="IWriteBehindEntity"/>. Prefer <see cref="IWriteBehindEntity"/>:
    /// sequential <see cref="IEtagEntity.Etag"/> is the version clock; only <see cref="IWriteBehindEntity.IsDeleted"/>
    /// is an additional persisted field.
    /// </summary>
    [Obsolete("Use IWriteBehindEntity: sequential Etag is the version clock; expose IsDeleted for soft delete.")]
    public interface IWriteBehindVersion : IWriteBehindEntity
    {
        /// <summary>
        /// Obsolete. Use a sequential <see cref="IEtagEntity.Etag"/> (epoch-ms string) instead of a separate column.
        /// </summary>
        long WriteBehindSequence { get; set; }
    }
}
