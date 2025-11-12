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

### Using the SDK directly

```csharp
using Dagger.SDK;

// Connect to Dagger engine
var result = await Dagger.Dag()
    .Container()
    .From("alpine:latest")
    .WithExec(new[] { "echo", "Hello from Dagger!" })
    .Stdout();

Console.WriteLine(result);
```

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
