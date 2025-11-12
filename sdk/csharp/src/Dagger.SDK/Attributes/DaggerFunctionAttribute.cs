namespace Dagger.SDK;

/// <summary>
/// Marks a method as a Dagger function.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class DaggerFunctionAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the function.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the function.
    /// </summary>
    public string? Description { get; set; }
}
