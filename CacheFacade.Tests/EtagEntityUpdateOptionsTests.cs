// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class EtagEntityUpdateOptionsTests
    {
        [Test]
        public void Normalized_ClampsNegativeAndMaxBelowInitial()
        {
            var options = new EtagEntityUpdateOptions
            {
                MaxRetryCount = -1,
                InitialRetryDelayMillis = -5,
                MaxRetryDelayMillis = -10,
            };

            EtagEntityUpdateOptions normalized = options.Normalized();
            Assert.That(normalized.MaxRetryCount, Is.EqualTo(0));
            Assert.That(normalized.InitialRetryDelayMillis, Is.EqualTo(0));
            Assert.That(normalized.MaxRetryDelayMillis, Is.EqualTo(0));

            var maxBelowInitial = new EtagEntityUpdateOptions
            {
                InitialRetryDelayMillis = 50,
                MaxRetryDelayMillis = 10,
            }.Normalized();
            Assert.That(maxBelowInitial.MaxRetryDelayMillis, Is.EqualTo(50));
        }

        [Test]
        public void CalculateRetryDelayMillis_NegativeIndexUsesInitial()
        {
            var options = new EtagEntityUpdateOptions
            {
                InitialRetryDelayMillis = 7,
                MaxRetryDelayMillis = 100,
                UseExponentialBackoff = true,
            };
            Assert.That(EtagEntityUpdateHelper.CalculateRetryDelayMillis(-1, options), Is.EqualTo(7));
        }
    }
}
