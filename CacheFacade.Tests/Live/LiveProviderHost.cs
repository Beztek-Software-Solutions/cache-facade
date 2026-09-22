// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using System;
    using System.Threading.Tasks;
    using Beztek.Facade.Cache;
    using DotNet.Testcontainers.Builders;
    using DotNet.Testcontainers.Containers;
    using Testcontainers.Redis;

    /// <summary>
    /// Starts (or skips) a throwaway provider backend for live tests and exposes a configured <see cref="ICache"/>.
    /// LocalMemory is in-process; other providers use Testcontainers.
    /// </summary>
    public sealed class LiveProviderHost : IAsyncDisposable
    {
        private readonly IContainer _container;

        private LiveProviderHost(CacheProviderType providerType, ICache cache, IContainer container)
        {
            ProviderType = providerType;
            Cache = cache;
            _container = container;
        }

        public CacheProviderType ProviderType { get; }

        public ICache Cache { get; }

        public static async Task<LiveProviderHost> StartAsync(CacheProviderType providerType)
        {
            if (providerType != CacheProviderType.LocalMemory)
                LiveContainerRuntime.EnsureConfigured();

            try
            {
                return providerType switch
                {
                    CacheProviderType.LocalMemory => StartLocalMemory(),
                    CacheProviderType.Redis => await StartRedisAsync().ConfigureAwait(false),
                    CacheProviderType.Dragonfly => await StartDragonflyAsync().ConfigureAwait(false),
                    CacheProviderType.KeyDB => await StartKeyDbAsync().ConfigureAwait(false),
                    CacheProviderType.Garnet => await StartGarnetAsync().ConfigureAwait(false),
                    CacheProviderType.Memcached => await StartMemcachedAsync().ConfigureAwait(false),
                    CacheProviderType.Hazelcast => await StartHazelcastAsync().ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unsupported provider"),
                };
            }
            catch (Exception ex) when (IsDockerUnavailable(ex))
            {
                throw new InvalidOperationException(
                    $"Cannot start {providerType}: no container engine API available. " +
                    "Install Podman (preferred) or Docker, ensure the engine is running " +
                    $"(Podman socket typically at $XDG_RUNTIME_DIR/podman/podman.sock), then re-run with {LiveProviderSelection.EnvVar} set.",
                    ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Cache is IAsyncDisposable asyncCache)
            {
                await asyncCache.DisposeAsync().ConfigureAwait(false);
            }
            else if (Cache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }

            if (_container != null)
                await _container.DisposeAsync().ConfigureAwait(false);
        }

        private static LiveProviderHost StartLocalMemory()
        {
            string cacheName = UniqueName("local");
            var config = new LocalMemoryProviderConfiguration(cacheName, timeToLiveMillis: 300_000);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.LocalMemory, cache, container: null);
        }

        private static async Task<LiveProviderHost> StartRedisAsync()
        {
            RedisContainer container = new RedisBuilder("redis:7-alpine").Build();
            await container.StartAsync().ConfigureAwait(false);
            string endpoint = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
            var config = new RedisProviderConfiguration(endpoint, password: "", UniqueName("redis"), useSSL: false, abortConnection: false);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.Redis, cache, container);
        }

        private static async Task<LiveProviderHost> StartDragonflyAsync()
        {
            IContainer container = new ContainerBuilder("docker.dragonflydb.io/dragonflydb/dragonfly:v1.25.1")
                .WithPortBinding(6379, true)
                .WithCommand("--logtostderr")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string endpoint = HostPort(container, 6379);
            var config = new DragonflyProviderConfiguration(endpoint, password: "", UniqueName("dragonfly"), useSSL: false);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.Dragonfly, cache, container);
        }

        private static async Task<LiveProviderHost> StartKeyDbAsync()
        {
            IContainer container = new ContainerBuilder("eqalpha/keydb:latest")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string endpoint = HostPort(container, 6379);
            var config = new KeyDBProviderConfiguration(endpoint, password: "", UniqueName("keydb"), useSSL: false);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.KeyDB, cache, container);
        }

        private static async Task<LiveProviderHost> StartGarnetAsync()
        {
            IContainer container = new ContainerBuilder("ghcr.io/microsoft/garnet")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string endpoint = HostPort(container, 6379);
            var config = new GarnetProviderConfiguration(endpoint, password: "", UniqueName("garnet"), useSSL: false);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.Garnet, cache, container);
        }

        private static async Task<LiveProviderHost> StartMemcachedAsync()
        {
            IContainer container = new ContainerBuilder("memcached:1.6-alpine")
                .WithPortBinding(11211, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(11211))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string endpoint = HostPort(container, 11211);
            var config = new MemcachedProviderConfiguration(endpoint, UniqueName("memcached"), timeToLiveMillis: 300_000);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.Memcached, cache, container);
        }

        private static async Task<LiveProviderHost> StartHazelcastAsync()
        {
            IContainer container = new ContainerBuilder("hazelcast/hazelcast:5.5.0")
                .WithPortBinding(5701, true)
                .WithEnvironment("HZ_CLUSTERNAME", "dev")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(5701, s => s.WithTimeout(TimeSpan.FromMinutes(2))))
                .Build();
            await container.StartAsync().ConfigureAwait(false);
            string address = HostPort(container, 5701);
            var config = new HazelcastProviderConfiguration("dev", address, UniqueName("hazelcast"), timeToLiveMillis: 300_000);
            ICache cache = CacheFactory.GetOrCreateCache(new CacheConfiguration(config, CacheType.NonPersistent));
            return new LiveProviderHost(CacheProviderType.Hazelcast, cache, container);
        }

        private static string HostPort(IContainer container, int containerPort) =>
            $"{container.Hostname}:{container.GetMappedPublicPort(containerPort)}";

        private static string UniqueName(string prefix) =>
            $"{prefix}-{Guid.NewGuid():N}";

        private static bool IsDockerUnavailable(Exception ex)
        {
            for (Exception cur = ex; cur != null; cur = cur.InnerException)
            {
                string msg = cur.Message ?? "";
                string type = cur.GetType().FullName ?? "";
                if (type.Contains("DockerUnavailable", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Cannot connect to the Docker", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("docker.sock", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("The Docker daemon", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
