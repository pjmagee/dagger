# Dagger C# Module Template

This directory contains the template files for initializing a new Dagger C# module.

## Structure

When you run `dagger init --sdk=csharp`, the following structure is created:

```
your-module/
├── Main.cs              # Your module class with Dagger functions
├── Entrypoint/          # Auto-generated entrypoint executable
│   ├── Program.cs       # Entrypoint that discovers and executes functions
│   └── Entrypoint.csproj
└── sdk/                 # Generated Dagger SDK code (auto-generated)
```

## How It Works

1. **Module Definition**: You define your module in `Main.cs` using attributes:
   - `[DaggerObject]` - Marks your class as a Dagger module
   - `[DaggerFunction]` - Marks methods to expose as Dagger functions
   - `[DaggerIgnore]` - Excludes methods/properties from Dagger

2. **Entrypoint**: The `Entrypoint/` folder contains the executable that:
   - Discovers classes marked with `[DaggerObject]`
   - Discovers methods marked with `[DaggerFunction]`
   - Handles registration (schema introspection) via `--register` flag
   - Handles execution (serving function calls) in normal mode

3. **Execution Flow**:
   - Dagger engine calls: `dotnet run --project Entrypoint/Entrypoint.csproj`
   - For registration: `... -- --register` (returns module schema)
   - For execution: `...` (listens for function calls and executes them)

## Example

```csharp
[DaggerObject]
public class MyModule
{
    [DaggerFunction]
    public Container Echo(string message)
    {
        return Dagger.Dag()
            .Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", message });
    }
}
```

This module will be discovered and executed by the Dagger engine automatically.
