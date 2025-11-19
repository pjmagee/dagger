using Dagger.SDK;
using static Dagger.SDK.Dagger;

namespace AdvancedFeatures;

/// <summary>
/// Advanced C# SDK feature demonstration
/// </summary>
[DaggerObject(Description = "Demonstrates advanced SDK features")]
public class AdvancedFeatures
{
    // Field exposed to Dagger
    [DaggerField(Description = "The default container image to use")]
    public string DefaultImage { get; } = "alpine:latest";

    [DaggerField(Description = "Operation timeout in seconds")]
    public int Timeout { get; } = 300;

    /// <summary>
    /// Returns a container with caching enabled for 5 minutes
    /// </summary>
    [DaggerFunction(Cache = "5m", Description = "Get a cached container for 5 minutes")]
    public Container GetCachedContainer()
    {
        return Dag.Container().From(DefaultImage);
    }

    /// <summary>
    /// Returns a container with session-based caching
    /// </summary>
    [DaggerFunction(Cache = "session", Description = "Get a container cached for the session")]
    public Container GetSessionContainer()
    {
        return Dag.Container().From("ubuntu:latest");
    }

    /// <summary>
    /// Returns a container with no caching
    /// </summary>
    [DaggerFunction(Cache = "never", Description = "Get a container with no caching")]
    public Container GetFreshContainer()
    {
        return Dag.Container().From("debian:latest");
    }

    /// <summary>
    /// Example of deprecated function
    /// </summary>
    [DaggerFunction(Deprecated = "Use GetCachedContainer instead")]
    public Container OldMethod()
    {
        return Dag.Container().From(DefaultImage);
    }

    /// <summary>
    /// Process a build environment enum
    /// </summary>
    [DaggerFunction(Description = "Echo the build environment")]
    public async Task<string> ProcessEnvironment(BuildEnvironment env)
    {
        return await Dag
            .Container()
            .From(DefaultImage)
            .WithExec(new[] { "echo", $"Environment: {env}" })
            .StdoutAsync();
    }

    /// <summary>
    /// Throws an exception to demonstrate error handling
    /// </summary>
    [DaggerFunction(Description = "Demonstrates error handling")]
    public Container FailingFunction(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        throw new InvalidOperationException($"Intentional failure: {message}");
    }
}

/// <summary>
/// Build environment enumeration
/// </summary>
[DaggerEnum(Description = "Supported build environments")]
public enum BuildEnvironment
{
    /// <summary>
    /// Development environment
    /// </summary>
    [DaggerEnumValue(Description = "Local development environment")]
    DEVELOPMENT,

    /// <summary>
    /// Staging environment
    /// </summary>
    [DaggerEnumValue(Description = "Pre-production staging")]
    STAGING,

    /// <summary>
    /// Production environment
    /// </summary>
    [DaggerEnumValue(Description = "Production deployment")]
    PRODUCTION,

    /// <summary>
    /// CI/CD environment
    /// </summary>
    [DaggerEnumValue(Description = "Continuous integration/deployment", Deprecated = "Use STAGING instead")]
    CI,
}
