# Nacos Config SDK Tests

This directory contains xUnit integration tests for the Nacos Configuration SDK.

## Configuration

Tests require a configuration file to connect to Nacos server.

### Setup Steps

1. **Copy the example configuration**:
   ```bash
   cp appsettings.Test.example.json appsettings.Test.json
   ```

2. **Update `appsettings.Test.json` with your Nacos server details**:
   ```json
   {
     "Nacos": {
       "ServerAddresses": ["http://your-nacos-server:8848"],
       "Namespace": "your-namespace-id",
       "UserName": "your-username",
       "Password": "your-password",
       "EnableSnapshot": true,
       "DefaultTimeoutMs": 10000,
       "LongPollingTimeoutMs": 30000
     }
   }
   ```

3. **Note**: `appsettings.Test.json` is git-ignored to protect sensitive credentials.

## Test Projects

### NacosConfigTests

Integration tests that interact with a real Nacos server.

**Test Classes:**
- `ConfigOperationsTests` - Basic configuration operations (Get, Publish, Remove, Update)
- `ConfigListenerTests` - Configuration change listening (Basic listener, Multiple listeners, Listener removal)

## Running Tests

### Prerequisites

1. **Running Nacos Server** - Tests require a running Nacos instance
2. **Configured appsettings.Test.json** - As described above

### Run All Tests

```powershell
cd tests/NacosConfigTests
dotnet test
```

### Run Specific Test Class

```powershell
dotnet test --filter "FullyQualifiedName~ConfigOperationsTests"
dotnet test --filter "FullyQualifiedName~ConfigListenerTests"
```

### Run Specific Test

```powershell
dotnet test --filter "FullyQualifiedName~PublishConfig_ShouldSucceed"
```

## Notes

- Tests are **integration tests** that require a real Nacos server
- Tests use unique GUIDs in content to ensure freshness
- Tests include cleanup logic in `finally` blocks
- Listener tests include appropriate delays for propagation
- Tests use `IAsyncLifetime` for proper setup/teardown
- Configuration is loaded from `appsettings.Test.json` (not committed to git)
