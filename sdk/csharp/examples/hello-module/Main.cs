using Dagger;

/// <summary>
/// A simple Dagger module demonstrating basic CI/CD operations.
/// </summary>
[Object]
public class HelloModule
{
    /// <summary>
    /// Builds a .NET application and returns the compiled binary.
    /// </summary>
    [Function]
    public async Task<File> Build(Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:10.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "restore" })
            .WithExec(new[] { "dotnet", "build", "--no-restore", "-c", "Release" })
            .WithExec(new[] { "dotnet", "publish", "--no-build", "-c", "Release", "-o", "/app" })
            .File("/app/MyApp.dll");
    }

    /// <summary>
    /// Runs tests for a .NET project.
    /// </summary>
    [Function]
    public async Task<string> Test(Directory source)
    {
        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:10.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "restore" })
            .WithExec(new[] { "dotnet", "test", "--no-restore" })
            .StdoutAsync();
    }

    /// <summary>
    /// Builds a production Docker image for a .NET application.
    /// </summary>
    [Function]
    public Container BuildImage(Directory source, string? tag = null)
    {
        var baseTag = tag ?? "latest";

        return Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:10.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(new[] { "dotnet", "publish", "-c", "Release", "-o", "/app" })
            .From("mcr.microsoft.com/dotnet/runtime:10.0")
            .WithDirectory(
                "/app",
                Dag.Directory()
                    .WithDirectory(
                        ".",
                        Dag.Container()
                            .From("mcr.microsoft.com/dotnet/sdk:10.0")
                            .WithDirectory("/src", source)
                            .WithWorkdir("/src")
                            .WithExec(new[] { "dotnet", "publish", "-c", "Release", "-o", "/app" })
                            .Directory("/app")
                    )
            )
            .WithWorkdir("/app")
            .WithEntrypoint(new[] { "dotnet", "MyApp.dll" });
    }

    /// <summary>
    /// Publishes a container image to a registry.
    /// </summary>
    [Function]
    public async Task<string> Publish(
        Directory source,
        string registry,
        string? tag = null,
        Secret? registryPassword = null
    )
    {
        var container = BuildImage(source, tag);

        if (registryPassword != null)
        {
            container = container.WithRegistryAuth(registry, "default", registryPassword);
        }

        var address = $"{registry}/myapp:{tag ?? "latest"}";
        return await container.PublishAsync(address);
    }

    /// <summary>
    /// Runs the application locally for testing.
    /// </summary>
    [Function]
    public async Task<string> Run(Directory source, string[]? args = null)
    {
        var runArgs = new List<string> { "dotnet", "run" };
        if (args != null)
        {
            runArgs.Add("--");
            runArgs.AddRange(args);
        }

        return await Dag.Container()
            .From("mcr.microsoft.com/dotnet/sdk:10.0")
            .WithDirectory("/src", source)
            .WithWorkdir("/src")
            .WithExec(runArgs.ToArray())
            .StdoutAsync();
    }
}
