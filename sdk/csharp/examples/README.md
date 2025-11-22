# C# SDK Examples

This directory contains examples demonstrating both ways to use Dagger with C#:

## Module Usage

Module usage is for creating reusable Dagger functions using the runtime code generation pattern.

### [hello-module](./hello-module/)

A complete example showing how to build, test, and publish a .NET application using Dagger modules.

**Key Features:**

- Build .NET applications
- Run tests
- Create Docker images
- Publish to container registries
- Use `[Object]` and `[Function]` attributes

**Usage:**

```bash
cd hello-module
dagger call build --source=./my-app
dagger call test --source=./my-app
dagger call build-image --source=./my-app
```

## Client Library Usage

Client library usage is for writing standalone applications that use Dagger as a library (via NuGet package).

### [standalone-client](./standalone-client/)

Examples of using Dagger.SDK as a NuGet package in standalone C# applications.

**Key Features:**

- Install via `dotnet add package Dagger.SDK`
- No attributes needed
- Direct API access via `await Dag.Container()...`
- Use in any .NET application

**Usage:**

```bash
cd standalone-client
dotnet run
```

## Comparison

| Feature | Module | Client Library |
|---------|--------|----------------|
| Installation | `dagger init --sdk=csharp` | `dotnet add package Dagger.SDK` |
| Code Pattern | `[Object]`, `[Function]` attributes | Direct API calls |
| CLI Access | `dagger call function-name` | Run as normal .NET app |
| SDK Code | Generated at runtime | Pre-generated in NuGet package |
| Use Case | Reusable CI/CD functions | Standalone applications |

## Getting Started

1. **For Module development**: Start with `hello-module` to see how to create reusable Dagger functions
2. **For Client applications**: Start with `standalone-client` to see how to use Dagger as a library

Both patterns use the same underlying Dagger API, just with different entry points and lifecycle management.
