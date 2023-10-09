// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Beztek.Facade.Sql;

    public class SqlPersistenceService<T> : IPersistenceService
    {
        private readonly ISqlFacade sqlFacade;
        private readonly ISqlGenerator<T> sqlGenerator;

        public SqlPersistenceService(ISqlFacade sqlFacade, ISqlGenerator<T> sqlGenerator)
        {
            this.sqlFacade = sqlFacade;
            this.sqlGenerator = sqlGenerator;
        }

        public async Task<object> GetByIdAsync(string id)
        {
            SqlSelect sqlSelect = this.sqlGenerator.GetSqlSelect(id);
            return sqlFacade.GetSingleResult<T>(sqlSelect);
        }

        public async virtual Task<int> UpdateAsync(string id, object value)
        {
            List<ISqlWrite> updateStatements = this.sqlGenerator.GetSqlUpdate(id, (T)value);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(updateStatements);
            return GetIsWritten(results);
        }

        public async virtual Task<int> CreateAsync(string id, object value)
        {
            List<ISqlWrite> insertStatements = this.sqlGenerator.GetSqlInsert(id, (T)value);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(insertStatements);
            return GetIsWritten(results);
        }

        public virtual async Task<int> DeleteAsync(string id)
        {
            List<ISqlWrite> deleteStatements = this.sqlGenerator.GetSqlDelete(id);
            IList<int> results = sqlFacade.ExecuteMultiSqlWrite(deleteStatements);
            return GetIsWritten(results);
        }

        public async virtual Task<PagedResults<string>> SearchIdsByQueryAsync(SqlSelect query, int pageNum, int pageSize, bool retrieveTotalNumResults = false)
        {
            return sqlFacade.GetPagedResults<string>(query, pageNum, pageSize, retrieveTotalNumResults);
        }

        public async Task<IDictionary<PersistenceAction, int>> BatchPersistAsync(List<PersistenceAction> persistenceActions, Dictionary<string, object> actionableItems)
        {
            List<ISqlWrite> allWrites = new List<ISqlWrite>();
            List<List<ISqlWrite>> batchWriteList = new List<List<ISqlWrite>>();
            foreach (PersistenceAction persistenceAction in persistenceActions)
            {
                List<ISqlWrite> batchWrites = new List<ISqlWrite>();
                switch (persistenceAction.WriteType)
                {
                    case WriteType.Create:
                        batchWrites = this.sqlGenerator.GetSqlInsert(persistenceAction.Id, (T)actionableItems[persistenceAction.Id]);
                        break;
                    case WriteType.Update:
                        batchWrites = this.sqlGenerator.GetSqlUpdate(persistenceAction.Id, (T)actionableItems[persistenceAction.Id]);
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

            // Iterate through each set of batch writes, and determine if the object got written
            IDictionary<PersistenceAction, int> result = new Dictionary<PersistenceAction, int>();
            int actionIndex = 0;
            foreach (List<ISqlWrite> batchWrites in batchWriteList)
            {
                IEnumerable<int> results = numWritesList.Skip(actionIndex).Take(batchWrites.Count);
                int numWrites = GetIsWritten(results);
                result.Add(persistenceActions[actionIndex], numWrites);
                actionIndex = actionIndex + batchWrites.Count;
            }

            return result;
        }

        // Internal

        /// <summary>
        /// Returns 1 if any of the batch writes wrote anything, or 0 otherwise
        /// </summary>
        /// <param name="numBatchWrites"></param>
        /// <returns></returns>
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
