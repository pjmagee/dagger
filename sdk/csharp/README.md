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

Create a `Program.cs` file:

```csharp
using Dagger.SDK;

// Connect to Dagger engine
var client = await Client.ConnectAsync();

// TODO: Add example usage when SDK is implemented
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
