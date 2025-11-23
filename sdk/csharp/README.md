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

The Dagger C# SDK provides **two distinct but complementary ways** to work with Dagger:

### 1. 🔧 Module Runtime (Source-based Distribution)

Create reusable Dagger modules that can be called by others:

```bash
dagger init --sdk=csharp --name=my-module
```

This generates a module where you write functions decorated with `[DaggerObject]` and `[DaggerFunction]`. The SDK source code is generated directly into your module for version consistency.

**Use this for:** Creating reusable CI/CD functions, building shareable modules, extending Dagger's capabilities

### 2. 📦 Client Library (NuGet Package)

Use Dagger programmatically in any .NET application:

```bash
dotnet add package Dagger.SDK
```

Import the SDK and call the Dagger API directly to build custom pipelines.

**Use this for:** Custom CI/CD scripts, build automation, one-off pipeline tasks

## Documentation

- **[LOCAL_TESTING.md](./LOCAL_TESTING.md)** - How to test the SDK locally and explore generated code
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - SDK architecture and internals
- **[TESTING.md](./TESTING.md)** - Development and testing workflow
- **[examples/standalone-client/](./examples/standalone-client/)** - Using Dagger.SDK as a client library

## Requirements

- .NET 9.0 or later
- [Docker](https://docs.docker.com/engine/install/), or another OCI-compatible container runtime
- [Dagger CLI](https://docs.dagger.io/cli) v0.19.0 or later

---

## 🔧 Usage as a Module Runtime (Source-based)

### How It Works

When you run `dagger init --sdk=csharp`, the runtime module generates the Dagger SDK code directly in your module:

1. The SDK source code is generated into your module's `sdk/` directory
2. You write functions decorated with `[DaggerObject]` and `[DaggerFunction]`
3. The Dagger engine loads and executes your module

This source-based approach ensures version consistency and eliminates dependency conflicts.

### Example Module

```csharp
using Dagger;

[Object]
public class MyModule
{
    /// <summary>
    /// Returns a container that echoes a message
    /// </summary>
    [Function]
    public Container Echo(string message)
    {
        return Dag
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", message });
    }

    /// <summary>
    /// Builds and tests a Go project
    /// </summary>
    [Function]
    public async Task<string> BuildAndTest(Directory source)
    {
        return await Dag
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

---

## 📦 Usage as a Client Library (NuGet)

---

## 📦 Usage as a Client Library (NuGet)

### Installation

Add the Dagger.SDK NuGet package to your project:

```bash
dotnet add package Dagger.SDK
```

### Example: Standalone Client

```csharp
using Dagger;

// Build a custom pipeline programmatically
var result = await Dag
    .Container()
    .From("alpine:latest")
    .WithExec(new[] { "echo", "Hello from Dagger!" })
    .Stdout();

Console.WriteLine(result);
```

### Running Your Client App

Use `dagger run` to ensure a Dagger session is available:

```bash
dagger run dotnet run
```

See the [standalone-client example](./examples/standalone-client/) for more detailed usage patterns.

---

## Module Runtime Internals

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
