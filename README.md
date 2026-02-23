# DotNetXtensions.DCache

[![NuGet](https://img.shields.io/nuget/v/DotNetXtensions.DCache.svg)](https://www.nuget.org/packages/DotNetXtensions.DCache)

High-performance caching types for .NET 8+

- **[CacheDictionary](./README-CacheDictionary.md)**: Thread-safe in-memory cache dictionary with automatic time-based expiration
- **[DCache](./README-DCache.md)**: Allows an IDistributedCache to look and act like a simple in-memory typed dictionary, with dual-layer caching

## Installation

```bash
dotnet add package DotNetXtensions.DCache
```

## CacheDictionary

**[📖 Full Documentation](./README-CacheDictionary.md)**

```csharp
// Items auto-expire after 5 minutes
CacheDictionary<string, User> cache = new(TimeSpan.FromMinutes(5));
cache["user123"] = new User { Name = "Alice" };

if (cache.TryGetValue("user123", out User user))
    WriteLine(user.Name);
```

**Features**

- **Time-Based Expiration**: Items automatically expire after a set duration
- **Thread-Safe**: Built on `ConcurrentDictionary` for safe concurrent access
- **High Performance**: Minimal overhead on normal Get/Set operations
- **Passive Purging**: Expired items are removed intelligently without requiring external timers
- **Guaranteed Fresh Data**: Expired items are never returned, even if still present internally
- **Automatic Disposal**: Optional automatic disposal of `IDisposable` cached values

## DCache

**[📖 Full Documentation](./README-DCache.md)**

```csharp
// Combines L1 (memory) + L2 (distributed) caching
DCache<User, string> cache = new(distributedCache);
await cache.SetAsync("user123", user);

User retrieved = await cache.GetAsync("user123");
```

**Features**

1. **Simplicity**: With little effort or boilerplate, an `IDistributedCache` looks and acts like a simple in-memory typed dictionary
2. **Dual-Layer Caching**: Combines fast (optional) in-memory caching (L1) with distributed caching (L2) for optimal performance
3. **Automatic Serialization**: Handles JSON serialization/deserialization transparently using `System.Text.Json`
4. **Simplified API**: Reduces boilerplate code for common caching patterns with typed keys and values


## When to Use Which?

| Use CacheDictionary when... | Use DCache when... |
|------------------------------|---------------------|
| Single-server scenarios | Multi-server/distributed systems |
| You need time-based expiration | You're already using IDistributedCache |
| Synchronous access is preferred | You need Redis, SQL Server, or other distributed caches |
| Memory-only caching is sufficient | You want L1 + L2 caching optimization |
