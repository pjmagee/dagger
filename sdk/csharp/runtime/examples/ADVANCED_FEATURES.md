# Advanced C# SDK Features

This document demonstrates the advanced features available in the C# SDK for Dagger.

## Table of Contents

1. [Cache Policies](#cache-policies)
2. [Enumerations](#enumerations)
3. [Custom Fields](#custom-fields)
4. [Constructors with Parameters](#constructors-with-parameters)
5. [Deprecation](#deprecation)
6. [Error Handling](#error-handling)

## Cache Policies

Control function caching behavior using the `Cache` property on `[DaggerFunction]`:

```csharp
// No caching - always executes
[DaggerFunction(Cache = "never")]
public Container GetFreshContainer()
{
    return Dag.Container().From("alpine:latest");
}

// Session-based caching - cached per Dagger session
[DaggerFunction(Cache = "session")]
public Container GetSessionContainer()
{
    return Dag.Container().From("ubuntu:latest");
}

// Duration-based caching - cached for specified time
[DaggerFunction(Cache = "5m")]  // 5 minutes
public Container GetCachedContainer()
{
    return Dag.Container().From("debian:latest");
}

[DaggerFunction(Cache = "1h")]  // 1 hour
public Container GetLongCachedContainer()
{
    return Dag.Container().From("nginx:latest");
}
```

### Supported Cache Values

- `"never"` - Disable caching entirely
- `"session"` - Cache for the duration of the Dagger session
- Duration string (e.g., `"5m"`, `"1h"`, `"30s"`) - Cache for specified time

## Enumerations

Expose C# enums to Dagger using the `[DaggerEnum]` attribute:

```csharp
[DaggerEnum(Description = "Supported build environments")]
public enum BuildEnvironment
{
    [DaggerEnumValue(Description = "Local development environment")]
    DEVELOPMENT,

    [DaggerEnumValue(Description = "Pre-production staging")]
    STAGING,

    [DaggerEnumValue(Description = "Production deployment")]
    PRODUCTION,
}
```

### Naming Conventions

- Enum values should use `UPPER_SNAKE_CASE` for GraphQL compatibility
- Use `[DaggerEnumValue]` to add descriptions to individual values
- Enums are automatically converted between C# and GraphQL formats

### Using Enums in Functions

```csharp
[DaggerFunction(Description = "Process environment-specific logic")]
public async Task<string> ProcessEnvironment(BuildEnvironment env)
{
    switch (env)
    {
        case BuildEnvironment.DEVELOPMENT:
            return "Development mode enabled";
        case BuildEnvironment.PRODUCTION:
            return "Production optimizations active";
        default:
            return "Unknown environment";
    }
}
```

## Custom Fields

Expose object properties as Dagger fields using `[DaggerField]`:

```csharp
[DaggerObject(Description = "Configuration object")]
public class BuildConfig
{
    [DaggerField(Description = "Build target platform")]
    public string Platform { get; set; } = "linux/amd64";

    [DaggerField(Description = "Enable optimization")]
    public bool Optimize { get; set; } = true;

    [DaggerField(Description = "Build timeout in seconds")]
    public int Timeout { get; set; } = 300;

    [DaggerFunction(Description = "Apply configuration")]
    public Container Apply(Container container)
    {
        return container
            .WithEnvVariable("PLATFORM", Platform)
            .WithEnvVariable("OPTIMIZE", Optimize.ToString());
    }
}
```

### Fields vs Functions

- **Fields**: Simple properties that can be read/written (`[DaggerField]`)
- **Functions**: Methods that perform operations (`[DaggerFunction]`)
- Fields are useful for configuration, state, and simple values
- Functions are used for operations, transformations, and side effects

## Constructors with Parameters

Module constructors can accept optional parameters with default values:

```csharp
[DaggerObject(Description = "Example module")]
public class MyModule
{
    public MyModule(
        string? defaultImage = "alpine:latest",
        int timeout = 300,
        bool verbose = false)
    {
        DefaultImage = defaultImage ?? "alpine:latest";
        Timeout = timeout;
        Verbose = verbose;
    }

    [DaggerField(Description = "Default container image")]
    public string DefaultImage { get; }

    [DaggerField(Description = "Operation timeout in seconds")]
    public int Timeout { get; }

    [DaggerField(Description = "Enable verbose logging")]
    public bool Verbose { get; }
}
```

### Constructor Guidelines

- Use optional parameters with default values
- Nullable types (`string?`) allow null defaults
- Constructor parameters automatically become constructor arguments in Dagger
- Values are passed from the parent module/call context

## Deprecation

Mark objects, functions, fields, and enum values as deprecated:

### Deprecated Functions

```csharp
[DaggerFunction(Deprecated = "Use GetCachedContainer instead")]
public Container OldMethod()
{
    return Dag.Container().From("alpine:latest");
}
```

### Deprecated Objects

```csharp
[DaggerObject(Deprecated = "Use NewModule instead")]
public class LegacyModule
{
    // ...
}
```

### Deprecated Fields

```csharp
[DaggerField(Deprecated = "Use NewConfigPath instead")]
public string ConfigPath { get; set; }
```

### Deprecated Enum Values

```csharp
[DaggerEnum]
public enum Status
{
    ACTIVE,
    
    [DaggerEnumValue(Deprecated = "Use INACTIVE instead")]
    DISABLED,
    
    INACTIVE,
}
```

## Error Handling

Throw standard C# exceptions - they're automatically converted to Dagger errors:

```csharp
[DaggerFunction(Description = "Validate and process input")]
public Container ProcessInput(string input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        throw new ArgumentException("Input cannot be empty", nameof(input));
    }

    if (input.Length > 100)
    {
        throw new InvalidOperationException("Input too long (max 100 chars)");
    }

    return Dag.Container()
        .From("alpine:latest")
        .WithExec(new[] { "echo", input });
}
```

### Exception Types

- `ArgumentException` - Invalid arguments
- `InvalidOperationException` - Invalid state/operation
- `NotImplementedException` - Feature not yet implemented
- `NotSupportedException` - Unsupported operation
- Any custom exception type

## Ignoring Members

Use `[DaggerIgnore]` to exclude members from Dagger:

```csharp
public class MyModule
{
    // Exposed to Dagger
    [DaggerFunction]
    public Container PublicMethod()
    {
        return Dag.Container().From("alpine:latest");
    }

    // Hidden from Dagger
    [DaggerIgnore]
    public string InternalState { get; set; }

    // Also hidden (no attribute means private)
    private void HelperMethod()
    {
        // Internal implementation
    }
}
```

## Complete Example

See `AdvancedExample.cs` for a complete working example demonstrating all features together.

## Testing Features

To test the advanced features:

```bash
# Create a new module with the advanced example
dagger init --sdk=csharp --name=advanced-example --source=./examples

# Call functions with cache policies
dagger call get-cached-container

# Use enum parameters
dagger call process-environment --env=PRODUCTION

# Access fields
dagger call build-config platform

# Test deprecated functions (should show warning)
dagger call old-method
```
