// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;

    using Beztek.Facade.Sql;

    /// <summary>
    /// Generates dialect-specific SQL for CRUD and upsert operations for entity type <typeparamref name="T"/>.
    /// Used by <see cref="SqlPersistenceService{T}"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="GetSqlSelectByIds"/> and <see cref="GetId"/> are required for
    /// <see cref="SqlPersistenceService{T}.GetByIdsAsync"/> (batch search hydrate). They are not used by
    /// custom <see cref="IPersistenceService"/> implementations that skip that override and rely on the
    /// interface default (N× <see cref="IPersistenceService.GetByIdAsync"/>).
    /// </remarks>
    /// <typeparam name="T">Entity type.</typeparam>
    public interface ISqlGenerator<in T>
    {
        /// <summary>
        /// Provides a SqlSelect object to get the entity of type T by its id
        /// </summary>
        /// <param name="id">is the identity of the entity of type T</param>
        /// <returns>SqlSelect object for the entity of type T</returns>
        SqlSelect GetSqlSelect(string id);

        /// <summary>
        /// Provides a SqlSelect that loads all entities whose ids are in <paramref name="ids"/>
        /// (typically <c>WHERE id IN (...)</c>). Used by <see cref="SqlPersistenceService{T}.GetByIdsAsync"/>
        /// for 1+1 search hydration.
        /// </summary>
        /// <param name="ids">Non-empty list of entity ids (already distinct).</param>
        /// <returns>SqlSelect returning rows mappable to <typeparamref name="T"/>.</returns>
        SqlSelect GetSqlSelectByIds(IReadOnlyList<string> ids);

        /// <summary>
        /// Returns the persistence/cache key for a loaded entity (same string used with
        /// <see cref="GetSqlSelect"/> / cache keys). Required so batch <see cref="GetSqlSelectByIds"/>
        /// results can be mapped back into the id → entity dictionary.
        /// </summary>
        /// <param name="entity">Loaded entity.</param>
        /// <returns>Id string, or <c>null</c> if the entity cannot be keyed.</returns>
        string GetId(T entity);

        /// <summary>
        /// Provides a list of ISqlWrite statements to delete the entity of type T
        /// </summary>
        /// <param name="id">The id of the entity to be deleted</param>
        /// <returns>list of ISqlWrite statements to delete the entity of type T</returns>
        List<ISqlWrite> GetSqlDelete(string id);

        /// <summary>
        /// Provides a list of ISqlWrite statements to update an entity of type T
        /// </summary>
        /// <param name="id">The id of the entity to be updated</param>
        /// <param name="t">The updated entity that needs to be saved</param>
        /// <returns>list of ISqlWrite statements to update the corresponding entity</returns>
        List<ISqlWrite> GetSqlUpdate(string id, T t);

        /// <summary>
        /// Provides a list of ISqlWrite statements to create a new entity of type T
        /// </summary>
        /// <param name="id">The id of the entity to be inserted</param>
        /// <param name="t">list of ISqlWrite statements to create entity T</param>
        /// <returns></returns>
        List<ISqlWrite> GetSqlInsert(string id, T t);

        /// <summary>
        /// Provides dialect-specific insert-or-update statements for write-behind batch drain.
        /// When <typeparamref name="T"/> implements <see cref="IWriteBehindVersion"/>, prefer
        /// version-gated SQL using the entity's sequential <see cref="IEtagEntity.Etag"/>
        /// (and soft-delete flag when using <see cref="IWriteBehindEntity"/>).
        /// </summary>
        /// <param name="id">The id of the entity to upsert</param>
        /// <param name="t">The entity snapshot to persist</param>
        /// <returns>List of ISqlWrite statements that upsert entity T</returns>
        List<ISqlWrite> GetSqlUpsert(string id, T t);
    }
}
