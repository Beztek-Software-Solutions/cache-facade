// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Retry / backoff settings for <see cref="EtagEntityUpdateHelper"/>.
    /// </summary>
    public sealed class EtagEntityUpdateOptions
    {
        /// <summary>Number of retries after the first attempt (total attempts = MaxRetryCount + 1).</summary>
        public int MaxRetryCount { get; set; } = 5;

        /// <summary>Initial backoff before the first retry; doubles after each failed attempt when exponential.</summary>
        public int InitialRetryDelayMillis { get; set; } = 5;

        /// <summary>Upper bound for a single retry delay.</summary>
        public int MaxRetryDelayMillis { get; set; } = 200;

        /// <summary>
        /// When true (default), delay doubles each retry (capped). When false, every retry uses
        /// <see cref="InitialRetryDelayMillis"/> (still capped by <see cref="MaxRetryDelayMillis"/>).
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>Create a copy with the same values.</summary>
        public EtagEntityUpdateOptions Clone() => new EtagEntityUpdateOptions
        {
            MaxRetryCount = MaxRetryCount,
            InitialRetryDelayMillis = InitialRetryDelayMillis,
            MaxRetryDelayMillis = MaxRetryDelayMillis,
            UseExponentialBackoff = UseExponentialBackoff,
        };

        /// <summary>Normalize invalid values to safe defaults.</summary>
        public EtagEntityUpdateOptions Normalized()
        {
            var copy = Clone();
            if (copy.MaxRetryCount < 0)
                copy.MaxRetryCount = 0;
            if (copy.InitialRetryDelayMillis < 0)
                copy.InitialRetryDelayMillis = 0;
            if (copy.MaxRetryDelayMillis < 0)
                copy.MaxRetryDelayMillis = 0;
            if (copy.MaxRetryDelayMillis < copy.InitialRetryDelayMillis)
                copy.MaxRetryDelayMillis = copy.InitialRetryDelayMillis;
            return copy;
        }
    }
}
