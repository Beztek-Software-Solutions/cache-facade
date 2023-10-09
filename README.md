# Cache Facade library

## Introduction
Caching facade library will serve as unified library for caching.

## Details

As part of implementation of micro-services, this library enables the services layer to use the cache as source of truth for data, without directly needingto go to the underlying persistence data layer. This library simplifies the cache implementation to the basic operations which can happen on a cache. Applications just need to read and write objects to the cache, and when configured for write-through or write-behind, the object gets saved in the back-end database. The cache exposes a search API using the Beztek.Facade.Sql library, that enables a powerful combination of using the cache with SQL queries.

Having a facade over of the caching operations help us to switch the cache providers without having need to change services code which use the cache. The facade can use the cache in one or more of the following ways:
  - Non-Persistent
  - Write Through - already built with back-end SQL support using the Beztek.Facade.Sql libary
  - Write Behind - needs the Beztek.Facade.Queue library to handle the asynchronous persistence

The back-end can be a distributed cache (Redis is the first implementation here), or a non-distributed cache. This library comes with a facade to a local memory cache for cases where a distributed cacheis not needed.

A powerful way to use this library in development is to use local memory cache, along with a local memory queue, and a local memory/SQLite file SQL database for quick-and-dirty prototyping in a standalone setup.

By using a distributed cache back-end such as Redis, this library can be invoked within a clustered micro-service which all share the same cache. - You get the performance of a cache, with the flexibility of a SQL database.
- You get the simplicity of a NoSQL database with the powerful query capability of SQL.

## API Summary

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

