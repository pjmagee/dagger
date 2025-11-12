using System.CommandLine;
using System.Reflection;

namespace DaggerEntrypoint;

/// <summary>
/// Entrypoint for Dagger C# modules.
/// This executable is called by the Dagger engine to execute module functions.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Dagger C# Module Entrypoint");

        var registerOption = new Option<bool>(
            "--register",
            description: "Register the module with Dagger"
        );
        rootCommand.AddOption(registerOption);

        rootCommand.SetHandler(async (bool register) =>
        {
            if (register)
            {
                // Register mode: introspect module and return schema
                await RegisterModule();
            }
            else
            {
                // Serve mode: listen for function calls and execute them
                await ServeModule();
            }
        }, registerOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RegisterModule()
    {
        // TODO: Implement module registration
        // This should:
        // 1. Discover all classes marked with [DaggerObject]
        // 2. Discover all methods marked with [DaggerFunction]
        // 3. Build the module schema
        // 4. Return it to the Dagger engine
        
        Console.WriteLine("Module registration not yet implemented");
        await Task.CompletedTask;
    }

    static async Task ServeModule()
    {
        // TODO: Implement module serving
        // This should:
        // 1. Load the user's module classes
        // 2. Listen for function call requests from Dagger
        // 3. Execute the requested functions
        // 4. Return results
        
        Console.WriteLine("Module serving not yet implemented");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Discovers all types in the current assembly marked with [DaggerObject]
    /// </summary>
    static IEnumerable<Type> DiscoverModuleTypes()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null)
        {
            return Enumerable.Empty<Type>();
        }

        // Find all types with DaggerObjectAttribute
        return assembly.GetTypes()
            .Where(t => t.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "DaggerObjectAttribute"));
    }
}
