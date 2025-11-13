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

        // Build module type information
        var moduleTypeInfos = new List<ModuleTypeInfo>();
        foreach (var type in moduleTypes)
        {
            var typeInfo = new ModuleTypeInfo
            {
                Name = type.Name,
                Description = GetTypeDescription(type),
                Functions = BuildFunctionInfos(type)
            };
            moduleTypeInfos.Add(typeInfo);
        }

        // Output the module schema
        // In a real implementation, this would use the Dagger API to build
        // and register the module schema. For now, we output JSON for debugging.
        var result = new
        {
            module = new
            {
                name = Environment.GetEnvironmentVariable("DAGGER_MODULE") ?? "unknown",
                types = moduleTypeInfos.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    functions = t.Functions.Select(f => new
                    {
                        name = f.Name,
                        description = f.Description,
                        returnType = f.ReturnType,
                        parameters = f.Parameters.Select(p => new
                        {
                            name = p.Name,
                            type = p.Type,
                            optional = p.IsOptional,
                            description = p.Description
                        }).ToArray()
                    }).ToArray()
                }).ToArray()
            }
        };

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        Console.WriteLine(json);

        await Task.CompletedTask;
        return 0;
    }

    private static async Task<int> ServeModuleAsync()
    {
        // TODO: Implement proper serving using Dagger API
        // This would:
        // 1. Use dag.CurrentFunctionCall() to get the function call info
        // 2. Check if there's a parent (if not, return module schema)
        // 3. Discover the target object type and method
        // 4. Instantiate the object (calling constructor if needed)
        // 5. Invoke the method with deserialized arguments
        // 6. Serialize and return the result

        Console.Error.WriteLine("Module serving mode not yet fully implemented");
        Console.Error.WriteLine("This requires integration with the Dagger API (dag.CurrentFunctionCall(), etc.)");

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
    /// Builds function information for all Dagger functions in a type.
    /// </summary>
    private static List<FunctionInfo> BuildFunctionInfos(Type type)
    {
        var functions = new List<FunctionInfo>();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var funcAttr = method.GetCustomAttributes(false)
                .FirstOrDefault(a => a.GetType().Name == "DaggerFunctionAttribute");

            if (funcAttr == null)
                continue;

            var funcInfo = new FunctionInfo
            {
                Name = ToCamelCase(method.Name),
                Description = GetMethodDescription(method),
                ReturnType = GetTypeName(method.ReturnType),
                Parameters = BuildParameterInfos(method)
            };

            functions.Add(funcInfo);
        }

        return functions;
    }

    /// <summary>
    /// Builds parameter information for a method.
    /// </summary>
    private static List<ParameterInfo> BuildParameterInfos(MethodInfo method)
    {
        var parameters = new List<ParameterInfo>();

        foreach (var param in method.GetParameters())
        {
            var paramInfo = new ParameterInfo
            {
                Name = ToCamelCase(param.Name ?? "unknown"),
                Type = GetTypeName(param.ParameterType),
                IsOptional = param.IsOptional,
                Description = null // Could be extracted from XML docs
            };

            parameters.Add(paramInfo);
        }

        return parameters;
    }

    /// <summary>
    /// Gets the description from a type's XML documentation or attributes.
    /// </summary>
    private static string? GetTypeDescription(Type type)
    {
        // Try to get description from DaggerObject attribute
        var attr = type.GetCustomAttributes(false)
            .FirstOrDefault(a => a.GetType().Name == "DaggerObjectAttribute");

        if (attr != null)
        {
            var descProp = attr.GetType().GetProperty("Description");
            if (descProp != null)
            {
                return descProp.GetValue(attr) as string;
            }
        }

        // Could also extract from XML documentation summary
        return null;
    }

    /// <summary>
    /// Gets the description from a method's XML documentation or attributes.
    /// </summary>
    private static string? GetMethodDescription(MethodInfo method)
    {
        // Try to get description from DaggerFunction attribute
        var attr = method.GetCustomAttributes(false)
            .FirstOrDefault(a => a.GetType().Name == "DaggerFunctionAttribute");

        if (attr != null)
        {
            var descProp = attr.GetType().GetProperty("Description");
            if (descProp != null)
            {
                return descProp.GetValue(attr) as string;
            }
        }

        // Could also extract from XML documentation summary
        return null;
    }

    /// <summary>
    /// Gets a user-friendly type name.
    /// </summary>
    private static string GetTypeName(Type type)
    {
        // Handle Task<T> and ValueTask<T>
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>))
            {
                return GetTypeName(type.GetGenericArguments()[0]);
            }
        }

        // Handle Task and ValueTask (void async)
        if (type == typeof(Task) || type == typeof(ValueTask))
        {
            return "Void";
        }

        // Return simple name for built-in types
        return type.Name;
    }

    /// <summary>
    /// Converts a PascalCase name to camelCase.
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
