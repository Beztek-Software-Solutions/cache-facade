// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System.Collections;
    using NUnit.Framework;

    /// <summary>
    /// Feeds <see cref="LiveProviderTests"/> one fixture per selected provider.
    /// When <c>CACHEFACADE_LIVE_PROVIDERS</c> is unset, yields nothing (no live tests discovered).
    /// </summary>
    public static class LiveProviderFixtureSource
    {
        public static IEnumerable Providers()
        {
            foreach (CacheProviderType provider in LiveProviderSelection.Resolve())
            {
                yield return new TestFixtureData(provider)
                    .SetArgDisplayNames(provider.ToString());
            }
        }
    }
}
