// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message, Exception inner)
            : base(message, inner)
        { }

        public ConcurrencyException(string message)
            : base(message)
        { }

        public ConcurrencyException()
            : base()
        { }
    }
}
