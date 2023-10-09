// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue;

    public class CacheWriteBehindProcessor<T> : IMessageProcessor
    {
        private readonly string cacheName;
        private Cache cache;

        public CacheWriteBehindProcessor(string cacheName)
        {
            this.cacheName = cacheName;
        }

        public virtual async Task<bool> Process(Message message)
        {
            // Piggy-back off the more complex logic that can process lists of messages
            return (await this.Process(new List<Message> { message }).ConfigureAwait(false))[0];
        }

        // Since we need to handle out-of-order messages, we need additional logic based on if the entity is in the cache and DB or not.
        // i.e., the latest state of the cache and the content of the DB determines whether we insert, update or delete
        public virtual async Task<List<bool>> Process(List<Message> messageList)
        {
            // This holds a set of only one entry per id, for each list of messages
            HashSet<string> uniqueIdSet = new HashSet<string>();

            Parallel.ForEach(messageList, message => {
                string id = message.RawMessage.ToString();
                uniqueIdSet.Add(id);
            });

            // List of persistence actions to be done for each unique id
            List<PersistenceAction> uniquePersistenceActionList = new List<PersistenceAction>();

            // Dictionary of mapped entities which come from the cache and are needed for saving
            Dictionary<string, object> actionableItems = new Dictionary<string, object>();

            // Keep track of ids where there is no chagne needed in the DB, to remove it prior to making the DB update
            List<string> toRemoveIds = new List<string>();
            foreach (string id in uniqueIdSet)
            {
                // Get from the underlying cache provider rather than the cache itself, since the cache provider is the source of truth for write-behind actions
                T cachedItem = await this.GetCache().CacheProvider.GetAsync<T>(id).ConfigureAwait(false);

                // Also get the item from the DB.
                T savedItem = (T)await this.GetCache().PersistenceService.GetByIdAsync(id).ConfigureAwait(false);

                // Insert or Update case: item is in the cache
                if (cachedItem != null)
                {
                    actionableItems.Add(id, cachedItem);

                    // Insert case: item not in DB
                    if (savedItem == null)
                    {
                        uniquePersistenceActionList.Add(new PersistenceAction(id, WriteType.Create));
                    }

                    // Update case: item in DB
                    else
                    {
                        // If the cached item is the same as the saved item, then no need to execute an UPDATE statement
                        if (cachedItem.Equals(savedItem))
                        {
                            toRemoveIds.Add(id);
                        }
                        else
                        {
                            uniquePersistenceActionList.Add(new PersistenceAction(id, WriteType.Update));
                        }
                    }
                }

                // Delete case: item is not in the cache
                else
                {
                    actionableItems.Add(id, default(T));

                    // If the item does not exist in the DB, then no need to execute a DELETE statement
                    if (savedItem == null)
                    {
                        toRemoveIds.Add(id);
                    }
                    else
                    {
                        uniquePersistenceActionList.Add(new PersistenceAction(id, WriteType.Delete));
                    }
                }
            }

            // Remove the ids from the actionable items
            Parallel.ForEach(toRemoveIds, id => actionableItems.Remove(id));

            // Execute the unique persistence actions. We do not need the results. If there are no exceptions we conclude that all succeeded
            if (uniquePersistenceActionList.Count > 0)
            {
                // We have not whittled down our actinalable items to the minimal set of writes we need to perform on the DB, and we will do them all in one transaction
                await this.GetCache().PersistenceService.BatchPersistAsync(uniquePersistenceActionList, actionableItems).ConfigureAwait(false);
            }

            // Build a list of boolean flags for each original message
            List<bool> results = new List<bool>();
            Parallel.ForEach(messageList, message => results.Add(true));

            return results;
        }

        // Internal

        private Cache GetCache()
        {
            if (this.cache == null)
            {
                this.cache = (Cache)CacheFactory.GetCache(this.cacheName);
            }

            return this.cache;
        }
    }
}
