# DCache

## Overview

`DCache<T, TId>` is a strongly-typed wrapper around `IDistributedCache` that provides a two-tier caching strategy combining distributed caching with optional in-memory caching. It abstracts away the complexities of serialization, key management, and cache coordination, presenting a simple dictionary-like interface for storing and retrieving typed objects.

## Key Features

The primary goals of `DCache` are:

1. **Type Safety**: Provides a strongly-typed interface over the untyped byte-array-based `IDistributedCache`
2. **Dual-Layer Caching**: Combines fast in-memory caching (L1) with distributed caching (L2) for optimal performance
3. **Automatic Serialization**: Handles JSON serialization/deserialization transparently using `System.Text.Json`
4. **Simplified API**: Reduces boilerplate code for common caching patterns with typed keys and values

## Architecture

### Two-Tier Caching Strategy

```
┌─────────────────────────────────────────────┐
│         DCache<T, TId>                      │
├─────────────────────────────────────────────┤
│  L1: MemCacheDict (Optional In-Memory)      │
│      - CacheDictionary<string, CacheData>   │
│      - Fast, local, time-based expiration   │
│      - Per-instance cache                   │
├─────────────────────────────────────────────┤
│  L2: IDistributedCache (Required)           │
│      - Redis, SQL Server, etc.              │
│      - Shared across instances              │
│      - Persistent, distributed              │
└─────────────────────────────────────────────┘
```

### Key Components

**Generic Parameters:**
- `T` - The type of objects being cached
- `TId` - The type of the cache key identifier

**Core Properties:**
- `CacheKeyPrefix` - Prefix applied to all cache keys for namespacing
- `CacheSetOptions` - Default options for setting cache entries
- `LastCacheSrc` - Tracks whether the last retrieval came from memory, distributed cache, or neither
- `MemCacheDict` - Optional in-memory `CacheDictionary` instance for L1 caching

## API Surface

### Initialization

Three ways to initialize:

1. **Default constructor + INIT method:**
```csharp
DCache<Customer, int> cache = new();
cache.INIT(distributedCache, "customer:", options, TimeSpan.FromMinutes(5));
```

2. **Constructor with TimeSpan:**
```csharp
DCache<Customer, int> cache = new(
    distributedCache, 
    "customer:", 
    expiresAfter: TimeSpan.FromHours(1),
    inMemoryCacheExpirationTime: TimeSpan.FromMinutes(5)
);
```

3. **Constructor with DistributedCacheEntryOptions:**
```csharp
DCache<Customer, int> cache = new(
    distributedCache,
    "customer:",
    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
    inMemoryCacheExpirationTime: TimeSpan.FromMinutes(5)
);
```

### Core Operations

**GetAsync** - Retrieves an item from cache
- Checks L1 (memory) first, then L2 (distributed)
- Updates L1 on L2 hits for subsequent fast access
- Tracks cache source via `LastCacheSrc`
- Optional `skipMemCache` parameter to bypass L1

**SetAsync** - Stores an item in cache
- Serializes object to JSON, then to UTF-8 bytes
- Writes to distributed cache
- Updates in-memory cache if enabled
- Accepts optional custom `DistributedCacheEntryOptions`

**RemoveAsync** - Removes an item from distributed cache

**RefreshAsync** - Refreshes sliding expiration on distributed cache entry

### Helper Methods

- `GetCacheKey(TId id)` - Constructs full cache key from prefix and id
- `ExistsInMemoryCache(TId id)` - Checks if item exists in L1 cache
- `MemoryCacheIsEnabled` - Property indicating if in-memory caching is active

### Serialization

**Virtual methods for customization:**
- `Deserialize(byte[] json)` - Converts bytes to object
- `Serialize(T obj)` - Converts object to JSON string

**Default JSON options:**
- No property naming policy (preserves original names)
- Non-indented (compact) output
- Allows trailing commas
- Ignores null values when writing
- Case-insensitive property names

Custom options can be set via `JsonSerializeOptions` property.

## Design Considerations & Limitations

### 1. Single Type Per Instance
- Each `DCache` instance is dedicated to one type `T`
- Enforces consistent serialization and caching strategy per type
- Multiple instances needed for different types
- Advantage: Type safety and clarity

### 2. JSON Serialization Only

Up front I'd say: It'd be nice to have a binary option, and it pry stinks to tightly couple this type to a single serialzer. Maybe can be updated in future.

- Uses `System.Text.Json.JsonSerializer` exclusively
- Simpler than binary serialization but potentially larger payload
- Customizable via `JsonSerializerOptions`
- May not work for types with complex serialization requirements

### 3. Key Type ToString Requirement
- `TId` must convert meaningfully to string via `ToString()`
- Works well for: `int`, `Guid`, `string`, simple structs
- May cause collisions for complex types without custom `ToString()`

### 4. Strong Coupling to Microsoft.Extensions.Caching
- Tightly integrated with `IDistributedCache` infrastructure
- Requires `Microsoft.Extensions.Caching.Distributed` package
- Uses `DistributedCacheEntryOptions` directly
- Not easily portable to other caching abstractions

### 5. In-Memory Cache Characteristics
- **Per-instance**: Not shared across application instances/servers
- **Time-based expiration**: Set once during initialization
- **No invalidation sync**: Changes on one server won't invalidate others' memory cache
- **Use case**: Low-churn, read-heavy scenarios where slight staleness is acceptable

## Cache Source Tracking

The `DCacheGetSrc` enum and `LastCacheSrc` property enable observability:

```csharp
public enum DCacheGetSrc { 
    None = 0,              // Cache miss
    MemoryCache = 1,       // L1 hit
    DistributedCache = 2   // L2 hit
}
```

This allows monitoring cache effectiveness and debugging cache behavior.

## Implementation Patterns

### Typical Usage Pattern

```csharp
// Initialization (typically in DI configuration)
services.AddSingleton<IDCache<Product, int>>(sp => 
    new DCache<Product, int>(
        sp.GetRequiredService<IDistributedCache>(),
        cacheKeyPrefix: "product:",
        TimeSpan.FromHours(4),
        inMemoryCacheExpirationTime: TimeSpan.FromMinutes(10)
    ));

// Usage in application code
var product = await _cache.GetAsync(productId);
if (product == null) {
    product = await _repository.GetProductAsync(productId);
    await _cache.SetAsync(productId, product);
}
```

### Cache-Aside Pattern Support

`DCache` is designed to support the cache-aside (lazy loading) pattern:
1. Check cache first (`GetAsync`)
2. On miss, load from data source
3. Write to cache (`SetAsync`)
4. Return to caller

## Performance Characteristics

### Memory Cache Benefits
- **Sub-millisecond latency** for L1 hits
- Eliminates network round-trip for repeated reads
- Configurable expiration via `inMemoryCacheExpirationTime`
- Automatic cleanup via `RunPurgeTS` (10-minute intervals)

### Trade-offs
- **Memory usage**: Each instance consumes memory proportional to cached items
- **Consistency**: Potential staleness between instances
- **Expiration overhead**: Periodic purge operations

## Potential Improvements & Considerations

1. **Remove operation incomplete**: `RemoveAsync` doesn't clear in-memory cache
2. **No cache invalidation notifications**: Changes don't propagate to other instances
3. **Key collision risk**: `TId.ToString()` may not guarantee uniqueness
4. **No size limits**: In-memory cache can grow unbounded between purges
5. **No metrics**: No built-in cache hit/miss tracking beyond `LastCacheSrc`

## Interface Design

The `IDCache<T, TId>` interface enables:
- Dependency injection
- Testing with mocks
- Multiple implementations (e.g., memory-only, distributed-only)
- Abstraction from concrete implementation

## Summary

`DCache` excels at scenarios requiring:
- Typed, simple caching operations
- Performance optimization through memory caching
- Reduced boilerplate for distributed cache interactions
- Read-heavy workloads with acceptable eventual consistency

It's best suited for:
- Configuration data
- Reference data (categories, countries, etc.)
- User profiles/sessions
- Product catalogs
- Any read-heavy, slowly-changing data

Consider alternatives when you need:
- Strong consistency across instances
- Complex eviction policies
- Binary serialization
- Multi-key operations or transactions
- Fine-grained cache control
