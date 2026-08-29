# Cache Facade library

## Introduction
Caching facade library will serve as unified library for caching.

## Details

As part of implementation of micro-services, this library enables the services layer to use the cache as source of truth for data, without directly needingto go to the underlying persistence data layer. This library simplifies the cache implementation to the basic operations which can happen on a cache. Applications just need to read and write objects to the cache, and when configured for write-through or write-behind, the object gets saved in the back-end database. The cache exposes a search API using the Beztek.Facade.Sql library, that enables a powerful combination of using the cache with SQL queries.

Having a facade over of the caching operations help us to switch the cache providers without having need to change services code which use the cache. The facade can use the cache in one or more of the following ways:
  - Non-Persistent
  - Write Through - already built with back-end SQL support using the Beztek.Facade.Sql libary
  - Write Behind - needs **Beztek.Facade.Queue ≥ 1.0.10** (LocalMemory, Azure Queue Storage, or AWS SQS). See [Write-behind cache](#write-behind-cache) below.

The back-end can be a distributed cache (Redis is the first implementation here), or a non-distributed cache. This library comes with a facade to a local memory cache for cases where a distributed cacheis not needed.

A powerful way to use this library in development is to use local memory cache, along with a local memory queue, and a local memory/SQLite file SQL database for quick-and-dirty prototyping in a standalone setup.

By using a distributed cache back-end such as Redis, this library can be invoked within a clustered micro-service which all share the same cache. - You get the performance of a cache, with the flexibility of a SQL database.
- You get the simplicity of a NoSQL database with the powerful query capability of SQL.

## Write-behind cache

Write-behind is modeled on the same last-write-wins outbox pattern used in production OpenSearch CDC sync: every change enqueues **intent + value snapshot + order stamp**, and the drain applies without re-reading the live cache. That keeps persistence correct if the key is flushed or evicted before the queue is drained.

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

### Entity contract for robust write-behind

For cross-batch shuffle and redelivery safety (especially create vs delete), entities should implement **`IWriteBehindEntity`**:

```csharp
public interface IWriteBehindEntity : IEtagEntity
{
    /// Soft-delete tombstone (OpenSearch _deleted analogue).
    bool IsDeleted { get; set; }
}
```

**Two persisted concerns, one reused column:**

| Concern | Storage | Notes |
|---------|---------|--------|
| Order / version clock | Existing **`etag`** column | **Always** a short sequential string via `EtagUtil.GenerateEtag()` (UTC epoch ms, ~13 characters)—same for write-through and write-behind. Used for API optimistic concurrency **and** (on write-behind) drain last-write-wins (`EtagUtil.ParseSequentialEtag`). |
| Soft-delete tombstone | **`is_deleted`** (bool) column | Delete keeps the row, sets `IsDeleted = true`, and advances `Etag`. A newer create/update clears `IsDeleted` (undelete). Write-behind only. |

There is **no** separate `write_behind_sequence` column: the sequential etag *is* the version. Using the same etag format in both cache modes makes switching write-through ↔ write-behind less painful.

`GetByIdAsync` (and anything that hydrates the cache) must treat `IsDeleted == true` as **missing** (`null`) so callers never see tombstones.

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

1. **Queue:** `Beztek.Facade.Queue` ≥ 1.0.10; register processor for `WriteBehindMessage` (not `string`). Configure `QueueConfiguration.MaxProcessingAttempts` (default 5). On max failures or `false`, messages land on the poison queue — use `PeekUnprocessedMessagesAsync` / `RequeueUnprocessedMessagesAsync` to inspect or retry.
2. **SQL:** implement `ISqlGenerator.GetSqlUpsert` (dialect-specific insert-or-update). Write-behind create/update drain through upsert; write-through may keep distinct insert/update.
3. **Schema (write-behind entities):** keep `etag` (store sequential strings); add `is_deleted` (bool). Filter deleted rows in reads.
4. **Entity:** implement `IWriteBehindEntity` (`Etag` + `IsDeleted`).
5. **Etag generation:** always use `EtagUtil.GenerateEtag()` (sequential epoch-ms string)—not `Guid.NewGuid()`.

### Write-through

Write-through remains synchronous create/update/delete with strict insert vs update and hard delete. It uses the **same sequential etag** as write-behind so switching modes does not change etag format. Soft delete (`IsDeleted`) is only required for write-behind cross-batch create/delete safety.

## Entity types: recommendations, fallbacks, and compromises

The library supports more than one entity shape. **Etag format is shared**; soft delete is the main write-behind-specific requirement.

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


In the initial version, caching library has implementation for following operations -

```csharp
// Returns the value for the key, and null if it is not in the cache.
Task<T> GetAsync<T>(string key);

// If the cache does not have the key, put the value for the key and return null, otherwise just return the old value and do not overwrite.
Task<T> GetAndPutIfAbsentAsync<T>(string key, T value);

// Replaces the entry for a key only if currently mapped to some value. Does nothing and returns null if it does not exist, and returns the old value if it exists.
Task<T> GetAndReplaceAsync<T>(string key, T value);

// If the cache has the key, replace the value for the key and return the old value, otherwise put the value corresponding to the key and return null.
Task<T> GetAndPutAsync<T>(string key, T value);

// Removes the value and returns it if it exists, and null if it doesn't.
Task<T> RemoveAsync<T>(string key);
```

Caching library has following cache providers implemented for the initial version -
1. Redis
2. Local Memory

THe Caching library is a facade to a back-end CacheProvider which can be used to initialize cache. Depending on which cache provider the application/service needs to use, the respective cache configuration object needs to be passed to the CacheProvider.

## Initializing cache

Instantiate a CacheProvider using the appropriate provider's configuration.
