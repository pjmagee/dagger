# Testing the C# SDK

This guide shows how to test and try out the C# SDK on this branch.

## Prerequisites

- .NET 9 SDK installed ([download here](https://dotnet.microsoft.com/download/dotnet/9.0))
- Dagger CLI installed ([install guide](https://docs.dagger.io/install))
- Git (to checkout this branch)

## Quick Start

### 1. Checkout the Branch

```bash
git clone https://github.com/pjmagee/dagger.git
cd dagger
git checkout copilot/add-csharp-sdk
```

### 2. Test the SDK Directly

You can test the SDK development module:

```bash
# Lint the C# SDK code
dagger call -m sdk/csharp/dev lint

# Format the C# SDK code
dagger call -m sdk/csharp/dev format export --path=./sdk/csharp

# Note: Testing requires an introspection.json file
# This will be generated when you initialize a module
```

### 3. Create a Test Module

Create a new Dagger module using the C# SDK:

```bash
# Create a test directory
mkdir -p /tmp/test-csharp-module
cd /tmp/test-csharp-module

# Initialize a new Dagger module with C# SDK
dagger init --sdk=csharp

# This should create:
# - Main.cs (your module with example functions)
# - Program.cs (entrypoint bootstrap)
# - DaggerModule.csproj (project file)
# - sdk/ (generated SDK DLLs)
```

### 4. Examine the Generated Module

```bash
# View the template Main.cs
cat Main.cs

# You should see a module with [DaggerObject] and [DaggerFunction] attributes
```

### 5. Call Module Functions

```bash
# Call the containerEcho function
dagger call container-echo --string-arg "Hello from C# SDK!"

# Call the grepDir function (if available)
dagger call grep-dir --directory-arg . --pattern "DaggerFunction"
```

### 6. Modify the Module

Edit `Main.cs` to add your own functions:

```csharp
[DaggerObject]
public class MyModule
{
    [DaggerFunction]
    public Container HelloWorld()
    {
        return Dagger.Dag()
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", "Hello from my C# Dagger module!" });
    }

    [DaggerFunction]
    public async Task<string> GetHostname()
    {
        return await Dagger.Dag()
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "hostname" })
            .Stdout();
    }
}
```

Then call your new functions:

```bash
dagger call hello-world stdout
dagger call get-hostname
```

## Testing the SDK Components

### Test the Runtime Module

```bash
cd /path/to/dagger/repo
dagger call -m sdk/csharp/runtime --help
```

### Test the Dev Module

```bash
# From the dagger repo root
cd sdk/csharp

# Lint the SDK
dagger call -m dev lint

# Format the SDK
dagger call -m dev format export --path=.
```

### Build the SDK Locally

```bash
cd sdk/csharp/src

# Build the SDK
dotnet build

# Build the CodeGen project
dotnet build Dagger.SDK.CodeGen/Dagger.SDK.CodeGen.csproj

# Build the main SDK project
dotnet build Dagger.SDK/Dagger.SDK.csproj
```

## Testing Module Discovery

The Module Runtime discovers functions via reflection. You can test this:

```bash
cd /tmp/test-csharp-module

# Run with --register to see module schema
dotnet run -- --register
```

This should output JSON with discovered types and functions.

## Troubleshooting

### "SDK not found" error

Make sure you're using the Dagger CLI from this branch:
```bash
dagger version
```

### .NET SDK version issues

Verify .NET 9 is installed:
```bash
dotnet --version
# Should show 9.0.x
```

### Module not building

Check the build output:
```bash
cd /tmp/test-csharp-module
dotnet build
```

### Functions not discovered

Ensure your class has `[DaggerObject]` and methods have `[DaggerFunction]`:
```csharp
[DaggerObject]  // ← Required on class
public class MyModule
{
    [DaggerFunction]  // ← Required on each function
    public Container MyFunction() { ... }
}
```

## Advanced Testing

### Test with Dagger-in-Dagger

```bash
dagger call -m sdk/csharp/dev test --introspection-json <path-to-introspection.json>
```

### Use the .dagger Module

```bash
# From the repo root
dagger call sdk csharp lint
dagger call sdk csharp test
```

## What to Look For

When testing, verify:

1. **Module Initialization**: Does `dagger init --sdk=csharp` create the template files?
2. **Code Generation**: Does the SDK generate types from introspection.json?
3. **Function Discovery**: Are `[DaggerFunction]` methods discovered correctly?
4. **Function Execution**: Do functions execute and return expected results?
5. **Type Safety**: Does the generated API provide proper IntelliSense?
6. **Async Support**: Do async functions (Task<T>) work correctly?

## Example Test Scenarios

### Scenario 1: Simple Echo

```bash
cd /tmp/test-csharp-module
dagger call container-echo --string-arg "test" stdout
```

Expected: Should output "test"

### Scenario 2: Directory Grep

```bash
dagger call grep-dir --directory-arg . --pattern "Main" 
```

Expected: Should show lines containing "Main"

### Scenario 3: Custom Module

Create a module that uses multiple Dagger types:

```csharp
[DaggerObject]
public class TestModule
{
    [DaggerFunction]
    public async Task<string> ReadFile(Directory dir, string path)
    {
        return await Dagger.Dag()
            .Directory()
            .File(path)
            .Contents();
    }

    [DaggerFunction]
    public Container BuildImage(Directory source)
    {
        return Dagger.Dag()
            .Container()
            .From("golang:1.21")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "go", "build", "." });
    }
}
```

## Feedback

If you encounter issues:
1. Check the logs: `dagger call <function> --debug`
2. Verify SDK files are generated in `sdk/` folder
3. Check that Main.cs has proper attributes
4. Ensure .NET 9 SDK is installed

For more information, see:
- [C# SDK README](../README.md)
- [Architecture Documentation](../ARCHITECTURE.md)
