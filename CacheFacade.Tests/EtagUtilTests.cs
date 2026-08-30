// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class EtagUtilTests
    {
        [Test]
        public void NextSequence_IsStrictlyIncreasing()
        {
            long first = EtagUtil.NextSequence();
            long second = EtagUtil.NextSequence();
            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void GenerateEtag_IsParseableSequentialString()
        {
            string etag = EtagUtil.GenerateEtag();
            Assert.That(EtagUtil.ParseSequentialEtag(etag), Is.GreaterThan(0));
            Assert.That(EtagUtil.GenerateSequentialEtag(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ParseSequentialEtag_ReturnsZeroForNullEmptyAndGuid()
        {
            Assert.That(EtagUtil.ParseSequentialEtag(null), Is.EqualTo(0));
            Assert.That(EtagUtil.ParseSequentialEtag(string.Empty), Is.EqualTo(0));
            Assert.That(EtagUtil.ParseSequentialEtag(Guid.NewGuid().ToString()), Is.EqualTo(0));
        }
    }
}
