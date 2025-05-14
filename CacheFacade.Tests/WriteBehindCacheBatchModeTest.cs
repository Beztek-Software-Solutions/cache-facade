// Copyright (c) Beztek Software Solutions. All rights reserved.

using NUnit.Framework;

namespace Beztek.Facade.Cache.Tests
{
    [TestFixture]
    public class WriteBehindCacheBatchModeTest : WriteBehindCacheIndividualModeTest
    {
        [SetUp]
        public new void SetUp()
        {
            this.CacheType = CacheType.WriteBehind;
        }
    }
}
