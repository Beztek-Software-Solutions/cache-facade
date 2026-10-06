// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Beztek.Facade.Sql;

    /// <summary>
    /// Persistence backend used by write-through and write-behind caches (typically <see cref="SqlPersistenceService{T}"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GetByIdsAsync"/> has a <b>default interface implementation</b> that loops
    /// <see cref="GetByIdAsync"/>. Custom <see cref="IPersistenceService"/> types do <b>not</b>
    /// need to implement <see cref="GetByIdsAsync"/> — search stays correct (1 id-page + N loads).
    /// </para>
    /// <para>
    /// Override <see cref="GetByIdsAsync"/> (as <see cref="SqlPersistenceService{T}"/> does) only when
    /// you want true 1+1 search hydration: one id page + one batch load for cache misses.
    /// The interface default does not call <see cref="SqlPersistenceService{T}"/>; each backend
    /// that can batch must override itself.
    /// </para>
    /// </remarks>
    public interface IPersistenceService
    {
        /// <summary>
        /// Creates the object and throws an exception if the object already exists.
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="value">The object to be created.</param>
        /// <returns>The number of rows changed by this operation (1 if written, else 0 for the SQL implementation).</returns>
        Task<int> CreateAsync(string id, object value);

        /// <summary>
        /// Gets the object associated with the given id.
        /// Soft-deleted <see cref="IWriteBehindEntity"/> rows should be returned as <c>null</c>.
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <returns>The object associated with the given id, or <c>null</c>.</returns>
        Task<object> GetByIdAsync(string id);

        /// <summary>
        /// Batch-loads objects for the given ids.
        /// Soft-deleted <see cref="IWriteBehindEntity"/> rows must be omitted (same as a
        /// <see cref="GetByIdAsync"/> miss). Missing ids are simply absent from the map.
        /// Used by <see cref="ICache.SearchByQueryAsync{T}"/> after provider peeks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Optional to override.</b> The default implementation loops <see cref="GetByIdAsync"/>
        /// (1+N). Implementers such as app-specific routers can ignore this method entirely.
        /// </para>
        /// <para>
        /// <see cref="SqlPersistenceService{T}"/> overrides with a single SQL <c>IN (...)</c> query
        /// (true 1+1 when paired with search). Override in custom backends only when you have an
        /// equivalent batch read.
        /// </para>
        /// </remarks>
        /// <param name="ids">Persistence/cache keys to load (duplicates ignored).</param>
        /// <returns>Map of id → entity for rows that exist and are not soft-deleted.</returns>
        async Task<IDictionary<string, object>> GetByIdsAsync(IReadOnlyList<string> ids)
        {
            // Default: correct but 1+N. SqlPersistenceService (and optional custom backends)
            // override for a single batch round-trip. Do not call SqlPersistenceService from here —
            // not every IPersistenceService is SQL-backed.
            Dictionary<string, object> map = new Dictionary<string, object>(System.StringComparer.Ordinal);
            if (ids == null || ids.Count == 0)
            {
                return map;
            }

            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    continue;
                }

                object value = await GetByIdAsync(id).ConfigureAwait(false);
                if (value != null)
                {
                    map[id] = value;
                }
            }

            return map;
        }

        /// <summary>
        /// Updates the object (does nothing if the object does not exist).
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="value">The updated object.</param>
        /// <returns>The number of rows changed by this operation.</returns>
        Task<int> UpdateAsync(string id, object value);

        /// <summary>
        /// Deletes the object associated with the given id.
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <returns>The number of rows changed by this operation.</returns>
        Task<int> DeleteAsync(string id);

        /// <summary>
        /// Executes a list of persistence actions (create/update/delete/upsert) in a single DB transaction batch.
        /// </summary>
        /// <param name="persistenceActions">Ordered list of actions to apply.</param>
        /// <param name="actionableItems">Dictionary of items keyed by id for create/update/upsert actions.</param>
        /// <returns>A dictionary of row-change indicators keyed by each <see cref="PersistenceAction"/>.</returns>
        Task<IDictionary<PersistenceAction, int>> BatchPersistAsync(List<PersistenceAction> persistenceActions, Dictionary<string, object> actionableItems);

        /// <summary>
        /// Gets paged results of ids based on the given query and pagination parameters.
        /// The cache search API is supported to the extent this method is implemented.
        /// </summary>
        /// <param name="query">SQL select returning ids.</param>
        /// <param name="pageNum">1-based page number.</param>
        /// <param name="pageSize">Page size.</param>
        /// <param name="retrieveTotalNumResults">When true, also compute total row count.</param>
        /// <returns>Paged results of ids.</returns>
        Task<PagedResults<string>> SearchIdsByQueryAsync(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false);

    }
}
