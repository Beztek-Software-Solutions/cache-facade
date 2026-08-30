# Cache Facade

Unified .NET caching facade (`Beztek.Facade.Cache`) over Redis and in-process local memory, with optional write-through / write-behind SQL persistence.

## Projects

| Project | Description |
|---------|-------------|
| [`CacheFacade/`](CacheFacade/) | Library package `Beztek.Facade.Cache` (see [CacheFacade/README.md](CacheFacade/README.md) for full API and write-behind guidance) |
| [`CacheFacade.Tests/`](CacheFacade.Tests/) | NUnit unit tests |

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

## NuGet

Install the package and follow [CacheFacade/README.md](CacheFacade/README.md) for initialization samples, entity contracts (`IEtagEntity` is sufficient unless using write-behind, which needs soft delete via `IWriteBehindEntity`), and write-behind drain rules.

```bash
dotnet add package Beztek.Facade.Cache
```

## Providers

| Provider | Status |
|----------|--------|
| Redis | Implemented (`RedisProviderConfiguration`) |
| Local memory | Implemented (`LocalMemoryProviderConfiguration`) |
| Hazelcast | Enum placeholder only — not implemented |
