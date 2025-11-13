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
        try
        {
            await ServeModuleAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in module runtime: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task ServeModuleAsync()
    {
        // Get the current function call context from Dagger
        var fnCall = Dagger.Dag().CurrentFunctionCall();
        var parentName = await fnCall.ParentName();

        // Determine if this is registration or invocation mode
        if (string.IsNullOrEmpty(parentName))
        {
            // Registration mode: build and return module schema
            await RegisterModuleAsync(fnCall);
        }
        else
        {
            // Invocation mode: execute the requested function
            await InvokeFunction(fnCall, parentName);
        }
    }

    private static async Task RegisterModuleAsync(FunctionCall fnCall)
    {
        // Discover all types marked with [DaggerObject]
        var moduleTypes = DiscoverModuleTypes();

        if (!moduleTypes.Any())
        {
            throw new InvalidOperationException("No types marked with [DaggerObject] found");
        }

        // Get module name from environment
        var moduleName = Environment.GetEnvironmentVariable("DAGGER_MODULE") ?? "unknown";
        
        // Build the module using Dagger API
        var mod = Dagger.Dag().Module();

        // Register each object type with the module
        foreach (var type in moduleTypes)
        {
            mod = await RegisterObjectType(mod, type);
        }

        // Get the module ID and return it
        var moduleId = await mod.Id();
        await fnCall.ReturnValue(moduleId);
    }

    private static async Task<Dagger.Module> RegisterObjectType(Dagger.Module mod, Type type)
    {
        var typeName = type.Name;
        var typeDescription = GetTypeDescription(type);

        // Create a type definition for this object
        var typeDef = Dagger.Dag().TypeDef().WithObject(typeName, typeDescription ?? "");

        // Discover and register all functions in this type
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            var funcAttr = method.GetCustomAttributes(false)
                .FirstOrDefault(a => a.GetType().Name == "DaggerFunctionAttribute");

            if (funcAttr == null)
                continue;

            typeDef = await RegisterFunction(typeDef, method);
        }

        // Add the object type to the module
        return mod.WithObject(typeDef);
    }

    private static async Task<TypeDef> RegisterFunction(TypeDef typeDef, MethodInfo method)
    {
        var funcName = ToCamelCase(method.Name);
        var funcDescription = GetMethodDescription(method);
        var returnType = await GetDaggerTypeDef(method.ReturnType);

        // Create function definition
        var funcDef = Dagger.Dag().Function(funcName, returnType);
        
        if (!string.IsNullOrEmpty(funcDescription))
        {
            funcDef = funcDef.WithDescription(funcDescription);
        }

        // Add parameters
        foreach (var param in method.GetParameters())
        {
            var paramName = ToCamelCase(param.Name ?? "unknown");
            var paramType = await GetDaggerTypeDef(param.ParameterType);
            
            if (param.IsOptional)
            {
                paramType = paramType.WithOptional(true);
            }

            funcDef = funcDef.WithArg(paramName, paramType);
        }

        return typeDef.WithFunction(funcDef);
    }

    private static async Task InvokeFunction(FunctionCall fnCall, string parentName)
    {
        // Get function call information
        var fnName = await fnCall.Name();
        var parentJson = await fnCall.Parent();
        var inputArgs = await fnCall.InputArgs();

        // Discover the target object type
        var moduleTypes = DiscoverModuleTypes();
        var targetType = moduleTypes.FirstOrDefault(t => t.Name == parentName);
        
        if (targetType == null)
        {
            throw new InvalidOperationException($"Object type '{parentName}' not found");
        }

        // Deserialize parent state
        var parentState = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(parentJson))
        {
            parentState = JsonSerializer.Deserialize<Dictionary<string, object?>>(parentJson) 
                ?? new Dictionary<string, object?>();
        }

        // Find the target method
        var method = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => ToCamelCase(m.Name) == fnName && 
                               m.GetCustomAttributes(false).Any(a => a.GetType().Name == "DaggerFunctionAttribute"));

        if (method == null)
        {
            throw new InvalidOperationException($"Function '{fnName}' not found in type '{parentName}'");
        }

        // Deserialize input arguments
        var args = new Dictionary<string, object?>();
        foreach (var arg in inputArgs)
        {
            var argName = await arg.Name();
            var argValue = await arg.Value();
            args[argName] = JsonSerializer.Deserialize<object>(argValue);
        }

        // Create instance of the object
        var instance = Activator.CreateInstance(targetType);
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of '{parentName}'");
        }

        // Restore parent state (set properties/fields)
        foreach (var kvp in parentState)
        {
            var prop = targetType.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(instance, kvp.Value);
            }
        }

        // Prepare method parameters
        var parameters = method.GetParameters();
        var paramValues = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramName = ToCamelCase(parameters[i].Name ?? "");
            if (args.TryGetValue(paramName, out var value))
            {
                paramValues[i] = value;
            }
            else if (parameters[i].IsOptional)
            {
                paramValues[i] = parameters[i].DefaultValue;
            }
        }

        // Invoke the method
        var result = method.Invoke(instance, paramValues);

        // Handle async methods
        if (result is Task task)
        {
            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            result = resultProperty?.GetValue(task);
        }

        // Serialize and return the result
        var resultJson = result != null ? JsonSerializer.Serialize(result) : "null";
        await fnCall.ReturnValue(resultJson as dynamic);
    }

    private static async Task<TypeDef> GetDaggerTypeDef(Type type)
    {
        // Handle Task<T> and ValueTask<T>
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(Task<>) || genericDef == typeof(ValueTask<>))
            {
                return await GetDaggerTypeDef(type.GetGenericArguments()[0]);
            }
        }

        // Handle Task and ValueTask (void async)
        if (type == typeof(Task) || type == typeof(ValueTask))
        {
            return Dagger.Dag().TypeDef().WithKind(TypeDefKind.VoidKind);
        }

        // Handle primitive types
        if (type == typeof(string))
        {
            return Dagger.Dag().TypeDef().WithKind(TypeDefKind.StringKind);
        }
        if (type == typeof(int) || type == typeof(long))
        {
            return Dagger.Dag().TypeDef().WithKind(TypeDefKind.IntegerKind);
        }
        if (type == typeof(bool))
        {
            return Dagger.Dag().TypeDef().WithKind(TypeDefKind.BooleanKind);
        }

        // Handle Dagger types (Container, Directory, etc.)
        // These need to be referenced by name
        return Dagger.Dag().TypeDef().WithObject(type.Name);
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
