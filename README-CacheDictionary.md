# CacheDictionary

`CacheDictionary<TKey, TValue>` is a dictionary whose items automatically expire after a configurable amount of time, and that internally wraps a `ConcurrentDictionary`, making it thread-safe and suitable for concurrent scenarios where caching is needed.

## Key Features

- **Time-Based Expiration**: Items automatically expire after a set duration
- **Thread-Safe**: Built on `ConcurrentDictionary` for safe concurrent access
- **High Performance**: Minimal overhead on normal Get/Set operations
- **Passive Purging**: Expired items are removed intelligently without requiring external timers
- **Guaranteed Fresh Data**: Expired items are never returned, even if still present internally

## Limitations

A passive purging strategy is core to this implementation. You must decide whether that is OK with the data you are caching. However, you still can use this if you are willing to implement your own timer to call `PurgeExpiredItems()` at your desired interval.

## Installation

```bash
dotnet add package DotNetXtensions.DCache
```

## Quick Start

```csharp
using DotNetXtensions.DCache;

// Create a cache where items expire after 5 minutes
CacheDictionary<string, User> cache = new(TimeSpan.FromMinutes(5));

// Add items
cache["user1"] = new User { Name = "Bob" };
cache.Add("user2", new User { Name = "Alice" });

// Retrieve items
if (cache.TryGetValue("user1", out User user))
  WriteLine($"Found: {user.Name}");

// Items automatically expire after 5 minutes
Thread.Sleep(TimeSpan.FromMinutes(6));
bool found = cache.TryGetValue("user1", out _); // Returns false
```

## How Expiration Works

### Passive Purging Strategy

Rather than using an embedded timer that actively removes expired items, `CacheDictionary` uses a smart passive approach:

1. **Immediate Removal on Access**: When you try to retrieve an expired item, it's immediately removed and `TryGetValue` returns false.

2. **Periodic Batch Purging**: A `RunPurgeTS` property (default: 1 minute) controls how often a full purge is triggered. Purging is checked at these trigger points:
   - When looking up an item (`TryGetValue`, indexer access)
   - When setting or adding an item
   - When enumerating items (including LINQ operations like `ToArray()`)

3. **High Performance**: The trigger check is extremely fast — just a `DateTime.UtcNow` call and a comparison. No expensive operations occur on every Get/Set.

### Why Passive Purging?

This design follows Separation of Concerns and KISS principles:
- No dependencies on timer types!!
- No risk of timer failures causing memory buildup!!
- Simpler, more predictable behavior
- Better testability

If you need guaranteed periodic purging without any interaction, you can easily implement an external timer:

```csharp
CacheDictionary<string, Data> cache = new(TimeSpan.FromMinutes(10));

// Set up periodic purging every 2 minutes
Timer timer = new(_ => cache.PurgeExpiredItems(), 
    null, 
    TimeSpan.FromMinutes(2), 
    TimeSpan.FromMinutes(2));
```

## Configuration

### Expiration Time

Set when creating the cache (minimum 1 second):

```csharp
CacheDictionary<string, string> cache = new(TimeSpan.FromHours(1));
```

### Purge Interval

Control how often automatic purging occurs:

```csharp
CacheDictionary<string, string> cache = new(TimeSpan.FromMinutes(10));

// Check for expired items every 30 seconds
cache.RunPurgeTS = TimeSpan.FromSeconds(30);

// Or more frequently for high-churn scenarios
cache.RunPurgeTS = TimeSpan.FromSeconds(5);

// Or less frequently if memory isn't a concern
cache.RunPurgeTS = TimeSpan.FromMinutes(10);
```

**Performance Consideration**: Setting `RunPurgeTS` too low may cause frequent purges (expensive with large dictionaries), while setting it too high may leave expired items in memory longer, slowing down iterations.

### Custom Equality Comparer

```csharp
CacheDictionary<string, int> cache = new(
    TimeSpan.FromMinutes(5),
    StringComparer.OrdinalIgnoreCase
);

cache["KEY"] = 100;
WriteLine(cache["key"]); // Returns 100 (case-insensitive)
```

## API Reference

### Properties

- **`ExpiresAfter`**: Gets the expiration timespan for items
- **`Count`**: Returns the internal dictionary count (may include expired items)
- **`CountPurged()`**: Returns count after winnowing expired items (requires full scan)
- **`Keys`**: Gets all non-expired keys
- **`Values`**: Gets all non-expired values
- **`RunPurgeTS`**: Gets/sets the interval for automatic purge checks

### Methods

- **`Add(TKey key, TValue value)`**: Adds or updates an item (doesn't throw if key exists)
- **`TryGetValue(TKey key, out TValue value)`**: Attempts to get a non-expired item
- **`ContainsKey(TKey key)`**: Checks if key exists
- **`Remove(TKey key)`**: Removes an item
- **`Clear()`**: Removes all items
- **`PurgeExpiredItems()`**: Manually purge all expired items
- **`GetItems()`**: Enumerates all non-expired key-value pairs

### Indexer

```csharp
// Get (throws if not found)
var value = cache[key];

// Set (adds or updates)
cache[key] = newValue;
```

## Important Notes

### Add vs Standard Dictionary

Unlike standard `Dictionary<TKey, TValue>`, the `Add` method will **update** an existing key rather than throwing an exception. This is intentional — with time-based caching, you shouldn't need to check if a key exists before adding/updating.

```csharp
cache.Add("key", "value1");
cache.Add("key", "value2"); // Updates, does NOT throw
```

### Count Property

The `Count` property returns the internal dictionary count, which may include recently expired items that haven't been purged yet. For an accurate count of non-expired items, use:

```csharp
int accurateCount = cache.CountPurged(); // Requires full scan
```

### Thread Safety

All operations are thread-safe. Multiple threads can safely read from and write to the cache concurrently, as this is based internally on a `ConcurrentDictionary` instance.

## Testing Support

For unit testing scenarios where you need to control time:

```csharp
CacheDictionary<string, string> cache = new(TimeSpan.FromMinutes(5));

// Override DateTime.UtcNow behavior
DateTime testTime = DateTime.UtcNow;
cache.GetDateTimeNow = () => testTime;

cache.Add("key", "value");

// Simulate time passing
testTime = testTime.AddMinutes(6);

bool found = cache.TryGetValue("key", out _); // Returns false
```

## Performance Characteristics

- **Get/Set Operations**: O(1) average case (ConcurrentDictionary performance) + minimal DateTime comparison overhead
- **Purge Operations**: O(n) where n is the number of items in the dictionary
- **Memory**: Expired items may remain in memory until the next purge, but are never returned

## Use Cases

Perfect for:
- API response caching
- Database query result caching
- Expensive computation result caching
- Session data with automatic timeout
- Rate limiting buckets
- Any scenario requiring thread-safe, time-based data caching
