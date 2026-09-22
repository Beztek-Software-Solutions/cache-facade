// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.IO;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using Enyim.Caching;
    using Enyim.Caching.Memcached;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class MemcachedProviderTests
    {
        private Mock<IMemcachedClient> client;
        private MemcachedProvider provider;

        [SetUp]
        public void SetUp()
        {
            this.client = new Mock<IMemcachedClient>();
            this.provider = new MemcachedProvider(this.client.Object, "orders", TimeSpan.FromMinutes(5));
        }

        [Test]
        public void Get_DeserializesStringPayload()
        {
            var expected = new TestCacheable("k1", "v1");
            string json = SerializationUtil.ByteToString(SerializationUtil.Serialize(SerializationType.Json, expected));
            this.client.Setup(c => c.Get<string>("orders:k1")).Returns(json);

            TestCacheable actual = this.provider.Get<TestCacheable>("k1");
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Get_MissingKey_ReturnsDefault()
        {
            this.client.Setup(c => c.Get<string>(It.IsAny<string>())).Returns((string)null);
            Assert.That(this.provider.Get<TestCacheable>("missing"), Is.Null);
        }

        [Test]
        public void Put_StoresPrefixedKey()
        {
            var value = new TestCacheable("k1", "v1");
            this.client.Setup(c => c.Store(StoreMode.Set, "orders:k1", It.IsAny<object>(), It.IsAny<TimeSpan>())).Returns(true);

            this.provider.Put("k1", value);

            this.client.Verify(c => c.Store(StoreMode.Set, "orders:k1", It.IsAny<object>(), TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Test]
        public void Put_WhenStoreFails_Throws()
        {
            this.client.Setup(c => c.Store(StoreMode.Set, It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>())).Returns(false);
            Assert.Throws<IOException>(() => this.provider.Put("k1", new TestCacheable("k1", "v1")));
        }

        [Test]
        public void Remove_ReturnsPreviousValue()
        {
            var expected = new TestCacheable("k1", "v1");
            string json = SerializationUtil.ByteToString(SerializationUtil.Serialize(SerializationType.Json, expected));
            this.client.Setup(c => c.Get<string>("orders:k1")).Returns(json);
            this.client.Setup(c => c.Remove("orders:k1")).Returns(true);

            Assert.That(this.provider.Remove<TestCacheable>("k1"), Is.EqualTo(expected));
            this.client.Verify(c => c.Remove("orders:k1"), Times.Once);
        }

        [Test]
        public void Clear_ThrowsNotSupported()
        {
            Assert.Throws<NotSupportedException>(() => this.provider.Clear());
        }
    }
}
