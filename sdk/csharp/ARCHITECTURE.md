# C# SDK Architecture

## Module Structure

When a user creates a C# Dagger module, they get this structure:

```
my-module/
├── Main.cs                  # User's module class
├── Program.cs               # Bootstrap entrypoint (from template)
├── DaggerModule.csproj      # Project file (from template)
└── sdk/                     # Generated SDK DLLs
    ├── Dagger.SDK.dll
    └── ... (other dependencies)
```

## Execution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Dagger Engine calls:                                         │
│    dotnet run --no-build -c Release -- [--register]             │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. Program.cs (user's file, from template):                     │
│    using Dagger.SDK.Module;                                     │
│    return await ModuleRuntime.RunAsync(args);                   │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. ModuleRuntime.cs (in SDK - Dagger.SDK.Module):              │
│    - Discovers [DaggerObject] classes                           │
│    - Discovers [DaggerFunction] methods                         │
│    - If --register: Returns schema to Dagger                    │
│    - Else: Listens for function calls and executes them         │
└──────────────────────┬──────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. User's Module (Main.cs):                                     │
│    [DaggerObject]                                               │
│    public class MyModule {                                      │
│        [DaggerFunction]                                         │
│        public Container Echo(string msg) { ... }                │
│    }                                                            │
└─────────────────────────────────────────────────────────────────┘
```

## Key Files

### Template Files (Copied to User's Module)

**Program.cs** - Simple bootstrap, calls into SDK:
```csharp
using Dagger.SDK.Module;
return await ModuleRuntime.RunAsync(args);
```

**Main.cs** - User's module definition:
```csharp
[DaggerObject]
public class DaggerModule
{
    [DaggerFunction]
    public Container ContainerEcho(string stringArg) { ... }
}
```

**DaggerModule.csproj** - References the SDK:
```xml
<Reference Include="sdk/Dagger.SDK.dll" />
```

### SDK Files (Built and Copied to User's sdk/ Folder)

**ModuleRuntime.cs** (src/Dagger.SDK/Module/) - The actual runtime logic:
- Module discovery via reflection
- Schema generation for `--register`
- Function execution for normal mode

## Comparison with Other SDKs

| SDK        | User's Entrypoint | Calls Into          | SDK Runtime         |
|------------|-------------------|---------------------|---------------------|
| Python     | `runtime.py`      | `→`                 | `dagger.mod.cli.app()` |
| PHP        | `entrypoint.php`  | `→`                 | `EntrypointCommand` |
| TypeScript | (implicit)        | `→`                 | `entrypoint()`      |
| **C#**     | **`Program.cs`**  | **`→`**             | **`ModuleRuntime`** |

All SDKs follow the same pattern:
1. User gets a simple bootstrap file in their module
2. Bootstrap calls into the SDK's runtime implementation
3. SDK runtime handles discovery, registration, and execution
