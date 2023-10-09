// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class NonPersistentCacheTest : AbstractCacheTest
    {
        [SetUp]
        public void SetUp()
        {
            this.CacheType = CacheType.NonPersistent;
        }
    }
}
