// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// One create/update/delete/upsert intent in a <see cref="IPersistenceService.BatchPersistAsync"/> batch.
    /// Equality is by <see cref="Id"/> and <see cref="WriteType"/> so instances can be dictionary keys.
    /// </summary>
    public class PersistenceAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PersistenceAction"/> class.
        /// </summary>
        /// <param name="id">Entity / cache key.</param>
        /// <param name="writeType">Persistence intent.</param>
        public PersistenceAction(string id, WriteType writeType)
        {
            this.Id = id;
            this.WriteType = writeType;
        }

        /// <summary>Entity / cache key.</summary>
        public string Id { get; }

        /// <summary>Create, update, delete, or upsert.</summary>
        public WriteType WriteType { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.WriteType}:{this.Id}";
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            PersistenceAction other = obj as PersistenceAction;
            if (other != null)
            {
                return string.Equals(this.Id, other.Id, StringComparison.Ordinal) && this.WriteType == other.WriteType;
            }

            return false;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.ToString().GetHashCode(StringComparison.Ordinal);
        }
    }
}
