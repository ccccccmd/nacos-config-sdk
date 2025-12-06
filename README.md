# Nacos Configuration SDK v2

简化的、HTTP-only 的 Nacos 配置中心 SDK,基于现代 .NET 最佳实践重新设计。

> **Gemini AI 提示**: 这是一个为 .NET 开发者精心设计的轻量级 Nacos 配置中心客户端,专注于简洁、高效和易用性。

## ✨ 核心特性

| 特性 | 说明 |
|------|------|
| 🌐 **仅 HTTP 协议** | 移除 gRPC 依赖,简化调试和部署 |
| ⚙️ **配置中心专用** | 移除服务发现功能,专注配置管理 |
| 🔐 **双重认证** | 支持 Username/Password 和 AK/SK 两种认证方式 |
| 🏗️ **现代架构** | 使用 IHttpClientFactory、Channel、SemaphoreSlim 等现代 API |
| 🚀 **高可用** | Failover → Server → Snapshot 三级降级策略 |
| 🔄 **自动重试** | 内置重试机制和服务器故障转移 |
| 💾 **本地快照** | 自动保存配置快照,支持离线使用 |

## 支持框架

- .NET 10.0
- .NET 9.0
- .NET 8.0
- .NET 6.0

## 快速开始

### 1. 安装

```bash
dotnet add package nacos-config-sdk-v2
```

### 2. 配置服务

#### 使用 Username/Password 认证

```csharp
using Nacos.V2.Config.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
    options.Namespace = "your-namespace";
    options.UserName = "nacos";
    options.Password = "nacos";
    options.DefaultTimeoutMs = 15000;
    options.EnableSnapshot = true; // 启用本地快照
});

var app = builder.Build();
```

#### 使用 AK/SK 认证

```csharp
builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
    options.Namespace = "your-namespace";
    options.AccessKey = "your-ak";
    options.SecretKey = "your-sk";
});
```

#### 无需认证(本地开发)

```csharp
builder.Services.AddNacosConfigService(options =>
{
    options.ServerAddresses = new List<string> { "http://localhost:8848" };
});
```

### 3. 使用配置服务

```csharp
using Nacos.V2.Config.Core;

public class YourService
{
    private readonly INacosConfigService _configService;

    public YourService(INacosConfigService configService)
    {
        _configService = configService;
    }

    // 获取配置
    public async Task<string?> GetDatabaseConfig()
    {
        var config = await _configService.GetConfigAsync(
            dataId: "database.json",
            group: "DEFAULT_GROUP"
        );

        return config;
    }

    // 发布配置
    public async Task<bool> PublishConfig()
    {
        return await _configService.PublishConfigAsync(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            content: "{\"key\":\"value\"}",
            type: "json"
        );
    }

    // 删除配置
    public async Task<bool> RemoveConfig()
    {
        return await _configService.RemoveConfigAsync(
            dataId: "old-config",
            group: "DEFAULT_GROUP"
        );
    }

    // 监听配置变化 (待实现)
    public void ListenConfigChanges()
    {
        var subscription = _configService.Subscribe(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            callback: evt =>
            {
                Console.WriteLine($"配置变更: {evt.NewContent}");
            }
        );

        // 取消订阅
        // subscription.Dispose();
    }
}
```

## 配置优先级

获取配置时遵循以下优先级:

1. **Failover** - 手动放置的本地配置文件(最高优先级)
2. **Server** - 从 Nacos 服务器获取
3. **Snapshot** - 本地快照缓存(服务器不可用时降级)

### Failover 文件路径

```
{SnapshotPath}/data/config-data/{tenant}/{group}/{dataId}
```

默认路径: `%LocalAppData%/nacos/config/data/...`

### Snapshot 文件路径

```
{SnapshotPath}/snapshot/{tenant}/{group}/{dataId}
```

## 配置选项

| 选项 | 说明 | 默认值 |
|------|------|--------|
| `ServerAddresses` | Nacos 服务器地址列表 | **必填** |
| `Namespace` | 命名空间(租户) | "" |
| `ContextPath` | 上下文路径 | "nacos" |
| `DefaultTimeoutMs` | 默认超时时间(ms) | 15000 |
| `UserName` | 用户名(用户名密码认证) | null |
| `Password` | 密码(用户名密码认证) | null |
| `AccessKey` | AccessKey(AK/SK认证) | null |
| `SecretKey` | SecretKey(AK/SK认证) | null |
| `MaxRetry` | 最大重试次数 | 3 |
| `RetryDelayMs` | 重试延迟(ms) | 2000 |
| `EnableSnapshot` | 启用本地快照 | true |
| `SnapshotPath` | 快照存储路径 | %LocalAppData%/nacos/config |
| `LongPollingTimeoutMs` | 长轮询超时(ms) | 30000 |
| `ConfigBatchSize` | 批量配置数量 | 3000 |

## 架构设计

SDK 采用清晰的分层架构:

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

### 核心组件

- **Core**: `INacosConfigService` - 用户接口,集成所有功能
- **Client**: `INacosConfigClient` - HTTP API 封装
- **Transport**: `IHttpTransport` - HTTP 传输,使用 IHttpClientFactory
- **Authentication**: 三种认证提供者(Null/UsernamePassword/AkSk)
- **Storage**: 本地快照和 failover 文件管理
- **Listening**: 配置变更监听(待实现)

## 功能状态

### ✅ 已完成

**核心功能:**
- HTTP-only 客户端 (无 gRPC)
- Username/Password 认证
- AK/SK 认证
- 配置 CRUD 操作 (Get/Publish/Remove)
- 配置变更监听 (长轮询)
- 本地快照缓存
- 服务器选择 (轮询)

**质量保障:**
- **Polly 重试机制** (指数退避)
- **xUnit 集成测试** (所有操作)
- **BenchmarkDotNet 性能测试**
- 外部配置文件 (tests/benchmarks)
- 连接池验证 (1000+ req/s)
- 性能基准测试完成

### ⏳ 待实现

**高优先级:**
- [ ] 熔断器模式 (Circuit Breaker)
- [ ] 分布式追踪 (OpenTelemetry)
- [ ] 配置加密/解密
- [ ] .NET Standard 2.0 支持

**中优先级:**
- [ ] 配置版本管理和回滚
- [ ] 批量配置操作
- [ ] 配置导入/导出工具
- [ ] 管理 API

**低优先级:**
- [ ] 配置对比和合并工具
- [ ] 健康检查端点
- [ ] Metrics 导出 (Prometheus 格式)

## 对比现有 SDK

| 方面 | 现有 SDK | v2 SDK |
|------|----------|--------|
| HTTP Client | ❌ 静态实例 | ✅ IHttpClientFactory |
| 异步模式 | ⚠️ Timer递归 | ✅ Task/Channel |
| 认证管理 | ⚠️ 分散 | ✅ 统一抽象 |
| 并发控制 | ⚠️ ConcurrentDict | ✅ SemaphoreSlim |
| 可测试性 | ⚠️ 一般 | ✅ 依赖注入 |
| 代码复杂度 | ⚠️ 高 | ✅ 简化 |

## License

Apache-2.0
