// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class WriteBehindCacheIndividualModeTest : AbstractCacheTest
    {
        [SetUp]
        public new void SetUp()
        {
            this.CacheType = CacheType.WriteBehind;
        }
    }
}
