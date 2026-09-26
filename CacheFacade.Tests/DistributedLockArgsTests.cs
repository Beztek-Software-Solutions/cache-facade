// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class DistributedLockArgsTests
    {
        [Test]
        public void Normalize_RejectsEmptyName()
        {
            Assert.Throws<ArgumentException>(() => DistributedLockArgs.Normalize("", 10, 10, 1));
            Assert.Throws<ArgumentException>(() => DistributedLockArgs.Normalize(null, 10, 10, 1));
        }

        [Test]
        public void Normalize_RejectsNegativeTimeout()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DistributedLockArgs.Normalize("k", -1, 10, 1));
        }

        [Test]
        public void Normalize_RejectsNonPositiveLease()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DistributedLockArgs.Normalize("k", 10, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DistributedLockArgs.Normalize("k", 10, -5, 1));
        }

        [Test]
        public void Normalize_ClampsNonPositiveRetryToOne()
        {
            Assert.That(DistributedLockArgs.Normalize("k", 10, 10, 0), Is.EqualTo(1));
            Assert.That(DistributedLockArgs.Normalize("k", 10, 10, -3), Is.EqualTo(1));
            Assert.That(DistributedLockArgs.Normalize("k", 10, 10, 7), Is.EqualTo(7));
        }
    }
}
