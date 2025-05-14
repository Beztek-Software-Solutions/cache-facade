// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class WriteBehindCacheIndividualModeTest : AbstractCacheTest
    {
        [SetUp]
        public void SetUp()
        {
            this.CacheType = CacheType.WriteBehind;
        }
    }
}
