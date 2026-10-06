// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Beztek.Facade.Sql;

    /// <summary>
    /// <see cref="IPersistenceService"/> backed by <see cref="ISqlFacade"/> and an
    /// <see cref="ISqlGenerator{T}"/> for entity type <typeparamref name="T"/>.
    /// Soft-deleted <see cref="IWriteBehindEntity"/> rows are treated as missing on read.
    /// </summary>
    /// <remarks>
    /// Overrides <see cref="IPersistenceService.GetByIdsAsync"/> with one SQL
    /// <c>IN (...)</c> load via <see cref="ISqlGenerator{T}.GetSqlSelectByIds"/>. That override is
    /// optional for correctness (the interface default loops <see cref="GetByIdAsync"/>) but
    /// required for true 1+1 <see cref="ICache.SearchByQueryAsync{T}"/> on this SQL path.
    /// </remarks>
    /// <typeparam name="T">Entity type mapped by the SQL generator.</typeparam>
    public class SqlPersistenceService<T> : IPersistenceService
    {
        private readonly ISqlFacade sqlFacade;
        private readonly ISqlGenerator<T> sqlGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlPersistenceService{T}"/> class.
        /// </summary>
        /// <param name="sqlFacade">SQL facade used to execute statements.</param>
        /// <param name="sqlGenerator">Dialect-specific CRUD/upsert SQL for <typeparamref name="T"/>.</param>
        public SqlPersistenceService(ISqlFacade sqlFacade, ISqlGenerator<T> sqlGenerator)
        {
            this.sqlFacade = sqlFacade;
            this.sqlGenerator = sqlGenerator;
        }

        /// <inheritdoc />
        public virtual async Task<object> GetByIdAsync(string id)
        {
            SqlSelect sqlSelect = this.sqlGenerator.GetSqlSelect(id);
            T result = sqlFacade.GetSingleResult<T>(sqlSelect);
            if (result is IWriteBehindEntity writeBehindEntity && writeBehindEntity.IsDeleted)
            {
                // Soft-delete tombstone: treat as missing for cache hydration and API reads.
                return await Task.FromResult<object>(null).ConfigureAwait(false);
            }

            return await Task.FromResult<object>(result).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <remarks>
        /// True batch hydrate for search: one <see cref="ISqlFacade.GetResults{T}(SqlSelect)"/> using
        /// <see cref="ISqlGenerator{T}.GetSqlSelectByIds"/>. Skipping this override would fall back to
        /// the interface default (N× <see cref="GetByIdAsync"/>).
        /// </remarks>
        public virtual async Task<IDictionary<string, object>> GetByIdsAsync(IReadOnlyList<string> ids)
        {
            Dictionary<string, object> map = new Dictionary<string, object>(System.StringComparer.Ordinal);
            if (ids == null || ids.Count == 0)
            {
                return await Task.FromResult<IDictionary<string, object>>(map).ConfigureAwait(false);
            }

            List<string> distinct = new List<string>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    continue;
                }

                distinct.Add(id);
            }

            if (distinct.Count == 0)
            {
                return await Task.FromResult<IDictionary<string, object>>(map).ConfigureAwait(false);
            }

            SqlSelect sqlSelect = this.sqlGenerator.GetSqlSelectByIds(distinct);
            IList<T> loaded = this.sqlFacade.GetResults<T>(sqlSelect);
            foreach (T item in loaded)
            {
                if (item == null)
                {
                    continue;
                }

                if (item is IWriteBehindEntity writeBehindEntity && writeBehindEntity.IsDeleted)
                {
                    continue;
                }

                string entityId = this.sqlGenerator.GetId(item);
                if (!string.IsNullOrEmpty(entityId))
                {
                    map[entityId] = item;
                }
            }

            return await Task.FromResult<IDictionary<string, object>>(map).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(string id, object value)
        {
            List<ISqlWrite> updateStatements = this.sqlGenerator.GetSqlUpdate(id, (T)value);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(updateStatements);
            return await Task.FromResult<int>(GetIsWritten(results)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<int> CreateAsync(string id, object value)
        {
            List<ISqlWrite> insertStatements = this.sqlGenerator.GetSqlInsert(id, (T)value);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(insertStatements);
            return await Task.FromResult<int>(GetIsWritten(results)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(string id)
        {
            List<ISqlWrite> deleteStatements = this.sqlGenerator.GetSqlDelete(id);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(deleteStatements);
            return await Task.FromResult<int>(GetIsWritten(results)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<PagedResults<string>> SearchIdsByQueryAsync(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false)
        {
            return await Task.FromResult(sqlFacade.GetPagedResults<string>(query, pageNum, pageSize, retrieveTotalNumResults)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public virtual async Task<IDictionary<PersistenceAction, int>> BatchPersistAsync(List<PersistenceAction> persistenceActions, Dictionary<string, object> actionableItems)
        {
            List<ISqlWrite> allWrites = new List<ISqlWrite>();
            List<List<ISqlWrite>> batchWriteList = new List<List<ISqlWrite>>();
            foreach (PersistenceAction persistenceAction in persistenceActions)
            {
                List<ISqlWrite> batchWrites = new List<ISqlWrite>();
                switch (persistenceAction.WriteType)
                {
                    case WriteType.Create:
                    case WriteType.Update:
                    case WriteType.Upsert:
                        // Write-behind drain uses upsert for create/update; keeps a single write path.
                        batchWrites = this.sqlGenerator.GetSqlUpsert(persistenceAction.Id, (T)actionableItems[persistenceAction.Id]);
                        break;
                    case WriteType.Delete:
                        batchWrites = this.sqlGenerator.GetSqlDelete(persistenceAction.Id);
                        break;
                }

                batchWriteList.Add(batchWrites);
                allWrites.AddRange(batchWrites);
            }

            // Execute all the SQL
            IList<int> numWritesList = this.sqlFacade.ExecuteMultiSqlWrite(allWrites);

            // Iterate through each set of batch writes, and determine if the object got written.
            // actionIndex indexes persistenceActions; writeOffset indexes into the flat numWritesList
            // (an upsert may contribute multiple ISqlWrite statements).
            IDictionary<PersistenceAction, int> result = new Dictionary<PersistenceAction, int>();
            int writeOffset = 0;
            for (int actionIndex = 0; actionIndex < batchWriteList.Count; actionIndex++)
            {
                List<ISqlWrite> batchWrites = batchWriteList[actionIndex];
                IEnumerable<int> results = numWritesList.Skip(writeOffset).Take(batchWrites.Count);
                int numWrites = GetIsWritten(results);
                result.Add(persistenceActions[actionIndex], numWrites);
                writeOffset += batchWrites.Count;
            }

            return await Task.FromResult<IDictionary<PersistenceAction, int>>(result).ConfigureAwait(false);
        }

        // Internal

        /// <summary>
        /// Returns 1 if any of the batch writes wrote anything, or 0 otherwise.
        /// </summary>
        /// <param name="numBatchWrites">Per-statement row counts from <see cref="ISqlFacade.ExecuteMultiSqlWrite"/>.</param>
        /// <returns>1 if any write affected rows; otherwise 0.</returns>
        private static int GetIsWritten(IEnumerable<int> numBatchWrites)
        {
            foreach (int currResult in numBatchWrites)
            {
                if (currResult > 0)
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}
