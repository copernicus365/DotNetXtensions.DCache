# DotNetXtensions.DCache

[![NuGet](https://img.shields.io/nuget/v/DotNetXtensions.DCache.svg)](https://www.nuget.org/packages/DotNetXtensions.DCache)

Provides cached dictionary types:

- [`CacheDictionary`]("./README-CacheDictionary.md"): an in-memory cached dictionary
- [`DCache`]("./README-DCache.md"): an implementation of `IDistributedCache` that abstracts the distributed-cache logic, functioning like a simple in-memory dictionary

## CacheDictionary

`CacheDictionary<TKey, TValue>` is a dictionary whose items automatically expire after a configurable amount of time, and that internally wraps a `ConcurrentDictionary`, making it thread-safe and suitable for concurrent scenarios where caching is needed.

For more details, see the [CacheDictionary README](./README-CacheDictionary.md).

### Key Features

- **Time-Based Expiration**: Items automatically expire after a set duration
- **Thread-Safe**: Built on `ConcurrentDictionary` for safe concurrent access
- **High Performance**: Minimal overhead on normal Get/Set operations
- **Passive Purging**: Expired items are removed intelligently without requiring external timers
- **Guaranteed Fresh Data**: Expired items are never returned, even if still present internally

For more details, see the [DCache README](./README-DCache.md).

## DCache

`DCache<T, TId>` is a strongly-typed wrapper around `IDistributedCache` that provides a two-tier caching strategy combining distributed caching with optional in-memory caching. It abstracts away the complexities of serialization, key management, and cache coordination, presenting a simple dictionary-like interface for storing and retrieving typed objects.

### Key Features

The primary goals of `DCache` are:

1. **Type Safety**: Provides a strongly-typed interface over the untyped byte-array-based `IDistributedCache`
2. **Dual-Layer Caching**: Combines fast in-memory caching (L1) with distributed caching (L2) for optimal performance
3. **Automatic Serialization**: Handles JSON serialization/deserialization transparently using `System.Text.Json`
4. **Simplified API**: Reduces boilerplate code for common caching patterns with typed keys and values

