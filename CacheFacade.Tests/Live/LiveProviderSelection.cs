// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Selects which providers run live container tests via <c>CACHEFACADE_LIVE_PROVIDERS</c>.
    /// <para>
    /// Examples:
    /// <list type="bullet">
    /// <item><c>redis</c> — one provider</item>
    /// <item><c>redis,memcached,hazelcast</c> — subset</item>
    /// <item><c>all</c> — every supported provider (LocalMemory in-process + containers for the rest)</item>
    /// </list>
    /// Unset / empty → no live fixtures are registered (default unit CI stays fast and Docker-free).
    /// </para>
    /// </summary>
    public static class LiveProviderSelection
    {
        public const string EnvVar = "CACHEFACADE_LIVE_PROVIDERS";

        private static readonly CacheProviderType[] AllProviders =
        {
            CacheProviderType.LocalMemory,
            CacheProviderType.Redis,
            CacheProviderType.Dragonfly,
            CacheProviderType.KeyDB,
            CacheProviderType.Garnet,
            CacheProviderType.Memcached,
            CacheProviderType.Hazelcast,
        };

        /// <summary>True when the env var is set to a non-empty value.</summary>
        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

        /// <summary>Providers requested for this process; empty when live tests should not run.</summary>
        public static IReadOnlyList<CacheProviderType> Resolve()
        {
            string raw = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<CacheProviderType>();

            var selected = new List<CacheProviderType>();
            foreach (string token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "all" or "*")
                    return AllProviders.ToList();

                if (TryParse(t, out CacheProviderType provider) && !selected.Contains(provider))
                    selected.Add(provider);
                else
                    throw new ArgumentException(
                        $"Unknown provider '{token}' in {EnvVar}. " +
                        "Use: all | localmemory | redis | dragonfly | keydb | garnet | memcached | hazelcast " +
                        "(comma-separated for a subset).");
            }

            return selected;
        }

        public static bool TryParse(string token, out CacheProviderType provider)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "localmemory":
                case "local":
                case "memory":
                    provider = CacheProviderType.LocalMemory;
                    return true;
                case "redis":
                    provider = CacheProviderType.Redis;
                    return true;
                case "dragonfly":
                case "df":
                    provider = CacheProviderType.Dragonfly;
                    return true;
                case "keydb":
                case "key":
                    provider = CacheProviderType.KeyDB;
                    return true;
                case "garnet":
                    provider = CacheProviderType.Garnet;
                    return true;
                case "memcached":
                case "memcache":
                case "mc":
                    provider = CacheProviderType.Memcached;
                    return true;
                case "hazelcast":
                case "hz":
                    provider = CacheProviderType.Hazelcast;
                    return true;
                default:
                    provider = default;
                    return false;
            }
        }
    }
}
