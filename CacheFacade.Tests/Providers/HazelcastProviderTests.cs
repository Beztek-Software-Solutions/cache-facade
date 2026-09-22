// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using Hazelcast.DistributedObjects;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class HazelcastProviderTests
    {
        private Mock<IHMap<string, byte[]>> map;
        private HazelcastProvider provider;

        [SetUp]
        public void SetUp()
        {
            this.map = new Mock<IHMap<string, byte[]>>();
            this.provider = new HazelcastProvider(this.map.Object, TimeSpan.FromMinutes(5));
        }

        [Test]
        public void Get_DeserializesPayload()
        {
            var expected = new TestCacheable("k1", "v1");
            byte[] payload = SerializationUtil.Serialize(SerializationType.Json, expected);
            this.map.Setup(m => m.GetAsync("k1")).Returns(Task.FromResult(payload));

            Assert.That(this.provider.Get<TestCacheable>("k1"), Is.EqualTo(expected));
        }

        [Test]
        public void Get_Missing_ReturnsDefault()
        {
            this.map.Setup(m => m.GetAsync("missing")).Returns(Task.FromResult<byte[]>(null));
            Assert.That(this.provider.Get<TestCacheable>("missing"), Is.Null);
        }

        [Test]
        public void Put_SetsWithTtl()
        {
            var value = new TestCacheable("k1", "v1");
            this.map.Setup(m => m.SetAsync("k1", It.IsAny<byte[]>(), TimeSpan.FromMinutes(5))).Returns(Task.CompletedTask);

            this.provider.Put("k1", value);

            this.map.Verify(m => m.SetAsync("k1", It.IsAny<byte[]>(), TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Test]
        public void Remove_ReturnsPrevious()
        {
            var expected = new TestCacheable("k1", "v1");
            byte[] payload = SerializationUtil.Serialize(SerializationType.Json, expected);
            this.map.Setup(m => m.RemoveAsync("k1")).Returns(Task.FromResult(payload));

            Assert.That(this.provider.Remove<TestCacheable>("k1"), Is.EqualTo(expected));
        }

        [Test]
        public void Clear_ClearsMap()
        {
            this.map.Setup(m => m.ClearAsync()).Returns(Task.CompletedTask);
            Assert.That(this.provider.Clear(), Is.True);
            this.map.Verify(m => m.ClearAsync(), Times.Once);
        }
    }
}
