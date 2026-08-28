// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// Etag helpers. All library-generated etags are short sequential strings (UTC epoch ms,
    /// strictly increasing within the process) so write-through and write-behind share the same format.
    /// </summary>
    public static class EtagUtil
    {
        private static long lastIssued;

        /// <summary>
        /// Next monotonic sequence (UTC epoch ms, strictly increasing in-process). Used for etags and
        /// <see cref="WriteBehindMessage.Sequence"/> so create/update/delete share one clock.
        /// </summary>
        public static long NextSequence()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (true)
            {
                long previous = Volatile.Read(ref lastIssued);
                long issued = Math.Max(now, previous + 1);
                if (Interlocked.CompareExchange(ref lastIssued, issued, previous) == previous)
                {
                    return issued;
                }
            }
        }

        /// <summary>
        /// Generates a short sequential etag (decimal string of <see cref="NextSequence"/>).
        /// Same format for write-through and write-behind.
        /// </summary>
        public static string GenerateEtag()
        {
            return NextSequence().ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Alias of <see cref="GenerateEtag"/>. Prefer <see cref="GenerateEtag"/> in new code.
        /// </summary>
        public static string GenerateSequentialEtag()
        {
            return GenerateEtag();
        }

        /// <summary>
        /// Parses a sequential etag to a comparable version. Returns 0 if the value is not a decimal long
        /// (e.g. legacy GUID etags from older clients).
        /// </summary>
        public static long ParseSequentialEtag(string etag)
        {
            if (string.IsNullOrEmpty(etag))
            {
                return 0;
            }

            return long.TryParse(etag, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sequence)
                ? sequence
                : 0;
        }
    }
}
