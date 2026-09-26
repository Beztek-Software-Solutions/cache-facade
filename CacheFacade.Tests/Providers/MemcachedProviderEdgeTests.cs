// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class MemcachedProviderEdgeTests
    {
        [Test]
        public void ParseEndpoint_AcceptsHostPort()
        {
            MemcachedProvider.ParseEndpoint("127.0.0.1:11211", out string host, out int port);
            Assert.That(host, Is.EqualTo("127.0.0.1"));
            Assert.That(port, Is.EqualTo(11211));
        }

        [Test]
        public void ParseEndpoint_RejectsBlank()
        {
            Assert.Throws<ArgumentException>(() => MemcachedProvider.ParseEndpoint("  ", out _, out _));
            Assert.Throws<ArgumentException>(() => MemcachedProvider.ParseEndpoint(null, out _, out _));
        }

        [Test]
        public void ParseEndpoint_RejectsInvalidShape()
        {
            Assert.Throws<ArgumentException>(() => MemcachedProvider.ParseEndpoint("localhost", out _, out _));
            Assert.Throws<ArgumentException>(() => MemcachedProvider.ParseEndpoint("host:abc", out _, out _));
            Assert.Throws<ArgumentException>(() => MemcachedProvider.ParseEndpoint("a:b:c", out _, out _));
        }
    }
}
