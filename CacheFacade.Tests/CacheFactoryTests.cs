// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Sql;
    using NUnit.Framework;

    [TestFixture]
    public class CacheFactoryTests
    {
        [Test]
        public void GetOrCreateCacheTest()
        {
            string cacheName = "CacheFactoryTests";
            CacheType cacheType = CacheType.NonPersistent;
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType));
            Assert.IsNotNull(cache);
        }

        [Test]
        public void WriteThroughConfiguratationValidationTest()
        {
            string cacheName = "WriteThroughConfiguratationValidation";
            CacheType cacheType = CacheType.WriteThrough;
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);

            Assert.Throws<ArgumentException>(() => CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType)));
        }

        [Test]
        public void WriteBehindConfiguratationValidationTest1()
        {
            string cacheName = "WriteBehindConfiguratationValidation1";
            CacheType cacheType = CacheType.WriteThrough;
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);

            Assert.Throws<ArgumentException>(() => CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType)));
        }

        [Test]
        public void WriteBehindConfiguratationValidationTest2()
        {
            string cacheName = "WriteBehindConfiguratationValidation2";
            CacheType cacheType = CacheType.WriteThrough;
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);
            ISqlFacade sqlUtil = SqlFacadeFactory.GetSqlFacade(new SqlFacadeConfig(Beztek.Facade.Sql.DbType.SQLITE, "Data Source=:memory:"));
            IPersistenceService persistenceService = new SqlPersistenceService<TestEtagCacheable>(sqlUtil, new TestSqlGenerator());

            Assert.Throws<ArgumentException>(() => CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType)));
        }

        [Test]
        public void GetCacheTest()
        {
            string cacheName = "CacheFactoryGetCache";
            CacheType cacheType = CacheType.NonPersistent;
            ICacheProviderConfiguration cacheProviderConfiguration = new LocalMemoryProviderConfiguration(cacheName, 300000);
            Assert.IsNull(CacheFactory.GetCache(cacheName));

            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(cacheProviderConfiguration, cacheType));
            Assert.IsNotNull(cache);
            Assert.IsNotNull(CacheFactory.GetCache(cacheName));
        }
    }
}
