namespace Dagger.SDK;

/// <summary>
/// Marks a method or property to be ignored by Dagger.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class DaggerIgnoreAttribute : Attribute
{
}
