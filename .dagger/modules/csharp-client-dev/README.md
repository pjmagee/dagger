# C# SDK Dev Module

This Dagger module provides development utilities for building, testing, and
maintaining the Dagger C# SDK.

## Available commands

```bash
# Generate client code from engine introspection
dagger call -m csharp-client generate export --path=.

# Run SDK tests
dagger call -m csharp-client test

# Check formatting
dagger call -m csharp-client lint

# Auto-format C# sources
dagger call -m csharp-client format export --path=.

# Create a NuGet package
dagger call -m csharp-client pack export --path=./packages
```
