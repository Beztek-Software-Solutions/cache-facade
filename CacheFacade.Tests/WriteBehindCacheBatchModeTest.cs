// Copyright (c) Beztek Software Solutions. All rights reserved.

using NUnit.Framework;

namespace Beztek.Facade.Cache.Tests
{
    [TestFixture]
    public class WriteBehindCacheBatchModeTest : WriteBehindCacheIndividualModeTest
    {
        [SetUp]
        public void SetUp()
        {
            this.CacheType = CacheType.WriteBehind;
        }
    }
}
