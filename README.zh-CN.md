# Nacos.Config.Lite

[![NuGet](https://img.shields.io/nuget/v/Nacos.Config.Lite.svg)](https://www.nuget.org/packages/Nacos.Config.Lite/)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

**[English](README.md) | 中文**

生产就绪的轻量级 Nacos 配置中心 SDK for .NET。基于现代 async/await 模式重新设计，零 gRPC 依赖，专为微服务和云原生应用优化。

> **Why Nacos.Config.Lite?** 简洁、高效、生产就绪。支持自动故障转移和本地快照，让配置管理变得简单可靠。

## ✨ 核心特性

| 特性 | 说明 |
|------|------|
| 🚀 **生产就绪** | 正式版 v1.0.0，核心功能完整，经过生产环境真实业务验证 |
| 🌐 **HTTP-Only** | 零 gRPC 依赖，简化部署和调试 |
| ⚡ **高性能** | 基于 IHttpClientFactory，100并发请求仅 77ms，1MB 内存占用 |
| 🔐 **双重认证** | 支持 Username/Password 和 AK/SK 认证 |
| 🏗️ **现代架构** | async/await、Channel、SemaphoreSlim 等现代 API |
| 🚀 **高可用** | Failover → Server → Snapshot 三级降级策略 |
| 🔄 **智能重试** | Polly 重试策略 + 自动服务器故障转移 |
| 💾 **本地快照** | 自动保存配置快照，支持离线使用 |
| 📡 **实时监听** | 长轮询机制，实时获取配置变更通知 |

## 支持框架

- .NET 10.0
- .NET 9.0
- .NET 8.0
- .NET 6.0

## 快速开始

### 1. 安装

```bash
dotnet add package Nacos.Config.Lite
```

### 2. 配置服务

#### 使用 Username/Password 认证

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
using Nacos.Config.Core;

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

    // 监听配置变化
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

    // 使用异步回调监听(支持异步操作)
    public void ListenWithAsyncCallback()
    {
        var subscription = _configService.Subscribe(
            dataId: "app-config.json",
            group: "DEFAULT_GROUP",
            asyncCallback: async evt =>
            {
                // 执行异步操作
                await SaveToDatabase(evt.NewContent);
                await NotifyExternalService(evt.NewContent);
                Console.WriteLine($"异步处理完成: {evt.NewContent}");
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
| `Namespace` | 命名空间(租户)ID | **必填** |
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
- **Listening**: 配置变更监听管理器

## 📊 性能基准

基于阿里云 ECS (1c2g, Nacos v2.3.2.0) 的真实测试结果：

| 并发请求数 | 平均耗时 | 内存分配 | Gen0 GC |
|-----------|---------|---------|----------|
| 10        | 31 ms   | 113 KB  | -        |
| 50        | 47 ms   | 543 KB  | -        |
| 100       | 77 ms   | 1086 KB | 111.1111 |

*每个请求平均内存分配：~10KB，符合业界标准*

## 功能状态

### ✅ v1.0.0 已完成

**核心功能:**
- ✅ HTTP-only 客户端 (零 gRPC 依赖)
- ✅ Username/Password 认证
- ✅ AK/SK 签名认证
- ✅ 配置 CRUD 操作 (Get/Publish/Remove)
- ✅ 配置变更监听 (长轮询 + Channel)
- ✅ 本地快照缓存
- ✅ 服务器轮询选择
- ✅ 三级降级策略 (Failover/Server/Snapshot)

**质量保障:**
- ✅ Polly 重试机制 (指数退避)
- ✅ xUnit 集成测试 (覆盖所有操作)
- ✅ BenchmarkDotNet 性能测试
- ✅ 连接池优化 (IHttpClientFactory)
- ✅ 内存分配优化 (~10KB/请求)

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

## 对比现有 SDK

| 方面 | 现有 SDK | ME |
|------|----------|--------|
| HTTP Client | ❌ 静态实例 | ✅ IHttpClientFactory |
| 异步模式 | ⚠️ Timer递归 | ✅ Task/Channel |
| 认证管理 | ⚠️ 分散 | ✅ 统一抽象 |
| 并发控制 | ⚠️ ConcurrentDict | ✅ SemaphoreSlim |
| 可测试性 | ⚠️ 一般 | ✅ 依赖注入 |
| 代码复杂度 | ⚠️ 高 | ✅ 简化 |

## License

Apache-2.0
