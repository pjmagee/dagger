# Local Testing Guide for C# SDK

This guide shows how to test and explore the C# SDK locally, understand what code gets generated, and validate changes during development.

## Quick Start

### 1. Initialize a Test Module

From outside the Dagger repository:

```bash
# Create a test directory
mkdir my-csharp-test
cd my-csharp-test

# Initialize with the C# SDK
dagger init --sdk=csharp --name=test-module

# Explore what was generated
ls -la
```

You should see:
```
.gitattributes
.gitignore
dagger.json          # Module configuration
DaggerModule.csproj  # .NET project file
Main.cs              # Your module code (template)
Program.cs           # Module entrypoint
sdk/                 # Generated SDK (created after first build)
```

### 2. Examine Generated Code

After the first `dagger` command, the SDK is generated:

```bash
# List functions (triggers SDK generation)
dagger functions

# Now examine the generated SDK
ls -la sdk/

# Key files:
# sdk/Dagger.SDK.g.cs           - Full Dagger API client (~25,000 lines)
# sdk/Dagger.SDK.csproj          - SDK project configuration
# sdk/Module/ModuleRuntime.cs    - Module execution infrastructure
# sdk/GraphQL/                   - GraphQL client implementation
# sdk/Attributes/                - Dagger attribute definitions
```

### 3. Explore the Generated API

The `Dagger.SDK.g.cs` file contains all Dagger types and methods:

```bash
# View the API surface (PowerShell)
Select-String -Path sdk/Dagger.SDK.g.cs -Pattern "^public class" | Select-Object -First 20

# Or using grep
grep "^public class" sdk/Dagger.SDK.g.cs | head -20
```

You'll see classes like:
- `Container` - Container operations
- `Directory` - Directory operations
- `File` - File operations
- `Query` - Main Dagger client
- `Module` - Module definition
- `Function` - Function definition
- `TypeDef` - Type definitions
- And many more...

### 4. Test with Local SDK Changes

To test changes from your local Dagger repository:

```bash
# From your test directory
cd my-csharp-test

# Clean existing SDK
rm -rf sdk/

# Reinitialize with local SDK path
dagger develop --sdk=/path/to/dagger/sdk/csharp/runtime

# Or use a specific branch
dagger develop --sdk=github.com/yourusername/dagger/sdk/csharp/runtime@your-branch
```

## Understanding the Generated Structure

### Main.cs (Template)

The generated template shows basic Dagger patterns:

```csharp
using Dagger;
using static Dagger.SDK.Dagger;

namespace TestModule;

[DaggerObject]
public class TestModule
{
    [DaggerFunction]
    public Container ContainerEcho(string stringArg)
    {
        return Dag
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", stringArg });
    }

    [DaggerFunction]
    public async Task<string> GrepDir(Directory directoryArg, string pattern)
    {
        return await Dag
            .Container()
            .From("alpine:latest")
            .WithMountedDirectory("/mnt", directoryArg)
            .WithWorkdir("/mnt")
            .WithExec(new[] { "grep", "-R", pattern, "." })
            .StdoutAsync();
    }
}
```

### Program.cs (Entrypoint)

The module entrypoint handles Dagger's function invocation protocol:

```csharp
using Dagger.SDK.Runtime;

await ModuleRuntime.RunAsync(args);
```

### DaggerModule.csproj

The project file references the generated SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Generated SDK is compiled as source -->
    <Compile Include="sdk/**/*.cs" />
  </ItemGroup>
</Project>
```

## Testing Specific Features

### Testing Advanced Features

Create a test module with advanced SDK features:

```csharp
using Dagger;
using static Dagger.SDK.Dagger;

[DaggerObject(Description = "Test advanced features")]
public class AdvancedTest
{
    // Custom fields
    [DaggerField(Description = "Configuration value")]
    public string Config { get; } = "default";

    // Cache policies
    [DaggerFunction(Cache = "5m", Description = "Cached for 5 minutes")]
    public Container CachedBuild()
    {
        return Dag.Container().From("alpine:latest");
    }

    // Enums
    [DaggerFunction]
    public async Task<string> ProcessEnv(BuildEnv env)
    {
        return $"Environment: {env}";
    }
}

[DaggerEnum(Description = "Build environments")]
public enum BuildEnv
{
    [DaggerEnumValue(Description = "Development")]
    DEVELOPMENT,
    
    [DaggerEnumValue(Description = "Production")]
    PRODUCTION,
}
```

Test the features:

```bash
# List fields and functions
dagger functions

# Test field access
dagger call config

# Test cached function
dagger call cached-build

# Test enum parameter
dagger call process-env --env=PRODUCTION
```

### Testing Error Handling

```csharp
[DaggerFunction]
public Container TestError(string input)
{
    if (string.IsNullOrEmpty(input))
    {
        throw new ArgumentException("Input cannot be empty");
    }
    
    return Dag.Container().From("alpine:latest");
}
```

Test error behavior:

```bash
# Should show error message
dagger call test-error --input=""
```

## Debugging Generated Code

### View Generated API Methods

To see what methods are available on a type:

```bash
# PowerShell: Find all Container methods
Select-String -Path sdk/Dagger.SDK.g.cs -Pattern "public.*Container" -Context 0,1 | Select-Object -First 30

# Bash/grep: Find all Container methods  
grep -A 1 "public.*Container" sdk/Dagger.SDK.g.cs | head -60
```

### Inspect Type Definitions

Look for specific types and their signatures:

```bash
# Find WithExec method signatures
Select-String -Path sdk/Dagger.SDK.g.cs -Pattern "WithExec" -Context 2,5
```

### Check Attribute Definitions

See what attributes are available:

```bash
ls sdk/Attributes/

# DaggerObjectAttribute.cs
# DaggerFunctionAttribute.cs
# DaggerFieldAttribute.cs
# DaggerEnumAttribute.cs
# DaggerEnumValueAttribute.cs
# DaggerIgnoreAttribute.cs
```

## Common Testing Scenarios

### Test Module Registration

Check if your module registers correctly:

```bash
# Should list your module and functions
dagger functions

# Check module metadata
dagger call --help
```

### Test Type Discovery

Verify that types are discovered:

```bash
# Create a module with multiple classes
# Only classes with [DaggerObject] should be registered
dagger functions
```

### Test Code Generation

Force regeneration of SDK:

```bash
# Remove generated SDK
rm -rf sdk/

# Regenerate by running any command
dagger functions

# Verify SDK was regenerated
ls sdk/Dagger.SDK.g.cs
```

### Test Build Process

See the full build output:

```bash
# Run with verbose output
dagger --debug call container-echo --string-arg="test"
```

## Comparing with Other SDKs

To understand differences, compare with other SDK outputs:

```bash
# Initialize multiple test modules
mkdir sdk-comparison
cd sdk-comparison

# Create TypeScript module
mkdir ts-test
cd ts-test
dagger init --sdk=typescript --name=ts-module
cd ..

# Create Python module
mkdir py-test
cd py-test
dagger init --sdk=python --name=py-module
cd ..

# Create C# module
mkdir cs-test
cd cs-test
dagger init --sdk=csharp --name=cs-module
cd ..

# Compare structures
ls ts-test/
ls py-test/
ls cs-test/
```

## Performance Testing

Test build and execution performance:

```bash
# Time initial generation
time dagger functions

# Time cached execution
time dagger call container-echo --string-arg="test"

# Test with complex operations
time dagger call grep-dir --directory-arg=. --pattern="TODO"
```

## Troubleshooting

### SDK Not Generating

If the SDK directory is missing:

```bash
# Check Dagger version
dagger version

# Force regeneration
rm -rf sdk/
dagger develop

# Check for errors
dagger --debug functions
```

### Build Errors

If you see compilation errors:

```bash
# Check .NET version
dotnet --version  # Should be 9.0 or later

# Try manual build
dotnet build

# Check for syntax errors in Main.cs
```

### Runtime Errors

If functions fail at runtime:

```bash
# Enable debug output
dagger --debug call your-function

# Check container logs
dagger call your-function --help
```

## Next Steps

- See [ARCHITECTURE.md](./ARCHITECTURE.md) for SDK internals
- See [TESTING.md](./TESTING.md) for the SDK development testing workflow
- See [README.md](./README.md) for feature documentation
