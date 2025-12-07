# NuGet Package Guide

## Package Information

- **Package Name**: `Nacos.Config.Lite`
- **Current Version**: `1.0.0-rc.1`
- **License**: Apache-2.0
- **Repository**: https://github.com/ccccccmd/nacos-config-sdk

## Building the Package

### Prerequisites

- .NET SDK 6.0 or higher
- NuGet CLI (optional, included with .NET SDK)

### Build Release Version

```powershell
# Navigate to the project directory
cd src

# Build in Release mode
dotnet build -c Release

# Create NuGet package
dotnet pack -c Release -o ../artifacts
```

The package will be created in `artifacts/Nacos.Config.Lite.1.0.0-rc.1.nupkg`

### Build with Specific Version

```powershell
dotnet pack -c Release -o ../artifacts /p:Version=1.0.0-rc.2
```

## Publishing to NuGet.org

### 1. Get API Key

1. Go to https://www.nuget.org/
2. Sign in / Register
3. Go to Account Settings → API Keys
4. Create new API key with "Push" permissions

### 2. Publish Package

```powershell
# Set your API key (one time)
dotnet nuget push artifacts/Nacos.Config.Lite.1.0.0-rc.1.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### 3. Verify Publication

Visit: https://www.nuget.org/packages/Nacos.Config.Lite/

## Version Management

### Pre-release Versions

- Alpha: `1.0.0-alpha`, `1.0.0-alpha.1`, `1.0.0-alpha.2`
- Beta: `1.0.0-beta`, `1.0.0-beta.1`
- RC: `1.0.0-rc.1`, `1.0.0-rc.2`

### Stable Release

- Production: `1.0.0`, `1.0.1`, `1.1.0`

### Semantic Versioning

- **Major** (X.0.0): Breaking changes
- **Minor** (2.X.0): New features, backward compatible
- **Patch** (2.0.X): Bug fixes, backward compatible

## Installation

Users can install the package using:

```powershell
dotnet add package Nacos.Config.Lite
```

Or via Package Manager:

```powershell
Install-Package Nacos.Config.Lite
```

Or in `.csproj`:

```xml
<PackageReference Include="Nacos.Config.Lite" Version="1.0.0-rc.1" />
```

## Package Contents

The package includes:

- ✅ Multi-target support: .NET 6.0, 8.0, 9.0, 10.0
- ✅ XML documentation
- ✅ Symbol packages (.snupkg) for debugging
- ✅ README.md
- ✅ License information

## Local Testing

Before publishing, test the package locally:

```powershell
# Create a local NuGet source
mkdir C:\local-nuget
nuget sources Add -Name "Local" -Source C:\local-nuget

# Copy package to local source
copy artifacts\*.nupkg C:\local-nuget\

# Test in a sample project
dotnet add package Nacos.Config.Lite --source C:\local-nuget
```

## Troubleshooting

### Package validation errors

```powershell
# Validate package before publishing
dotnet nuget verify artifacts/Nacos.Config.Lite.1.0.0-rc.1.nupkg
```

### Inspect package contents

```powershell
# Rename .nupkg to .zip and extract
copy artifacts\Nacos.Config.Lite.1.0.0-rc.1.nupkg temp.zip
# Extract and inspect contents
```
