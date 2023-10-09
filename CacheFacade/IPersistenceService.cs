// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Beztek.Facade.Sql;

    public interface IPersistenceService
    {
        /// <summary>
        /// Creates the object and throws an exception if the object already exists
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="value">The object to be created.</param>
        /// <returns>The number of rows changed by this operation.</returns>
        Task<int> CreateAsync(string id, object value);

        /// <summary>
        /// Gets the object associated with the given id
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <returns>The object associated with the given id.</returns>
        Task<object> GetByIdAsync(string id);

        /// <summary>
        /// Updates the object (does nothing if the object does not exists)
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="value">The object to be created.</param>
        /// <returns>The number of rows changed by this operation.</returns>
        Task<int> UpdateAsync(string id, object value);

        /// <summary>
        /// Deletes the object associated with the given id
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <returns>The number of rows changed by this operation.</returns>
        Task<int> DeleteAsync(string id);

        /// <summary>
        /// Takes a list of persistance actions (a combination of database create/update/delete statements)
        /// and executes them all in a single DB transaction
        /// </summary>
        /// <param name="id">The id of the object.</param>
        /// <param name="actionableItems">Dictionary of items on which the persistence actions need to be performed, keyed by their ids.</param>
        /// <returns>A dictionary of the numbers of rows changed corresponding to each unique persistenceAction.</returns>
        Task<IDictionary<PersistenceAction, int>> BatchPersistAsync(List<PersistenceAction> persistenceActions, Dictionary<string, object> actionableItems);

        /// <summary>
        /// Gets PagedResults of ids based on the given query and pagination parameters. The cache will support queries to the extent
        /// that this method is supported by the implementation of this interface.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="pageNum"></param>
        /// <param name="pageSize"></param>
        /// <param name="retrieveTotalNumResults"></param>
        /// <returns>PagedResults of ids based on the given query and pagination parameters</returns>
        Task<PagedResults<string>> SearchIdsByQueryAsync(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false);

    }
}
