# Roomzin C# SDK

Official C# SDK for [Roomzin](https://m-javani.github.io/roomzin-doc/) — a high-performance in-memory inventory engine for booking platforms.

The SDK provides a modern, idiomatic C# interface for communicating with Roomzin servers in both standalone and clustered deployments. It automatically manages routing, failover, connection pooling, and cluster topology changes.

---

## Features

- Automatic request routing (leader for writes, followers for reads)
- Built-in failover and cluster discovery
- Connection pooling
- Standalone and clustered deployment support
- Fully typed C# API
- Async/await support
- `IAsyncDisposable` client for resource management

---

## Requirements

- .NET 6 or later
- Roomzin Server v1.x

---

## Installation

```bash
dotnet add package Roomzin.Sdk
```

Or via the NuGet Package Manager:

```
Install-Package Roomzin.Sdk
```

---

## Client Setup

### Standalone

```csharp
using Roomzin.Sdk;
using Roomzin.Sdk.Api;

var cfg = new ConfigBuilder()
    .WithHost("127.0.0.1")
    .WithTcpPort(7777)
    .WithToken("abc123")
    .WithTimeout(TimeSpan.FromSeconds(5))
    .WithKeepAlive(TimeSpan.FromSeconds(30))
    .Build();

var client = Client.New(cfg);
await client.CloseAsync();
```

### Cluster (Static Discovery)

```csharp
using Roomzin.Sdk;
using Roomzin.Sdk.Api;
using Roomzin.Sdk.Types;

var staticDiscovery = new List<NodeAddr>
{
    new("roomzin-0", "172.20.0.10", 7777, 8080),
    new("roomzin-1", "172.20.0.11", 7777, 8080),
    new("roomzin-2", "172.20.0.12", 7777, 8080)
};

var cfg = new ClusterConfigBuilder()
    .WithSeedNodeIds("roomzin-0,roomzin-1,roomzin-2")
    .WithStaticDiscovery(staticDiscovery)
    .WithTcpPort(7777)
    .WithApiPort(8080)
    .WithToken("abc123")
    .WithTimeout(TimeSpan.FromSeconds(5))
    .WithKeepAlive(TimeSpan.FromSeconds(30))
    .Build();

var client = Client.New(cfg);
await client.CloseAsync();
```

### Cluster (HTTP Discovery)

```csharp
var cfg = new ClusterConfigBuilder()
    .WithSeedNodeIds("roomzin-0,roomzin-1,roomzin-2")
    .WithHttpDiscovery("http://discovery-service:8080/nodes")
    .WithTcpPort(7777)
    .WithApiPort(8080)
    .WithToken("abc123")
    .WithTimeout(TimeSpan.FromSeconds(5))
    .WithKeepAlive(TimeSpan.FromSeconds(30))
    .Build();

var client = Client.New(cfg);
```

---

## Discovery Configuration

Roomzin SDKs need to know how to reach each Roomzin node in the cluster. The cluster nodes communicate with each other using internal address resolvers, but the SDK as an external client needs actual network addresses (IP:port or hostname:port) to connect.

The SDK fetches the cluster topology from the Roomzin cluster itself. This topology includes the node identities of the leader and followers. The SDK then uses discovery to resolve these node identities into actual network addresses.

Two discovery modes are supported:

### Static Discovery

The SDK gets the mapping once in config and never updates it. Use this when your cluster nodes have stable, predictable addresses.

### HTTP Discovery

The SDK periodically fetches the mapping from an HTTP endpoint. Use this when cluster nodes are dynamic (e.g., Kubernetes pods with changing IPs).

---

## Property Management

### SetPropAsync
Adds or updates a property.

```csharp
await client.SetPropAsync(new SetPropPayload
{
    Segment = "downtown",
    Area = "manhattan",
    PropertyId = "hotel_123",
    PropertyType = "hotel",
    Category = "luxury",
    Stars = 4,
    Latitude = 40.7128,
    Longitude = -74.0060,
    Amenities = new List<string> { "wifi", "pool", "gym" }
});
```

### SearchPropAsync
Searches properties by segment, area, type, or location.

```csharp
// By segment
var ids = await client.SearchPropAsync(new SearchPropPayload
{
    Segment = "downtown"
});

// By area
var ids = await client.SearchPropAsync(new SearchPropPayload
{
    Segment = "downtown",
    Area = "manhattan"
});

// By location (radius search)
var ids = await client.SearchPropAsync(new SearchPropPayload
{
    Segment = "downtown",
    Latitude = 40.7128,
    Longitude = -74.0060
});
```

### PropExistAsync
Checks if a property exists.

```csharp
bool exists = await client.PropExistAsync("hotel_123");
```

### PropRoomExistAsync
Checks if a specific room type exists for a property.

```csharp
bool exists = await client.PropRoomExistAsync(new PropRoomExistPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite"
});
```

### PropRoomListAsync
Lists all room types for a property.

```csharp
var rooms = await client.PropRoomListAsync("hotel_123");
```

### PropRoomDateListAsync
Lists dates with availability data for a property and room type.

```csharp
var dates = await client.PropRoomDateListAsync(new PropRoomDateListPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite"
});
```

---

## Room Package Management

### SetRoomPkgAsync
Sets availability, price, and rate features for a room type on a date.

```csharp
await client.SetRoomPkgAsync(new SetRoomPkgPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20",
    Availability = 10,
    FinalPrice = 199,
    RateFeature = new List<string> { "free_cancellation", "breakfast_included" }
});
```

### SetRoomAvlAsync
Sets exact availability for a room type on a specific date.

```csharp
byte newAvail = await client.SetRoomAvlAsync(new UpdRoomAvlPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20",
    Amount = 20
});
```

### IncRoomAvlAsync
Increases availability (e.g., on cancellation).

```csharp
byte newAvail = await client.IncRoomAvlAsync(new UpdRoomAvlPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20",
    Amount = 1
});
```

### DecRoomAvlAsync
Decreases availability (e.g., on booking).

```csharp
byte newAvail = await client.DecRoomAvlAsync(new UpdRoomAvlPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20",
    Amount = 2
});
```

### GetPropRoomDayAsync
Gets availability and pricing for a specific room on a specific date.

```csharp
var day = await client.GetPropRoomDayAsync(new GetRoomDayRequest
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20"
});
Console.WriteLine($"Avail: {day.Availability}, Price: {day.FinalPrice}");
```

---

## Search & Query

### SearchAvailAsync
Searches available rooms by filters.

```csharp
var results = await client.SearchAvailAsync(new SearchAvailPayload
{
    Segment = "downtown",
    RoomType = "suite",
    Date = new List<string> { "2026-07-20", "2026-07-21" },
    Limit = 50,
    MinPrice = 100,
    MaxPrice = 300,
    Amenities = new List<string> { "wifi", "pool" },
    RateFeature = new List<string> { "free_cancellation" }
});

foreach (var result in results)
{
    Console.WriteLine($"Property: {result.PropertyId}");
    foreach (var day in result.Days)
    {
        Console.WriteLine($"  {day.Date}: Avail {day.Availability}, Price {day.FinalPrice}");
    }
}
```

### GetSegmentsAsync
Lists all active segments with their property counts.

```csharp
var segments = await client.GetSegmentsAsync();
foreach (var seg in segments)
{
    Console.WriteLine($"{seg.Segment}: {seg.Count} properties");
}
```

### GetCodecsAsync
Gets the current codec registry (used internally for validation).

```csharp
var codecs = await client.GetCodecsAsync();
Console.WriteLine(string.Join(", ", codecs.RateFeatures));
```

---

## Delete Operations

### DelRoomDayAsync
Deletes availability for a specific room on a specific date.

```csharp
await client.DelRoomDayAsync(new DelRoomDayRequest
{
    PropertyId = "hotel_123",
    RoomType = "suite",
    Date = "2026-07-20"
});
```

### DelPropDayAsync
Deletes all data for a property on a specific date.

```csharp
await client.DelPropDayAsync(new DelPropDayRequest
{
    PropertyId = "hotel_123",
    Date = "2026-07-20"
});
```

### DelPropRoomAsync
Deletes a room type from a property.

```csharp
await client.DelPropRoomAsync(new DelPropRoomPayload
{
    PropertyId = "hotel_123",
    RoomType = "suite"
});
```

### DelPropAsync
Deletes an entire property.

```csharp
await client.DelPropAsync("hotel_123");
```

### DelSegmentAsync
Deletes a segment and all properties within it.

```csharp
await client.DelSegmentAsync("downtown");
```

---

## Error Handling

All async methods throw `RoomzinException`. Use the static helper methods to classify errors:

```csharp
try
{
    await client.SetRoomPkgAsync(payload);
}
catch (RoomzinException ex) when (RoomzinException.IsRequest(ex))
{
    // Business rule violation - fix the request
    Console.WriteLine($"Request error: {ex.Code}");
}
catch (RoomzinException ex) when (RoomzinException.IsRetry(ex))
{
    // Temporary condition - retry with backoff
    await Task.Delay(100);
    await client.SetRoomPkgAsync(payload);
}
catch (RoomzinException ex) when (RoomzinException.IsClient(ex))
{
    // Authentication or protocol errors
    Console.WriteLine($"Client error: {ex.Message}");
}
catch (RoomzinException ex) when (RoomzinException.IsInternal(ex))
{
    // Unexpected server response
    throw new InvalidOperationException("Internal error", ex);
}
```

### Error Categories

| Category | Description | Action |
|----------|-------------|--------|
| **Client** | Authentication or protocol errors | Check credentials and configuration |
| **Request** | Invalid input or business rule violation | Fix request, don't retry |
| **Retry** | Temporary server condition (429, 503, 308) | Retry with backoff |
| **Internal** | Unexpected server response | Log and investigate |

---

## Client Lifecycle

Create a **single client** during application startup and reuse it throughout your application.

```csharp
// ✅ Good - create once, reuse
var client = Client.New(cfg);
// Use client everywhere...
await client.CloseAsync();

// ❌ Bad - creating per request
foreach (var req in requests)
{
    var client = Client.New(cfg); // Don't do this
    await client.SetRoomPkgAsync(req);
    await client.CloseAsync();
}
```

The client is safe for concurrent use and manages TCP connections internally.

---

## API Reference

For the complete interface definition, see `src/Roomzin.Sdk/Api/ICacheClientApi.cs`. All types are documented with XML comments.

---

## Documentation

For Roomzin concepts, deployment, and administration:

[https://m-javani.github.io/roomzin-doc/docs.html](https://m-javani.github.io/roomzin-doc/docs.html)

---

## Contributing

Contributions are welcome! Please open an issue before proposing large changes.

All contributions are subject to the BUSL-1.1 License terms.

---

## License

This SDK is licensed under the [BUSL-1.1 License](https://github.com/m-javani/roomzin-csharp/blob/main/LICENSE).

**Note:** This SDK communicates with Roomzin Server, which requires a valid Roomzin license.

---

## Support

- **Documentation**: [roomzin-doc](https://m-javani.github.io/roomzin-doc/)
- **Community Q&A**: [GitHub Discussions](https://github.com/m-javani/roomzin-doc/discussions)
- **Issues**: [GitHub Issues](https://github.com/roomzin/roomzin-csharp/issues)
- **Security**: [mehdy.javany@gmail.com](mailto:mehdy.javany@gmail.com)

---

## Related Repositories

- [Roomzin Quickstart](https://github.com/m-javani/roomzin-quickstart) — Local Docker cluster
- [Roomzin Bench](https://github.com/m-javani/roomzin-bench) — Benchmarking tool