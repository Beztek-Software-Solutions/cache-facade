// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Beztek.Facade.Queue;

    /// <summary>
    /// Test write behind processor that shuffles the messages being processed, to simulate out-of-order message processing from multple clients
    /// </summary>
    public class TestCacheWriteBehindProcessor : CacheWriteBehindProcessor<TestEtagCacheable>
    {
        public TestCacheWriteBehindProcessor(string cacheName)
            : base(cacheName)
        { }

        public override async Task<List<bool>> Process(List<Message> messageList)
        {
            // Shuffle the order of messages
            Random rng = new Random();
            List<Message> shuffledMessageList = messageList.OrderBy(a => rng.Next()).ToList();

            // Call parent implementation
            return await base.Process(shuffledMessageList).ConfigureAwait(false);
        }
    }
}
