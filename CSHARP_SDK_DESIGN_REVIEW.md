# Dagger C# SDK Design Review

**Date**: November 23, 2024  
**Reviewer**: Architecture Analysis  
**Version**: Based on sdk/csharp (experimental)

## Executive Summary

The Dagger C# SDK demonstrates a thoughtful approach to bringing Dagger's capabilities to the .NET ecosystem. The SDK follows many .NET idioms and conventions, particularly in its use of attributes, XML documentation, and async/await patterns. However, there are areas where the current attribute-heavy design could be reconsidered to provide a more idiomatic .NET experience.

**Overall Assessment**: Good foundation with room for improvement in reducing boilerplate and leveraging .NET conventions more naturally.

---

## 1. Public API Design

### 1.1 Attribute-Based Module Definition

The SDK currently requires explicit attributes for module definition:

```csharp
[Object]
public class MyModule
{
    [Function]
    public Container Echo(string message) { ... }
    
    [Field]
    public string Configuration { get; set; }
}
```

**Strengths:**
- **Explicit and clear**: Developers know exactly what is exposed to Dagger
- **IDE-friendly**: Attributes provide excellent discoverability via IntelliSense
- **Familiar pattern**: Similar to ASP.NET Core's attribute routing, Entity Framework attributes
- **Metadata-rich**: Attributes can carry additional information (Description, Deprecated, Cache policies)

**Concerns:**
- **Verbose**: Requires decorating every public member that should be exposed
- **Redundant with .NET conventions**: Public methods are already discoverable via reflection
- **Different from Python/TypeScript SDKs**: Other SDKs use more convention-based approaches

### 1.2 Current Attribute Set

The SDK provides these attributes:

1. **`[Object]`** - Marks a class as a Dagger module
2. **`[Function]`** - Marks a method as a Dagger function
3. **`[Field]`** - Marks a property as exposed
4. **`[Enum]`** - Marks an enum as a Dagger enum
5. **`[EnumValue]`** - Provides metadata for enum values
6. **`[DefaultPath]`** - Specifies default path for Directory/File parameters
7. **`[Ignore]`** - Specifies file patterns to ignore for Directory parameters

**Assessment**: Comprehensive but potentially over-specified.

---

## 2. .NET Idioms & Conventions

### 2.1 What's Done Well

#### ✅ Async/Await Support
```csharp
[Function]
public async Task<string> Build(Directory source)
{
    return await Dag.Container()
        .From("golang:1.21")
        .WithDirectory("/src", source)
        .StdoutAsync();
}
```
- Proper support for `Task<T>`, `ValueTask<T>`, and `Task`/`ValueTask`
- Natural async/await patterns match .NET conventions

#### ✅ XML Documentation Integration
```csharp
/// <summary>
/// Builds a .NET application and returns the compiled binary.
/// </summary>
/// <param name="source">Source directory containing the project</param>
[Function]
public async Task<File> Build(Directory source) { ... }
```
- Leverages standard XML documentation comments
- Falls back to XML docs when Description property not set
- Encourages good documentation practices

#### ✅ Nullable Reference Types
```csharp
[Function]
public Container BuildImage(Directory source, string? tag = null)
```
- Proper use of nullable reference types (`string?`)
- Optional parameters with default values
- Type system accurately reflects nullability

#### ✅ Naming Conventions
- PascalCase for methods and properties (C# convention)
- Automatic camelCase conversion for GraphQL API
- `ToCamelCase()` utility properly handles conversion

#### ✅ Exception Handling
```csharp
public class DaggerException : Exception
{
    public DaggerException(string message) : base(message) { }
    public DaggerException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```
- Custom exception types following .NET patterns
- Proper exception constructors

### 2.2 Areas That Could Be More Idiomatic

#### ⚠️ Attribute Requirement vs. Convention Over Configuration

**Current Approach:**
```csharp
[Object]
public class MyModule
{
    [Function]
    public Container Build() { ... }
    
    [Function]  // Required even though it's public
    public Container Test() { ... }
}
```

**More Idiomatic Approach:**
```csharp
// Option 1: Interface-based (like ASP.NET Core minimal APIs)
public class MyModule : IDaggerModule
{
    // All public methods are automatically exposed
    public Container Build() { ... }
    public Container Test() { ... }
    
    // Explicit opt-out if needed
    [Ignore]
    private Container InternalHelper() { ... }
}

// Option 2: Convention-based (like Python SDK)
public class MyModule  // No attribute needed
{
    // Public async methods returning Dagger types = functions
    public async Task<Container> Build() { ... }
    
    // Properties with getters = fields
    public string Configuration { get; set; }
}
```

#### ⚠️ Static `Dag` Access

**Current:**
```csharp
Dag.Container()  // Static access (from generated code)
```

**More Idiomatic:**
```csharp
// Dependency injection pattern (ASP.NET Core style)
public class MyModule
{
    private readonly IDaggerClient _client;
    
    public MyModule(IDaggerClient client)
    {
        _client = client;
    }
    
    public Container Build() => _client.Container();
}
```

This would:
- Follow .NET DI patterns
- Make testing easier (mock injection)
- Be more familiar to .NET developers

---

## 3. Comparison with Other SDKs

### Python SDK Approach

```python
import dagger
from dagger import function, object_type

@object_type
class MyModule:
    @function
    def build(self, source: dagger.Directory) -> dagger.Container:
        return dag.container().from_("golang:1.21")
```

**Key Differences:**
- Uses decorators (Python's equivalent of attributes) - similar pattern
- More explicit decorator usage than C# attributes
- Convention: methods decorated with `@function` are exposed

### TypeScript SDK Approach

```typescript
@object()
class MyModule {
  @func()
  async build(source: Directory): Promise<Container> {
    return dag.container().from("golang:1.21")
  }
}
```

**Key Differences:**
- Also uses decorators (TypeScript's equivalent)
- Very similar to C# approach
- Type inference helps reduce boilerplate

### Go SDK Approach

```go
type MyModule struct{}

// Build builds the project
func (m *MyModule) Build(source *Directory) *Container {
    return dag.Container().From("golang:1.21")
}
```

**Key Differences:**
- No attributes/decorators needed
- Pure convention: exported (capitalized) methods = functions
- Relies on comments for documentation
- Most "minimal" approach

---

## 4. Analyzers and Developer Experience

### 4.1 Roslyn Analyzers

The SDK includes excellent Roslyn analyzers that provide:

**DAGGER001**: Suggests `[Function]` for public methods in `[Object]` classes
```
Public method 'Echo' in class marked with [Object] should be marked with 
[Function] attribute to be exposed as a Dagger function
```

**DAGGER002-003**: Encourages XML documentation
```
Function 'Build' should have XML documentation comments
Parameter 'source' should have XML documentation
```

**DAGGER004-005**: Suggests helpful attributes
```
Directory parameter 'source' might benefit from [DefaultPath] attribute
Directory parameter 'source' might benefit from [Ignore] attribute
```

**Assessment**: These analyzers are **excellent**. They:
- Guide developers toward best practices
- Are informational (not errors) - don't force compliance
- Provide helpful context in the IDE
- Could be enhanced if attributes become optional

### 4.2 CodeFixes

The SDK includes code fixes for automatic application of attributes and documentation. This is a **strong positive** - reduces friction even with attribute-heavy design.

---

## 5. Specific Design Decisions

### 5.1 `[Object]` Attribute

**Current Requirement**: Every module class needs `[Object]`

**Rationale**: 
- Explicit marker for Dagger modules
- Allows non-module classes in same assembly
- Carries metadata (Name, Description, Deprecated)

**Alternative Considerations**:

```csharp
// Option 1: Base class
public class MyModule : DaggerModule
{
    // Inheriting from DaggerModule identifies this as a module
}

// Option 2: Interface
public class MyModule : IDaggerModule
{
    // Implementing IDaggerModule identifies this as a module
}

// Option 3: Naming convention
public class MyModule  // Classes ending in "Module" are auto-discovered
{
    // Convention-based discovery
}
```

**Recommendation**: Keep `[Object]` but make it **optional** with fallback to convention:
- If `[Object]` is present, use it
- Otherwise, check for base class/interface
- Fall back to naming pattern (class ends with "Module" or is in entry assembly)

### 5.2 `[Function]` Attribute

**Current Requirement**: Every public method needs `[Function]`

**Rationale**:
- Explicit opt-in prevents accidental exposure
- Carries metadata (Name, Description, Cache, Deprecated)
- Clear visual indicator

**Issues**:
1. **Redundant with public modifier**: In .NET, `public` already means "intentionally exposed"
2. **Verbose**: Every method needs decoration
3. **Different from Go SDK**: Go uses simple convention (exported = public)
4. **Analyzer suggests it anyway**: DAGGER001 recommends adding it to all public methods

**Alternative Design**:

```csharp
public class MyModule
{
    // Public methods are automatically functions
    public Container Build(Directory source) { ... }
    
    // Use [Ignore] or [NonFunction] to opt-out
    [Ignore]
    public void InternalHelper() { ... }
    
    // Use [Function] ONLY when you need metadata
    [Function(Cache = "5m", Deprecated = "Use BuildV2 instead")]
    public Container BuildV1(Directory source) { ... }
}
```

**Benefits**:
- Less boilerplate for common case
- Attribute only needed for metadata
- Follows "convention over configuration"
- More like Go SDK (which .NET developers often appreciate for simplicity)

### 5.3 `[Field]` Attribute

**Current Requirement**: Properties need `[Field]` to be exposed

**Similar Concerns**:
- Public properties are already discoverable
- Could use convention: auto-expose public properties
- Attribute only needed for metadata

**Recommendation**: Make optional, auto-expose public properties

### 5.4 `[DefaultPath]` and `[Ignore]` Attributes

**Assessment**: These are **well-designed** and should remain:

```csharp
public async Task<string> Build(
    [DefaultPath(".")]
    [Ignore("node_modules", ".git", "**/*.log")]
    Directory source)
```

**Strengths**:
- Parameter-specific metadata
- No convention would work here
- Clear and explicit
- Type-safe (attributes verify at compile-time)
- Similar to ASP.NET Core's `[FromRoute]`, `[FromBody]`, etc.

**These attributes add real value and have no obvious convention-based alternative.**

---

## 6. Code Generation Quality

### 6.1 Source Generator

The SDK uses a Source Generator to create the Dagger API from introspection schema:

**Strengths**:
- Type-safe API at compile-time
- IntelliSense support for all Dagger types
- Proper async/await support
- No runtime reflection for client API

**Example Generated Code Quality**:
```csharp
public Container Container() => 
    new Container(QueryBuilder.Select("container"), GraphQLClient);

public Container WithExec(string[] args) => 
    new Container(QueryBuilder.Select("withExec")
        .WithArg("args", args), GraphQLClient);
```

**Assessment**: High-quality, idiomatic C# code generation.

### 6.2 Module Runtime

The module runtime uses reflection to discover and invoke functions:

**Strengths**:
- Proper async/await unwrapping
- Handles `Task<T>`, `ValueTask<T>`, `Task`, `ValueTask`
- XML documentation extraction
- Type conversion (JSON ↔ C# types)
- Error handling and reporting

**Concerns**:
- Heavy reliance on attributes for discovery
- Could leverage more conventions

---

## 7. Potential Issues & Improvements

### 7.1 Current Issues

#### Issue 1: Attribute Verbosity
**Problem**: Every public member needs an attribute
**Impact**: High ceremony, repetitive code
**Severity**: Medium - Not a blocker but reduces DX

#### Issue 2: Inconsistent with .NET Conventions
**Problem**: `public` modifier should be sufficient for exposure
**Impact**: Confusing for .NET developers used to `public` = exposed
**Severity**: Low-Medium

#### Issue 3: Different from Other Language SDKs
**Problem**: Each SDK has different attribute requirements
**Impact**: Inconsistent cross-language experience
**Severity**: Low - Each language has idioms

### 7.2 Proposed Improvements

#### Improvement 1: Convention Over Configuration

**Proposal**: Make attributes optional with sensible defaults

```csharp
// Current (Required)
[Object]
public class MyModule
{
    [Function]
    public Container Build() { ... }
}

// Proposed (Optional)
public class MyModule  // Auto-detected as module
{
    public Container Build() { ... }  // Auto-exposed as function
    
    // Attribute only when you need metadata
    [Function(Cache = "5m")]
    public Container BuildCached() { ... }
}
```

**Implementation**:
1. Check for `[Object]` attribute first
2. If not found, check if class implements `IDaggerModule` interface
3. If not found, check naming convention (ends with "Module")
4. If not found, check if it's a class in entry assembly with public methods returning Dagger types

Similar for functions:
1. Check for `[Function]` attribute
2. If not found and method is public, auto-expose
3. Use `[Ignore]` to opt-out

**Benefits**:
- Reduced boilerplate (80% reduction in attributes for typical module)
- More .NET-idiomatic
- Backward compatible (attributes still work)
- Metadata still available when needed

#### Improvement 2: Dependency Injection for Client Access

**Current**:
```csharp
Dag.Container()  // Static global
```

**Proposed**:
```csharp
public class MyModule
{
    private readonly IDaggerClient _client;
    
    // Constructor injection (optional - falls back to static if not used)
    public MyModule(IDaggerClient client)
    {
        _client = client;
    }
    
    public Container Build() => _client.Container();
}
```

**Benefits**:
- Testable (can mock client)
- Familiar to ASP.NET Core developers
- Still allows static `Dag` for simplicity

#### Improvement 3: Enhanced Analyzer Guidance

If attributes become optional, update analyzers:

**DAGGER001 (New)**: 
```
Public method 'Helper' in module 'MyModule' will be automatically exposed 
as a Dagger function. Use [Ignore] if this is unintended.
```

**Benefits**:
- Helps developers understand convention
- Prevents accidental exposure
- Maintains safety of explicit attributes

#### Improvement 4: Fluent Builder for Metadata

**Current**:
```csharp
[Function(Name = "customName", Description = "...", Cache = "5m", Deprecated = "...")]
```

**Alternative**:
```csharp
public Container Build() { ... }

// Metadata via fluent API (if needed)
static void ConfigureModule(IModuleBuilder builder)
{
    builder.Function(nameof(Build))
        .WithName("customName")
        .WithDescription("...")
        .WithCache("5m")
        .Deprecated("...");
}
```

**Assessment**: Probably not worth it - attributes work well for this use case.

---

## 8. Comparison Matrix

| Aspect | Current Design | Proposed Improvement | Trade-offs |
|--------|---------------|---------------------|------------|
| **Module Class** | `[Object]` required | Optional via convention | More flexible but requires clear documentation |
| **Functions** | `[Function]` required | Optional for public methods | Less explicit but more concise |
| **Fields** | `[Field]` required | Optional for public properties | Consistent with functions |
| **Parameter Attributes** | `[DefaultPath]`, `[Ignore]` | Keep as-is | These add real value |
| **Client Access** | Static `Dag` | DI-friendly + static | More options, slightly more complex |
| **Analyzers** | Suggest attributes | Warn about auto-exposure | Maintains safety |

---

## 9. Security Considerations

### Current Design (Explicit)
- ✅ Requires explicit opt-in via attributes
- ✅ Prevents accidental exposure
- ✅ Clear visual indicator of exposed API
- ❌ Can lead to copy-paste errors (forgetting attributes)

### Proposed Design (Convention)
- ✅ Natural .NET patterns reduce errors
- ✅ Analyzers warn about auto-exposure
- ⚠️ Requires developers to understand conventions
- ⚠️ May accidentally expose public methods

**Mitigation**: Strong analyzer warnings + documentation

---

## 10. Real-World Usage Examples

### Example 1: Simple Module (Current)

```csharp
[Object]
public class HelloModule
{
    [Function]
    public Container Echo(string message)
    {
        return Dag.Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", message });
    }
}
```

**Lines**: 10
**Attributes**: 2

### Example 1: Simple Module (Proposed)

```csharp
public class HelloModule
{
    public Container Echo(string message)
    {
        return Dag.Container()
            .From("alpine:latest")
            .WithExec(new[] { "echo", message });
    }
}
```

**Lines**: 8
**Attributes**: 0
**Reduction**: 20% fewer lines, 100% fewer attributes

### Example 2: Complex Module (Current)

```csharp
/// <summary>
/// A CI/CD module for building and testing applications.
/// </summary>
[Object]
public class CiModule
{
    /// <summary>
    /// Build configuration
    /// </summary>
    [Field]
    public string? Configuration { get; set; }

    /// <summary>
    /// Builds the application
    /// </summary>
    /// <param name="source">Source directory</param>
    [Function]
    public async Task<File> Build(
        [DefaultPath(".")]
        [Ignore("node_modules", ".git")]
        Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:8.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "build", "-c", Configuration ?? "Release" })
            .File("/src/bin/Release/app.dll");
    }

    /// <summary>
    /// Runs tests
    /// </summary>
    /// <param name="source">Source directory</param>
    [Function]
    public async Task<string> Test(
        [DefaultPath(".")]
        Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:8.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "test" })
            .StdoutAsync();
    }
}
```

### Example 2: Complex Module (Proposed)

```csharp
/// <summary>
/// A CI/CD module for building and testing applications.
/// </summary>
public class CiModule
{
    /// <summary>
    /// Build configuration
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Builds the application
    /// </summary>
    /// <param name="source">Source directory</param>
    public async Task<File> Build(
        [DefaultPath(".")]
        [Ignore("node_modules", ".git")]
        Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:8.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "build", "-c", Configuration ?? "Release" })
            .File("/src/bin/Release/app.dll");
    }

    /// <summary>
    /// Runs tests
    /// </summary>
    /// <param name="source">Source directory</param>
    public async Task<string> Test(
        [DefaultPath(".")]
        Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:8.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "test" })
            .StdoutAsync();
    }
}
```

**Difference**: Removed `[Object]`, `[Field]`, `[Function]` attributes (3 attributes)
**Keep**: Parameter attributes like `[DefaultPath]`, `[Ignore]` (provide real value)

---

## 11. Recommendations

### High Priority (Should Implement)

1. **Make `[Object]` optional**
   - Auto-detect classes in entry assembly
   - Support optional base class/interface
   - Keep attribute for explicit cases

2. **Make `[Function]` optional for public methods**
   - Auto-expose public methods returning Dagger types
   - Add `[Ignore]` attribute for opt-out
   - Keep `[Function]` for metadata (Cache, Deprecated, etc.)

3. **Make `[Field]` optional for public properties**
   - Auto-expose public get/set properties
   - Keep attribute for metadata

4. **Update Analyzers**
   - Warn when public methods will be auto-exposed
   - Suggest `[Ignore]` if exposure is unintended
   - Keep existing documentation warnings

### Medium Priority (Consider)

5. **Add Dependency Injection support**
   - Allow constructor injection of `IDaggerClient`
   - Maintain backward compatibility with static `Dag`
   - Better testing story

6. **Interface-based module definition**
   - Create `IDaggerModule` interface
   - Allows for better tooling and discovery

### Low Priority (Nice to Have)

7. **Convention-based naming**
   - Classes ending in "Module" auto-detected
   - Methods starting with "Build", "Test", etc. auto-recognized
   - May be too magical for .NET

---

## 12. Migration Path

If these changes are implemented:

### Backward Compatibility

✅ **Fully Backward Compatible**
- All existing code with attributes continues to work
- No breaking changes
- Attributes take precedence over conventions

### Migration Guide

```csharp
// Old code (still works)
[Object]
public class MyModule
{
    [Function]
    public Container Build() { ... }
}

// New code (also works)
public class MyModule
{
    public Container Build() { ... }
}

// Mixed (also works)
public class MyModule
{
    // Auto-exposed
    public Container Build() { ... }
    
    // Explicit metadata
    [Function(Cache = "5m")]
    public Container BuildCached() { ... }
    
    // Opt-out
    [Ignore]
    public void Helper() { ... }
}
```

---

## 13. Final Verdict

### What's Great

✅ **Excellent fundamentals**
- Strong type safety
- Good async/await support
- Proper error handling
- High-quality code generation
- Comprehensive analyzers

✅ **Good .NET citizenship**
- XML documentation integration
- Nullable reference types
- Standard exception patterns
- Natural naming conventions

✅ **Developer experience**
- IntelliSense support
- Code fixes
- Clear error messages
- Good documentation

### What Could Be Better

⚠️ **Reduce ceremony**
- Too many required attributes
- Verbose for simple cases
- Not following "convention over configuration"

⚠️ **More idiomatic .NET**
- Leverage public modifier more
- Consider dependency injection
- Use conventions where sensible

⚠️ **Consistency**
- Different approaches across language SDKs
- Could learn from Go SDK's simplicity

### Overall Score: **8/10**

The C# SDK is **very good** with a solid foundation. The main area for improvement is reducing the attribute requirements to provide a more streamlined, convention-based experience while maintaining the safety and explicitness that attributes provide when needed.

### Key Recommendation

**Implement "Convention Over Configuration" while keeping attributes for metadata:**
- Make `[Object]`, `[Function]`, and `[Field]` optional
- Auto-expose public members by default
- Use attributes only when metadata is needed
- Provide strong analyzer warnings to prevent accidental exposure
- Maintain 100% backward compatibility

This would bring the C# SDK more in line with idiomatic .NET development while preserving all the benefits of the current design.

---

## Appendix: Survey of .NET Attribute Usage Patterns

### Minimal Attributes (Good Examples)

**xUnit**:
```csharp
public class MyTests
{
    [Fact]  // Only when needed
    public void TestMethod() { }
}
```

**ASP.NET Core Minimal APIs**:
```csharp
app.MapGet("/", () => "Hello");  // No attributes needed
```

### Heavy Attributes (Anti-pattern Warning)

**WCF (Legacy)**:
```csharp
[ServiceContract]
public interface IService
{
    [OperationContract]
    string GetData();
}
```
- Generally considered too verbose
- Modern approaches use conventions

### Balanced Approach (Good Examples)

**ASP.NET Core MVC**:
```csharp
public class HomeController : Controller  // Base class provides convention
{
    public IActionResult Index() => View();  // Auto-discovered
    
    [HttpPost]  // Attribute only for specific metadata
    public IActionResult Create() => View();
}
```

**Recommendation**: Follow the ASP.NET Core MVC pattern - convention-based with attributes for metadata.

---

## Document Version

- **Version**: 1.0
- **Last Updated**: November 23, 2024
- **Next Review**: When SDK exits experimental phase
