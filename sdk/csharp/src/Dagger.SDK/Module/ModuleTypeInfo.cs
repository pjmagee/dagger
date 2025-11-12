namespace Dagger.SDK.Module;

/// <summary>
/// Represents information about a discovered module type.
/// </summary>
internal class ModuleTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<FunctionInfo> Functions { get; set; } = new();
}

/// <summary>
/// Represents information about a discovered function.
/// </summary>
internal class FunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
}

/// <summary>
/// Represents information about a function parameter.
/// </summary>
internal class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? Description { get; set; }
}
