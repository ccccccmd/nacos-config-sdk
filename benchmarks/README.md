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

Typical results on aliyun(1c2g v2.3.2.0) public network:

| Method             | requestCount | Mean     | Error    | StdDev    | Gen0     | Allocated  |
|------------------- |------------- |---------:|---------:|----------:|---------:|-----------:|
| ConcurrentRequests | 10           | 31.09 ms | 11.07 ms |  2.874 ms |        - |  112.79 KB |
| ConcurrentRequests | 50           | 47.30 ms | 14.43 ms |  2.233 ms |        - |  543.37 KB |
| ConcurrentRequests | 100          | 77.03 ms | 92.34 ms | 14.290 ms | 111.1111 | 1086.37 KB |

*Results may vary based on network latency and Nacos server performance*

## Notes

- Benchmarks use `[MemoryDiagnoser]` to track allocations
- Warmup runs ensure JIT compilation is complete
- Connection pooling should show linear scaling with concurrency
- Snapshot caching should reduce network calls significantly
