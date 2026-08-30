// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Beztek.Facade.Sql;

    /// <summary>
    /// Persistence backend used by write-through and write-behind caches (typically <see cref="SqlPersistenceService{T}"/>).
    /// </summary>
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
