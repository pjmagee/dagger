# Dagger C# SDK

> **Warning** This SDK is experimental. Please do not use it for anything
> mission-critical. Possible issues include:

- Missing features
- Stability issues
- Performance issues
- Lack of polish
- Upcoming breaking changes
- Incomplete or out-of-date documentation

A client package for running [Dagger](https://dagger.io/) pipelines in C# / .NET.

## What is the Dagger C# SDK?

The Dagger C# SDK contains everything you need to develop CI/CD pipelines in C#, and run them on any OCI-compatible container runtime.

## Requirements

- .NET 9.0 or later
- [Docker](https://docs.docker.com/engine/install/), or another OCI-compatible container runtime

A compatible version of the [Dagger CLI](https://docs.dagger.io/cli) is automatically downloaded and run by the SDK for you, although it's possible to manage it manually.

## Installation

From [NuGet](https://www.nuget.org/), using the .NET CLI:

```shell
dotnet add package Dagger.SDK
```

## Example

### Using Dagger Functions

Create a module with Dagger functions:

```csharp
using Dagger.SDK;

[DaggerObject]
public class MyModule
{
    /// <summary>
    /// Returns a container that echoes a message
    /// </summary>
    [DaggerFunction]
    public Container Echo(string message)
    {
        return Dagger.Dag()
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", message });
    }

    /// <summary>
    /// Builds and tests a Go project
    /// </summary>
    [DaggerFunction]
    public async Task<string> BuildAndTest(Directory source)
    {
        return await Dagger.Dag()
            .Container()
            .From("golang:1.21")
            .WithMountedDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "go", "build", "./..." })
            .WithExec(new[] { "go", "test", "./..." })
            .Stdout();
    }
}
```

The Dagger engine executes your module via `Program.cs`, which bootstraps the `ModuleRuntime` from the SDK.

### Using the SDK directly

```csharp
using Dagger.SDK;

// Connect to Dagger engine and use the API
var result = await Dagger.Dag()
    .Container()
    .From("alpine:latest")
    .WithExec(new[] { "echo", "Hello from Dagger!" })
    .Stdout();

Console.WriteLine(result);
```

## How It Works

When you create a Dagger module with `dagger init --sdk=csharp`, the runtime:

1. **Builds the Dagger SDK** with your module's introspection schema
2. **Copies SDK DLLs** to your module's `sdk/` folder
3. **Initializes your module** (if new) with template files:
   - `Main.cs` - Your module class with Dagger functions
   - `Program.cs` - Entrypoint that bootstraps the SDK runtime
   - `DaggerModule.csproj` - Project file referencing the SDK

4. **Execution flow**:
   ```
   Dagger Engine → dotnet run (Program.cs) → ModuleRuntime (from SDK) → Your module functions
   ```

The **Program.cs** is a simple bootstrap:
```csharp
using Dagger.SDK.Module;
return await ModuleRuntime.RunAsync(args);
```

The **ModuleRuntime** (in the SDK) handles:
- Discovering classes marked with `[DaggerObject]`
- Discovering methods marked with `[DaggerFunction]`
- Registration (`--register` flag): Returns module schema
- Execution: Calls your functions when invoked

## Client API

The Dagger client API is generated at build time by the Source Generator from `introspection.json`. 
The generated code provides type-safe access to all Dagger API types and methods.

You access the API through the static `Dagger.Dag()` method (defined in the generated code):
```csharp
var container = Dagger.Dag()
    .Container()
    .From("alpine:latest")
    .WithExec(new[] { "echo", "hello" });
```

The Source Generator (`Dagger.SDK.CodeGen`) reads the GraphQL schema and generates:
- All Dagger types (Container, Directory, File, etc.)
- Query methods
- Type-safe method chaining
- Proper async/await support

## Learn more

- [Documentation](https://docs.dagger.io/sdk/csharp)
- [Source code](https://github.com/dagger/dagger/tree/main/sdk/csharp)

## Development

The SDK is managed with a Dagger module in `./dev`. To see which tasks are
available run:

```shell
dagger call -m dev
```

### Common tasks

Run tests:

```shell
dagger call -m dev test --introspection-json=<path>
```

Check for linting violations:
```shell
dagger call -m dev lint
```

Re-format code:
```shell
dagger call -m dev format export --path=.
```
