using Dagger.SDK;
using static Dagger.SDK.Dagger;

namespace DaggerModule;

/// <summary>
/// Example demonstrating advanced C# SDK features
/// </summary>
[DaggerObject(Description = "Advanced example module")]
public class AdvancedExample
{
    // Constructor with optional parameters
    public AdvancedExample(string? defaultImage = "alpine:latest")
    {
        DefaultImage = defaultImage ?? "alpine:latest";
    }

    // Field exposed to Dagger
    [DaggerField(Description = "The default container image to use")]
    public string DefaultImage { get; }

    /// <summary>
    /// Returns a container with caching enabled for 5 minutes
    /// </summary>
    [DaggerFunction(
        Cache = "5m",
        Description = "Get a cached container for 5 minutes"
    )]
    public Container GetCachedContainer()
    {
        return Dag.Container().From(DefaultImage);
    }

    /// <summary>
    /// Returns a container with session-based caching
    /// </summary>
    [DaggerFunction(
        Cache = "session",
        Description = "Get a container cached for the session"
    )]
    public Container GetSessionContainer()
    {
        return Dag.Container().From("ubuntu:latest");
    }

    /// <summary>
    /// Returns a container with no caching
    /// </summary>
    [DaggerFunction(
        Cache = "never",
        Description = "Get a container with no caching"
    )]
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

    // This property is ignored by Dagger
    [DaggerIgnore]
    public string InternalState { get; set; } = "hidden";

    // This method is ignored by Dagger
    [DaggerIgnore]
    [DaggerFunction]
    public void HelperMethod()
    {
        // Internal helper, not exposed
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
    CI
}

/// <summary>
/// Example of a custom object with fields
/// </summary>
[DaggerObject(Description = "Configuration object example")]
public class BuildConfig
{
    [DaggerField(Description = "Build target platform")]
    public string Platform { get; set; } = "linux/amd64";

    [DaggerField(Description = "Enable optimization")]
    public bool Optimize { get; set; } = true;

    [DaggerField(Description = "Build timeout in seconds")]
    public int Timeout { get; set; } = 300;

    [DaggerFunction(Description = "Apply this configuration to a container")]
    public Container Apply(Container container)
    {
        return container
            .WithEnvVariable("PLATFORM", Platform)
            .WithEnvVariable("OPTIMIZE", Optimize.ToString())
            .WithEnvVariable("TIMEOUT", Timeout.ToString());
    }
}
