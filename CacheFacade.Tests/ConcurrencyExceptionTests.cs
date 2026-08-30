// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using NUnit.Framework;

    [TestFixture]
    public class ConcurrencyExceptionTests
    {
        [Test]
        public void Constructors_SetMessageAndInnerException()
        {
            var withMessage = new ConcurrencyException("stale");
            Assert.That(withMessage.Message, Is.EqualTo("stale"));

            var inner = new InvalidOperationException("inner");
            var withInner = new ConcurrencyException("outer", inner);
            Assert.That(withInner.InnerException, Is.SameAs(inner));

            var empty = new ConcurrencyException();
            Assert.That(empty, Is.Not.Null);
        }
    }
}
