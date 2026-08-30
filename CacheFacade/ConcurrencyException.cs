// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Thrown when an optimistic-concurrency check fails (for example, replace with a stale
    /// <see cref="IEtagEntity.Etag"/>). <see cref="EtagEntityUpdateHelper"/> retries on this exception.
    /// </summary>
    public class ConcurrencyException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a message and inner exception.
        /// </summary>
        /// <param name="message">Error description.</param>
        /// <param name="inner">Underlying cause (often a prior concurrency failure after retries).</param>
        public ConcurrencyException(string message, Exception inner)
            : base(message, inner)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a message.
        /// </summary>
        /// <param name="message">Error description.</param>
        public ConcurrencyException(string message)
            : base(message)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a default message.
        /// </summary>
        public ConcurrencyException()
            : base()
        { }
    }
}
