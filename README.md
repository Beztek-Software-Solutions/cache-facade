# Cache Facade

Unified .NET caching facade (`Beztek.Facade.Cache`) over Redis, Dragonfly, KeyDB, Valkey, Garnet, Hazelcast, Memcached, and in-process local memory, with optional write-through / write-behind SQL persistence.

## Projects

| Project | Description |
|---------|-------------|
| [`CacheFacade/`](CacheFacade/) | Library package `Beztek.Facade.Cache` (see [CacheFacade/README.md](CacheFacade/README.md) for full API and write-behind guidance) |
| [`CacheFacade.Tests/`](CacheFacade.Tests/) | NUnit unit tests + optional Testcontainers live suite (`CACHEFACADE_LIVE_PROVIDERS`) |

## Quick start

```bash
dotnet restore cache-facade.sln
dotnet build cache-facade.sln
dotnet test CacheFacade.Tests/Beztek.Facade.Cache.Tests.csproj
```

With coverage (Coverlet; target ≥ 85% line coverage):

```bash
dotnet test CacheFacade.Tests/Beztek.Facade.Cache.Tests.csproj \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput=./coverage/ \
  /p:Include='[Beztek.Facade.Cache]*' \
  /p:Threshold=85 \
  /p:ThresholdType=line
```

### Live container tests

Optional Testcontainers suite under `CacheFacade.Tests/Live/`. Unset env → not discovered (default CI stays container-free).

Uses the **Docker Engine API**. Prefer **Podman** (rootless): the suite auto-detects `$XDG_RUNTIME_DIR/podman/podman.sock` and sets `DOCKER_HOST` — no `docker` CLI or alias needed. Docker Desktop still works if that socket is what your machine exposes.

`localmemory` needs no container engine. Heavier images (Hazelcast, Garnet) have longer cold-start times.

```bash
# One provider
CACHEFACADE_LIVE_PROVIDERS=redis \
  dotnet test CacheFacade.Tests/Beztek.Facade.Cache.Tests.csproj --filter Category=Live

# Several providers
CACHEFACADE_LIVE_PROVIDERS=redis,memcached,localmemory \
  dotnet test CacheFacade.Tests/Beztek.Facade.Cache.Tests.csproj --filter Category=Live

# Every provider (LocalMemory in-process + containers for the rest)
CACHEFACADE_LIVE_PROVIDERS=all \
  dotnet test CacheFacade.Tests/Beztek.Facade.Cache.Tests.csproj --filter Category=Live
```

Aliases: `localmemory`/`local`, `redis`, `dragonfly`/`df`, `keydb`, `valkey`/`vk`, `garnet`, `memcached`/`mc`, `hazelcast`/`hz`, `all`.

## NuGet

Install the package and follow [CacheFacade/README.md](CacheFacade/README.md) for initialization samples, entity contracts (`IEtagEntity` is sufficient unless using write-behind, which needs soft delete via `IWriteBehindEntity`), and write-behind drain rules.

```bash
dotnet add package Beztek.Facade.Cache
```

## Providers

| Provider | Status |
|----------|--------|
| Redis | Implemented (`RedisProviderConfiguration`) |
| Dragonfly | Implemented (`DragonflyProviderConfiguration`; Redis protocol) |
| KeyDB | Implemented (`KeyDBProviderConfiguration`; Redis protocol) |
| Valkey | Implemented (`ValkeyProviderConfiguration`; Redis protocol) |
| Garnet | Implemented (`GarnetProviderConfiguration`; Redis RESP subset, token lock) |
| Local memory | Implemented (`LocalMemoryProviderConfiguration`) |
| Hazelcast | Implemented (`HazelcastProviderConfiguration`) |
| Memcached | Implemented (`MemcachedProviderConfiguration`) |
