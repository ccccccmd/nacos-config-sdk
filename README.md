# Nacos.Config.Lite

[![NuGet](https://img.shields.io/nuget/v/Nacos.Config.Lite.svg)](https://www.nuget.org/packages/Nacos.Config.Lite/)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

**[中文文档](README.zh-CN.md)**

A production-ready, lightweight Nacos configuration SDK for .NET. Redesigned with modern async/await patterns, zero gRPC dependencies, optimized for microservices and cloud-native applications.

> **Why Nacos.Config.Lite?** Simple, efficient, and production-ready. Supports automatic failover and local snapshots, making configuration management simple and reliable.

## ✨ Key Features

| Feature | Description |
|---------|-------------|
| 🚀 **Production Ready** | RC version with complete core features, validated through benchmarks and integration tests |
| 🌐 **HTTP-Only** | Zero gRPC dependencies, simplified deployment and debugging |
| ⚡ **High Performance** | Based on IHttpClientFactory, 100 concurrent requests in 77ms, 1MB memory footprint |
| 🔐 **Dual Authentication** | Supports both Username/Password and AK/SK authentication |
| 🏗️ **Modern Architecture** | Built with async/await, Channel, SemaphoreSlim and other modern APIs |
| 🚀 **High Availability** | Three-tier fallback strategy: Failover → Server → Snapshot |
| 🔄 **Smart Retry** | Polly retry policy + automatic server failover |
| 💾 **Local Snapshot** | Auto-save configuration snapshots, supports offline usage |
| 📡 **Real-time Listening** | Long-polling mechanism for instant config change notifications |

## Supported Frameworks

- .NET 10.0
- .NET 9.0
- .NET 8.0
- .NET 6.0

## Quick Start

### 1. Installation

```bash
dotnet add package Nacos.Config.Lite
```

### 2. Configure Service

#### Using Username/Password Authentication

```csharp
using Nacos.Config.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
    options.Namespace = "your-namespace";
    options.UserName = "nacos";
    options.Password = "nacos";
    options.DefaultTimeoutMs = 15000;
    options.EnableSnapshot = true; // Enable local snapshots
});

var app = builder.Build();
```

#### Using AK/SK Authentication

```csharp
builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
    options.Namespace = "your-namespace";
    options.AccessKey = "your-ak";
    options.SecretKey = "your-sk";
});
```

#### No Authentication (Local Development)

```csharp
builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
});
```

### 3. Use Configuration Service

```csharp
using Nacos.Config.Core;

public class YourService
{
    private readonly INacosConfigService _configService;

    public YourService(INacosConfigService configService)
    {
        _configService = configService;
    }

    // Get configuration
    public async Task<string?> GetDatabaseConfig()
    {
        var config = await _configService.GetConfigAsync(
            dataId: "database.json",
            group: "DEFAULT_GROUP"
        );

        return config;
    }

    // Publish configuration
    public async Task<bool> PublishConfig()
    {
        return await _configService.PublishConfigAsync(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            content: "{\"key\":\"value\"}",
            type: "json"
        );
    }

    // Remove configuration
    public async Task<bool> RemoveConfig()
    {
        return await _configService.RemoveConfigAsync(
            dataId: "old-config",
            group: "DEFAULT_GROUP"
        );
    }

    // Listen for configuration changes
    public void ListenConfigChanges()
    {
        var subscription = _configService.Subscribe(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            callback: evt =>
            {
                Console.WriteLine($"Config changed: {evt.NewContent}");
            }
        );

        // Unsubscribe
        // subscription.Dispose();
    }

    // Listen with async callback (for async operations)
    public void ListenWithAsyncCallback()
    {
        var subscription = _configService.Subscribe(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            asyncCallback: async evt =>
            {
                // Perform async operations
                await SaveToDatabase(evt.NewContent);
                await NotifyExternalService(evt.NewContent);
                Console.WriteLine($"Async processing complete: {evt.NewContent}");
            }
        );

        // Unsubscribe
        // subscription.Dispose();
    }
}
```

## Configuration Priority

When retrieving configuration, the following priority order is followed:

1. **Failover** - Manually placed local configuration files (highest priority)
2. **Server** - Retrieved from Nacos server
3. **Snapshot** - Local snapshot cache (fallback when server is unavailable)

### Failover File Path

```
{SnapshotPath}/data/config-data/{tenant}/{group}/{dataId}
```

Default path: `%LocalAppData%/nacos/config/data/...`

### Snapshot File Path

```
{SnapshotPath}/snapshot/{tenant}/{group}/{dataId}
```

## Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `ServerAddresses` | Nacos server address list | **Required** |
| `Namespace` | NamespaceId | **Required** |
| `ContextPath` | Context path | "nacos" |
| `DefaultTimeoutMs` | Default timeout (ms) | 15000 |
| `UserName` | Username (for username/password auth) | null |
| `Password` | Password (for username/password auth) | null |
| `AccessKey` | AccessKey (for AK/SK auth) | null |
| `SecretKey` | SecretKey (for AK/SK auth) | null |
| `MaxRetry` | Maximum retry attempts | 3 |
| `RetryDelayMs` | Retry delay (ms) | 2000 |
| `EnableSnapshot` | Enable local snapshots | true |
| `SnapshotPath` | Snapshot storage path | %LocalAppData%/nacos/config |
| `LongPollingTimeoutMs` | Long-polling timeout (ms) | 30000 |
| `ConfigBatchSize` | Batch config size | 3000 |

## Architecture Design

The SDK uses a clean layered architecture:

```
Application
    ↓
INacosConfigService (Core)
    ↓
├─ INacosConfigClient (HTTP API)
│   └─ IHttpTransport (Transport)
│       ├─ IServerSelector (Server Selection)
│       └─ IAuthenticationProvider (Authentication)
│
└─ ILocalConfigStorage (Storage)
```

### Core Components

- **Core**: `INacosConfigService` - User-facing API, integrates all features
- **Client**: `INacosConfigClient` - HTTP API wrapper
- **Transport**: `IHttpTransport` - HTTP transport using IHttpClientFactory
- **Authentication**: Three authentication providers (Null/UsernamePassword/AkSk)
- **Storage**: Local snapshot and failover file management
- **Listening**: Configuration change listener manager

## 📊 Performance Benchmarks

Real-world test results on Aliyun ECS (1c2g, Nacos v2.3.2.0):

| Concurrent Requests | Avg Latency | Memory Allocation | Gen0 GC |
|---------------------|-------------|-------------------|----------|
| 10                  | 31 ms       | 113 KB            | -        |
| 50                  | 47 ms       | 543 KB            | -        |
| 100                 | 77 ms       | 1086 KB           | 111.1111 |

*Average memory per request: ~10KB, industry standard*

## Feature Status

### ✅ v1.0.0-rc.1 Completed

**Core Features:**
- ✅ HTTP-only client (zero gRPC dependencies)
- ✅ Username/Password authentication
- ✅ AK/SK signature authentication
- ✅ Config CRUD operations (Get/Publish/Remove)
- ✅ Config change listening (long-polling + Channel)
- ✅ Local snapshot caching
- ✅ Server round-robin selection
- ✅ Three-tier fallback strategy (Failover/Server/Snapshot)

**Quality Assurance:**
- ✅ Polly retry mechanism (exponential backoff)
- ✅ xUnit integration tests (full operation coverage)
- ✅ BenchmarkDotNet performance testing
- ✅ Connection pool optimization (IHttpClientFactory)
- ✅ Memory allocation optimization (~10KB/request)

### ⏳ Planned Features

**High Priority:**
- [ ] Circuit Breaker pattern
- [ ] Distributed Tracing (OpenTelemetry)
- [ ] Config encryption/decryption
- [ ] .NET Standard 2.0 support

**Medium Priority:**
- [ ] Configuration versioning and rollback
- [ ] Batch configuration operations
- [ ] Config import/export tools
- [ ] Management API

**Low Priority:**
- [ ] Config comparison and merge tools
- [ ] Health check endpoints

## Comparison with Existing SDK

| Aspect | Existing SDK | Nacos.Config.Lite |
|--------|--------------|-------------------|
| HTTP Client | ❌ Static instances | ✅ IHttpClientFactory |
| Async Pattern | ⚠️ Timer recursion | ✅ Task/Channel |
| Auth Management | ⚠️ Scattered | ✅ Unified abstraction |
| Concurrency Control | ⚠️ ConcurrentDict | ✅ SemaphoreSlim |
| Testability | ⚠️ Average | ✅ Dependency Injection |
| Code Complexity | ⚠️ High | ✅ Simplified |

## License

Apache-2.0
