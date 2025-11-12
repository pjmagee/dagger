using System.Reflection;
using System.Text.Json;

namespace Dagger.SDK.Module;

/// <summary>
/// Runtime for Dagger C# modules.
/// Handles module discovery, registration, and function execution.
/// </summary>
public static class ModuleRuntime
{
    /// <summary>
    /// Main entry point for the Dagger module runtime.
    /// </summary>
    /// <param name="args">Command line arguments from Dagger engine</param>
    public static async Task<int> RunAsync(string[] args)
    {
        bool isRegister = args.Contains("--register");

        try
        {
            if (isRegister)
            {
                // Registration mode: introspect module and return schema
                return await RegisterModuleAsync();
            }
            else
            {
                // Serve mode: listen for function calls and execute them
                return await ServeModuleAsync();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in module runtime: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<int> RegisterModuleAsync()
    {
        // Discover all types marked with [DaggerObject]
        var moduleTypes = DiscoverModuleTypes();

        if (!moduleTypes.Any())
        {
            Console.Error.WriteLine("No types marked with [DaggerObject] found");
            return 1;
        }

        // For now, just output the discovered types for debugging
        // TODO: Build actual module schema using Dagger API
        var typeNames = moduleTypes.Select(t => t.Name).ToArray();
        Console.WriteLine($"Discovered {moduleTypes.Count} module type(s): {string.Join(", ", typeNames)}");

        // Output a simple JSON response for now
        var result = new
        {
            types = moduleTypes.Select(t => new
            {
                name = t.Name,
                functions = DiscoverDaggerFunctions(t).Select(m => new
                {
                    name = m.Name,
                    returnType = m.ReturnType.Name
                }).ToArray()
            }).ToArray()
        };

        Console.WriteLine(JsonSerializer.Serialize(result));
        await Task.CompletedTask;
        return 0;
    }

    private static async Task<int> ServeModuleAsync()
    {
        // TODO: Implement proper serving using Dagger API
        // For now, this is a placeholder that demonstrates the structure
        
        Console.WriteLine("Module serving mode");
        
        // In a real implementation, this would:
        // 1. Connect to Dagger using dag.CurrentFunctionCall()
        // 2. Get the parent name to determine if this is a function call
        // 3. Discover the target object and method
        // 4. Instantiate the object
        // 5. Invoke the method with arguments
        // 6. Return the result
        
        await Task.CompletedTask;
        return 0;
    }

    /// <summary>
    /// Discovers all types in the entry assembly marked with [DaggerObject].
    /// </summary>
    private static List<Type> DiscoverModuleTypes()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly == null)
        {
            return new List<Type>();
        }

        return assembly.GetTypes()
            .Where(t => t.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "DaggerObjectAttribute"))
            .ToList();
    }

    /// <summary>
    /// Discovers all methods in a type marked with [DaggerFunction].
    /// </summary>
    private static List<MethodInfo> DiscoverDaggerFunctions(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "DaggerFunctionAttribute"))
            .ToList();
    }
}
