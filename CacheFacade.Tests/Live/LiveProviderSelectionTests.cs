// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class LiveProviderSelectionTests
    {
        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, null);
        }

        [Test]
        public void Resolve_Unset_ReturnsEmpty()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, null);
            Assert.That(LiveProviderSelection.Resolve(), Is.Empty);
            Assert.That(LiveProviderSelection.IsConfigured, Is.False);
        }

        [Test]
        public void Resolve_SingleProvider_ReturnsOne()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "redis");
            Assert.That(LiveProviderSelection.Resolve(), Is.EqualTo(new[] { CacheProviderType.Redis }));
        }

        [Test]
        public void Resolve_Subset_ParsesAliases()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "local, mc, hz, df");
            Assert.That(
                LiveProviderSelection.Resolve(),
                Is.EqualTo(new[]
                {
                    CacheProviderType.LocalMemory,
                    CacheProviderType.Memcached,
                    CacheProviderType.Hazelcast,
                    CacheProviderType.Dragonfly,
                }));
        }

        [Test]
        public void Resolve_All_ReturnsEveryProvider()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "all");
            IReadOnlyList<CacheProviderType> providers = LiveProviderSelection.Resolve();
            Assert.That(providers, Does.Contain(CacheProviderType.LocalMemory));
            Assert.That(providers, Does.Contain(CacheProviderType.Redis));
            Assert.That(providers, Does.Contain(CacheProviderType.Dragonfly));
            Assert.That(providers, Does.Contain(CacheProviderType.KeyDB));
            Assert.That(providers, Does.Contain(CacheProviderType.Garnet));
            Assert.That(providers, Does.Contain(CacheProviderType.Memcached));
            Assert.That(providers, Does.Contain(CacheProviderType.Hazelcast));
            Assert.That(providers.Count, Is.EqualTo(7));
        }

        [Test]
        public void Resolve_UnknownToken_Throws()
        {
            Environment.SetEnvironmentVariable(LiveProviderSelection.EnvVar, "cosmos");
            Assert.Throws<ArgumentException>(() => LiveProviderSelection.Resolve());
        }
    }
}
