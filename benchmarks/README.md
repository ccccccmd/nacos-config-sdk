# Nacos Config SDK Benchmarks

Performance benchmarks using BenchmarkDotNet for the Nacos Configuration SDK.

## Prerequisites

- .NET 9.0 SDK
- Running Nacos server (default: http://192.168.1.55:8848)
- Valid credentials configured in `Program.cs`

## Running Benchmarks

```powershell
cd benchmarks/NacosBenchmarks
dotnet run -c Release
```

## Benchmark Categories

### 1. ConfigOperationBenchmarks

Measures performance of basic CRUD operations:
- `GetConfig` - Single config retrieval
- `PublishConfig` - Single config publish
- `GetAndPublish` - Combined read-write operation

**Metrics**: Execution time, Memory allocation

### 2. ConnectionPoolingBenchmarks

Tests concurrent request handling:
- 10 concurrent requests
- 50 concurrent requests  
- 100 concurrent requests

**Metrics**: Throughput, Connection reuse efficiency

### 3. SnapshotPerformanceBenchmarks

Compares performance with/without local snapshot caching:
- Multiple config reads with snapshot enabled
- Multiple config reads with snapshot disabled

**Metrics**: Read latency, Memory usage

## Configuration

Update connection settings in `Program.cs`:
```csharp
options.ServerAddresses = new List<string> { "http://your-nacos-server:8848" };
options.Namespace = "your-namespace-id";
options.UserName = "your-username";
options.Password = "your-password";
```

## Expected Results

Typical results on local network:

| Operation | Mean Time | Allocated Memory |
|-----------|-----------|------------------|
| GetConfig | ~50-100ms | ~2-5 KB |
| PublishConfig | ~100-200ms | ~5-10 KB |
| 100 Concurrent | ~500ms | ~200 KB |

*Results may vary based on network latency and Nacos server performance*

## Notes

- Benchmarks use `[MemoryDiagnoser]` to track allocations
- Warmup runs ensure JIT compilation is complete
- Connection pooling should show linear scaling with concurrency
- Snapshot caching should reduce network calls significantly
