# Cache Facade library

## Introduction

`Beztek.Facade.Cache` is a unified caching facade for .NET. Services read and write objects through a single `ICache` API; the library can keep those objects in Redis or in-process memory, and optionally persist them with write-through or write-behind SQL.

## Details

As part of micro-services, this library lets the services layer treat the cache as the source of truth without calling the persistence layer on every request. Applications use basic cache operations; when configured for write-through or write-behind, values are saved in the back-end database. Search uses the Beztek.Facade.Sql library so cache + SQL queries work together.

A facade over caching operations lets you switch providers without changing service code. Modes:

- **Non-Persistent** — cache only
- **Write Through** — synchronous SQL via Beztek.Facade.Sql
- **Write Behind** — needs **Beztek.Facade.Queue ≥ 1.0.10** (LocalMemory, Azure Queue Storage, or AWS SQS). See [Write-behind cache](#write-behind-cache) below.

The back-end can be a distributed cache (Redis) or a non-distributed local memory cache. For local prototyping, combine local memory cache, a local memory queue, and SQLite.

With Redis in a clustered micro-service you get cache performance with SQL query flexibility, and NoSQL-style key access with SQL search when needed.

## Core API (`ICache`)

| Method | Behavior |
|--------|----------|
| `GetAsync<T>` | Get by key; write-through/behind miss loads from persistence and fills the provider |
| `PeekAsync<T>` | Provider-only get (no persistence load) |
| `WarmAsync<T>` | Provider-only put (no DB write); use after batch SQL load |
| `GetAndPutIfAbsentAsync<T>` | Insert if absent; otherwise return existing |
| `GetAndReplaceAsync<T>` | Replace if present; etag check for `IEtagEntity` |
| `GetAndPutAsync<T>` | Upsert into cache (+ persistence per mode) |
| `RemoveAsync<T>` | Remove from cache (+ delete / enqueue per mode) |
| `SearchByQueryAsync<T>` | Paged SQL id query + hydrate via `GetAsync` |
| `FlushKeyAsync` / `FlushAsync` | Evict from provider only (write-behind snapshots still drain) |
| `AcquireLock` | Named disposable lock (Redis RedLock or local reentrant lock) |

Obtain instances only via `CacheFactory.GetOrCreateCache` / `GetCache`.

## Initializing cache

### Non-persistent (local memory)

```csharp
var providerConfig = new LocalMemoryProviderConfiguration("orders", timeToLiveMillis: 300_000);
var cacheConfig = new CacheConfiguration(providerConfig, CacheType.NonPersistent);
ICache cache = CacheFactory.GetOrCreateCache(cacheConfig, logger);
```

### Write-through (local memory + SQL)

```csharp
ISqlFacade sqlFacade = SqlFacadeFactory.GetSqlFacade(/* your SqlFacadeConfig */);
IPersistenceService persistence = new SqlPersistenceService<MyEntity>(sqlFacade, new MySqlGenerator());

var providerConfig = new LocalMemoryProviderConfiguration("orders", 300_000);
var cacheConfig = new CacheConfiguration(providerConfig, CacheType.WriteThrough, persistence);
ICache cache = CacheFactory.GetOrCreateCache(cacheConfig, logger);
```

### Write-behind (Redis + queue + SQL)

```csharp
var redisConfig = new RedisProviderConfiguration(
    endpoint: "mycache.redis.cache.windows.net:6380",
    password: redisPassword,
    cacheName: "orders",
    useSSL: true);

IPersistenceService persistence = new SqlPersistenceService<MyEntity>(sqlFacade, new MySqlGenerator());
IQueueClient queueClient = QueueClientFactory.GetQueueClient(queueProviderConfig, logger);
var processor = new CacheWriteBehindProcessor<MyEntity>("orders");
var queueConfig = new QueueConfiguration(queueClient, processor, cancellationToken);

var cacheConfig = new CacheConfiguration(redisConfig, CacheType.WriteBehind, persistence, queueConfig);
ICache cache = CacheFactory.GetOrCreateCache(cacheConfig, logger);
```

Look up an existing instance: `CacheFactory.GetCache("orders")`.

### Providers

| Provider | Configuration type | Status |
|----------|-------------------|--------|
| Redis | `RedisProviderConfiguration` | Implemented |
| Local memory | `LocalMemoryProviderConfiguration` | Implemented |
| Hazelcast | — | Enum placeholder only |

## Write-behind cache

Write-behind is modeled in this way: every change enqueues **intent + value snapshot + order stamp**, and the drain applies without re-reading the live cache. That keeps persistence correct if the key is flushed or evicted before the queue is drained.

### Flow

```
Write API (put / replace / remove)
    → update ICacheProvider
    → Enqueue WriteBehindMessage { Id, WriteType, Value, Sequence }
         │
         ▼
Queue (LocalMemory / Azure / SQS)
         │
         ▼
CacheWriteBehindProcessor
    → keep highest Sequence per Id (in-batch compaction)
    → Create/Update → Upsert snapshot
    → Delete → soft-delete upsert (IWriteBehindEntity) or hard delete
    → ISqlGenerator.GetSqlUpsert / version-gated apply
```

### Queue payload (`WriteBehindMessage`)

| Field | Purpose |
|-------|---------|
| `Id` | Cache / entity key |
| `WriteType` | `Create`, `Update`, or `Delete` captured at enqueue time |
| `Value` | Entity snapshot at enqueue (`null` only when no snapshot is available on delete) |
| `Sequence` | UTC epoch milliseconds; aligned with the entity's sequential `Etag` when present |

The legacy key-only message is obsolete: inferring create/update/delete from live cache+DB fails after `FlushKeyAsync` / `FlushAsync`.

### Entity contract

**`IEtagEntity` is sufficient** for non-persistent and write-through caches (sequential `Etag` only).

**Write-behind** additionally needs soft delete: implement **`IWriteBehindEntity`** (extends `IEtagEntity` with `IsDeleted`) so cross-batch create/delete races stay safe under redelivery.

```csharp
public interface IWriteBehindEntity : IEtagEntity
{
    /// Soft-delete tombstone (OpenSearch _deleted analogue).
    bool IsDeleted { get; set; }
}
```

**Schema:**

| Mode | Columns |
|------|---------|
| Non-persistent / write-through (`IEtagEntity`) | **`etag`** only (sequential string). Hard delete removes the row; no soft-delete column. |
| Write-behind (`IWriteBehindEntity`) | **`etag`** plus **`is_deleted`** (bool). Soft-delete tombstone keeps the version clock after delete. |

| Concern | Storage | When needed | Notes |
|---------|---------|-------------|--------|
| Order / version clock | **`etag`** | All modes that use optimistic concurrency or write-behind drain ordering | Short sequential string via `EtagUtil.GenerateEtag()` (UTC epoch ms, ~13 characters). Same format for write-through and write-behind. Used for API concurrency **and** (on write-behind) last-write-wins (`EtagUtil.ParseSequentialEtag`). |
| Soft-delete tombstone | **`is_deleted`** (bool) | **Write-behind only** | Delete keeps the row, sets `IsDeleted = true`, and advances `Etag`. A newer create/update clears `IsDeleted` (undelete). Not required for write-through. |

There is **no** separate `write_behind_sequence` column: the sequential etag *is* the version. Using the same etag format in both cache modes makes switching write-through ↔ write-behind less painful.

On write-behind, `GetByIdAsync` (and anything that hydrates the cache) must treat `IsDeleted == true` as **missing** (`null`) so callers never see tombstones.

### Why soft delete

Hard delete drops the version clock. Then:

1. Delete@200 is applied (row gone).
2. Stale Create@100 arrives later → insert succeeds → **row resurrected**.

Soft delete retains the row with sequential etag + `IsDeleted`, so the stale create is rejected (`incoming etag/sequence ≤ persisted`). This matches OpenSearch soft delete + `_sync_version`.

In-batch message shuffle is already safe (max `Sequence` wins before apply). Soft delete closes the **cross-batch** create/delete race.

### Drain rules (write-behind entities)

| Winning message | Apply |
|-----------------|--------|
| Create / Update | Upsert snapshot; set sequential `Etag` from `Sequence`; `IsDeleted = false` |
| Delete | Upsert tombstone; set sequential `Etag` from `Sequence`; `IsDeleted = true` (do not `SQL DELETE`) |

Upsert SQL should be version-gated, e.g. only apply when the incoming sequential etag is strictly newer than the persisted etag (same idea as OpenSearch `_sync_version`).

### Consumer checklist

Assumes write-through is already in place (`IEtagEntity`, sequential `etag`, insert/update/delete SQL). Below is **only the write-behind delta**:

1. **SQL:** implement `ISqlGenerator.GetSqlUpsert` (dialect-specific, preferably version-gated on sequential etag). Write-behind create/update drain through upsert.
2. **Schema:** add **`is_deleted`** (bool); filter deleted rows in reads.
3. **Entity:** implement **`IWriteBehindEntity`** (`IsDeleted` for soft delete).
4. **Queue:** `Beztek.Facade.Queue` ≥ 1.0.10; register processor for `WriteBehindMessage` (not `string`). Configure `QueueConfiguration.MaxProcessingAttempts` (default 5). On max failures or `false`, messages land on the poison queue — use `PeekUnprocessedMessagesAsync` / `RequeueUnprocessedMessagesAsync` to inspect or retry.

### Write-through

Write-through remains synchronous create/update/delete with strict insert vs update and hard delete. It uses the **same sequential etag** as write-behind so switching modes does not change etag format. Soft delete (`IsDeleted`) is only required for write-behind cross-batch create/delete safety.

## Entity types: recommendations, fallbacks, and compromises

The library supports more than one entity shape. **`IEtagEntity` is sufficient unless you use write-behind**, which then needs soft delete via **`IWriteBehindEntity`**. Etag format is shared across modes.

### Sequential etag (all modes)

`EtagUtil.GenerateEtag()` always returns a short sequential string (UTC epoch ms). Use it for write-through **and** write-behind so:

- Optimistic concurrency tokens look the same after a mode switch.
- Write-behind upsert can version-gate on `ParseSequentialEtag` without a separate column.
- Callers and tests do not branch on cache type for etag generation.

Legacy GUID etags still compare for equality on replace, but `ParseSequentialEtag` returns `0` for them—migrate generators to `EtagUtil.GenerateEtag()`.

### Recommended

| Cache mode | Recommended entity | Etag | Extra persisted field | Why |
|------------|-------------------|------|----------------------|-----|
| **Write-through** | `IEtagEntity` | Sequential (`EtagUtil.GenerateEtag()`) | None | Synchronous writes; hard delete is fine. Same etag format as write-behind for easier migration. |
| **Write-behind** | `IWriteBehindEntity` | Same sequential etag | `is_deleted` (bool) | Soft-delete tombstone keeps the version clock after delete; avoids stale-create resurrection. |

A shared DTO that implements `IWriteBehindEntity` can serve both modes: write-through ignores soft-delete semantics; write-behind uses them.

### What still works without the recommendation (fallbacks)

| Setup | Fallback behavior |
|-------|-------------------|
| Write-behind + **`IEtagEntity` only** (not `IWriteBehindEntity`) | Snapshot + upsert still persist create/update after flush. **Deletes use hard `SQL DELETE`.** In-batch shuffle is safe. **No cross-batch protection** for create vs delete races. |
| Write-behind + **legacy GUID etags** (not library-generated) | Soft delete may run, but **version-gated upsert cannot order GUIDs**. Stale redelivery may overwrite newer state. |
| Write-behind + **no `IEtagEntity`** | No optimistic concurrency on replace. Enqueue/drain still works if `GetSqlUpsert` is implemented. |
| Write-through + **`IWriteBehindEntity`** | Works; hard delete on remove. Sequential etags already match write-behind. Soft-delete column unused until you switch modes. |
| Write-behind + **`IWriteBehindEntity` without `is_deleted` in schema / SQL** | Tombstones not stored; cross-batch delete/create safety is lost. |

### Compromises of sub-optimal approaches

**Write-behind without `IWriteBehindEntity` (hard delete only)**

- **Gain:** No `is_deleted` column; can share a plain `IEtagEntity` with write-through.
- **Lose:** Stale create after delete can **resurrect** the row.
- **OK when:** Low delete volume, single writer per key, or you accept resurrection under redelivery.

**Legacy GUID etags instead of `EtagUtil.GenerateEtag()`**

- **Gain:** None for new code; hurts mode switching.
- **Lose:** Cross-batch last-write-wins broken for write-behind; etag shape differs from library-generated values.
- **OK when:** Temporary migration only—replace generators ASAP.

**Write-through with `IWriteBehindEntity`**

- **Gain:** One DTO for both modes; etags already sequential.
- **Lose:** Soft-delete unused on write-through; hard delete still removes the row.
- **OK when:** You expect to move the cache to write-behind later.

**Write-behind without sequential etag + without soft delete**

- **Lose:** Async complexity **and** resurrection / stale-write risk.
- **Avoid** unless ordering guarantees are deliberately relaxed.

### Decision guide

```
Etag: always EtagUtil.GenerateEtag() (sequential) — both modes

Need write-behind?
  No  → IEtagEntity (write-through)
  Yes → IWriteBehindEntity + is_deleted
        └─ Cannot add is_deleted? → accept resurrection risk OR side tombstone table
```

### Summary

- **Etag:** sequential for all modes (`EtagUtil.GenerateEtag()`).
- **Recommended:** `IEtagEntity` for write-through; `IWriteBehindEntity` (+ `is_deleted`) for write-behind.
- **Fallbacks** allow incremental migration; skipping soft delete trades cross-batch create/delete correctness for schema simplicity.

## Related helpers

| Type | Role |
|------|------|
| `EtagUtil` | Sequential etag generation / parse |
| `EtagEntityUpdateHelper` | Retrying optimistic updates on `ConcurrencyException` |
| `SqlPersistenceService<T>` | Default SQL `IPersistenceService` |
| `ISqlGenerator<T>` | Dialect-specific insert/update/delete/upsert SQL |
| `CacheWriteBehindProcessor<T>` | Queue drain for `WriteBehindMessage` |

XML documentation is included in the NuGet package (`GenerateDocumentationFile`).
