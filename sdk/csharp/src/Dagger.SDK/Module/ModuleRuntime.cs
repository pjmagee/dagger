#if !DAGGER_CODEGEN_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dagger.SDK.GraphQL;
using DaggerObject = Dagger.SDK.Object;

// Renamed namespace to avoid collision with generated 'Module' type in Dagger.SDK
namespace Dagger.SDK.Runtime;

/// <summary>
/// Runtime for Dagger C# modules. Handles module discovery, registration, and invocation.
/// </summary>
public static class ModuleRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<int> RunAsync(string[] args)
    {
        Query dag;
        try
        {
            dag = Dagger.Dag;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialise Dagger client: {ex.Message}");
            return 1;
        }

        FunctionCall fnCall;
        try
        {
            fnCall = dag.CurrentFunctionCall();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to resolve current function call: {ex.Message}");
            return 1;
        }

        var moduleInfos = BuildModuleTypeInfos();
        if (moduleInfos.Count == 0)
        {
            await ReturnErrorAsync(dag, fnCall, "No types decorated with [DaggerObject] were discovered in the entry assembly.");
            return 1;
        }

        string parentName;
        try
        {
            parentName = await fnCall.ParentNameAsync();
        }
        catch (Exception ex)
        {
            await ReturnErrorAsync(dag, fnCall, ex);
            return 1;
        }

        if (string.IsNullOrEmpty(parentName))
        {
            return await HandleRegistrationAsync(dag, fnCall, moduleInfos);
        }

        return await HandleInvocationAsync(dag, fnCall, parentName, moduleInfos);
    }

    private static async Task<int> HandleRegistrationAsync(Query dag, FunctionCall fnCall, IReadOnlyCollection<ModuleTypeInfo> moduleInfos)
    {
        try
        {
            var module = dag.Module();

            // Register enums first
            var enums = BuildEnumTypeInfos();
            foreach (var enumInfo in enums)
            {
                var enumDef = dag.TypeDef().WithEnum(enumInfo.Name, enumInfo.Description);

                foreach (var valueInfo in enumInfo.Values)
                {
                    enumDef = enumDef.WithEnumMember(
                        name: valueInfo.Name,
                        value: valueInfo.Value,
                        description: valueInfo.Description,
                        deprecated: valueInfo.Deprecated
                    );
                }

                module = module.WithEnum(enumDef);
            }

            foreach (var moduleInfo in moduleInfos)
            {
                var typeDef = dag
                    .TypeDef()
                    .WithObject(moduleInfo.Name, moduleInfo.Description);

                // TODO: TypeDef.WithDeprecated not available in current Dagger version
                // Register deprecated status on object (when API becomes available)
                // if (!string.IsNullOrWhiteSpace(moduleInfo.Deprecated))
                // {
                //     typeDef = typeDef.WithDeprecated(moduleInfo.Deprecated);
                // }

                // Register constructor if present
                if (moduleInfo.Constructor != null)
                {
                    var ctorFunc = dag.Function("", typeDef); // Empty name for constructor

                    foreach (var param in moduleInfo.Constructor.GetParameters())
                    {
                        var (paramTypeDef, paramNullable) = BuildTypeDef(dag, param.ParameterType);

                        if (paramNullable || param.HasDefaultValue || param.IsOptional)
                        {
                            paramTypeDef = paramTypeDef.WithOptional(true);
                        }

                        Json? defaultJson = null;
                        if (param.HasDefaultValue)
                        {
                            var normalized = NormalizeDefaultValue(param.DefaultValue);
                            if (normalized != null)
                            {
                                defaultJson = new Json { Value = JsonSerializer.Serialize(normalized, SerializerOptions) };
                            }
                        }

                        ctorFunc = ctorFunc.WithArg(
                            ToCamelCase(param.Name ?? $"arg{param.Position}"),
                            paramTypeDef,
                            null, // description
                            defaultJson
                        );
                    }

                    typeDef = typeDef.WithConstructor(ctorFunc);
                }

                // Register fields
                foreach (var field in moduleInfo.Fields)
                {
                    var (fieldTypeDef, fieldIsNullable) = BuildTypeDef(dag, field.PropertyType);
                    if (fieldIsNullable)
                    {
                        fieldTypeDef = fieldTypeDef.WithOptional(true);
                    }

                    typeDef = typeDef.WithField(
                        name: field.Name,
                        typeDef: fieldTypeDef,
                        description: field.Description,
                        deprecated: field.Deprecated
                    );
                }

                // Register functions
                foreach (var function in moduleInfo.Functions)
                {
                    var (returnTypeDef, returnIsNullable) = BuildTypeDef(dag, function.ReturnType);
                    if (function.ReturnsVoid || returnIsNullable)
                    {
                        returnTypeDef = returnTypeDef.WithOptional(true);
                    }

                    var functionDef = dag.Function(function.Name, returnTypeDef);

                    if (!string.IsNullOrWhiteSpace(function.Description))
                    {
                        functionDef = functionDef.WithDescription(function.Description);
                    }

                    // Register cache policy
                    if (!string.IsNullOrWhiteSpace(function.CachePolicy))
                    {
                        switch (function.CachePolicy.ToLowerInvariant())
                        {
                            case "never":
                                functionDef = functionDef.WithCachePolicy(FunctionCachePolicy.Never);
                                break;
                            case "session":
                                functionDef = functionDef.WithCachePolicy(FunctionCachePolicy.PerSession);
                                break;
                            default:
                                // Duration string like "5m", "1h"
                                functionDef = functionDef.WithCachePolicy(
                                    policy: FunctionCachePolicy.Default,
                                    timeToLive: function.CachePolicy
                                );
                                break;
                        }
                    }

                    // Register deprecation
                    if (!string.IsNullOrWhiteSpace(function.Deprecated))
                    {
                        functionDef = functionDef.WithDeprecated(function.Deprecated);
                    }

                    foreach (var parameter in function.Parameters)
                    {
                        if (parameter.IsCancellationToken)
                        {
                            continue;
                        }

                        var (argumentTypeDef, argumentNullable) = BuildTypeDef(dag, parameter.ParameterType);

                        if (argumentNullable || parameter.IsOptional)
                        {
                            argumentTypeDef = argumentTypeDef.WithOptional(true);
                        }

                        Json? defaultJson = null;
                        if (parameter.Parameter.HasDefaultValue)
                        {
                            var normalizedDefault = NormalizeDefaultValue(parameter.Parameter.DefaultValue);
                            if (normalizedDefault is not null)
                            {
                                defaultJson = new Json
                                {
                                    Value = JsonSerializer.Serialize(normalizedDefault, SerializerOptions)
                                };
                            }
                        }

                        functionDef = functionDef.WithArg(
                            parameter.Name,
                            argumentTypeDef,
                            parameter.Description,
                            defaultJson
                        );
                    }

                    typeDef = typeDef.WithFunction(functionDef);
                }

                module = module.WithObject(typeDef);
            }

            var moduleId = await module.IdAsync();
            var result = new Json
            {
                Value = JsonSerializer.Serialize(moduleId.Value, SerializerOptions)
            };

            await fnCall.ReturnValueAsync(result);
            return 0;
        }
        catch (Exception ex)
        {
            await ReturnErrorAsync(dag, fnCall, ex);
            return 1;
        }
    }

    private static async Task<int> HandleInvocationAsync(Query dag, FunctionCall fnCall, string parentName, IReadOnlyCollection<ModuleTypeInfo> moduleInfos)
    {
        try
        {
            var moduleInfo = moduleInfos.FirstOrDefault(info => string.Equals(info.Name, parentName, StringComparison.Ordinal));
            if (moduleInfo is null)
            {
                await ReturnErrorAsync(dag, fnCall, $"Module object '{parentName}' is not registered.");
                return 1;
            }

            var functionName = await fnCall.NameAsync();
            var functionInfo = moduleInfo.Functions.FirstOrDefault(f => string.Equals(f.Name, functionName, StringComparison.Ordinal));
            if (functionInfo is null)
            {
                await ReturnErrorAsync(dag, fnCall, $"Function '{functionName}' not found on module object '{parentName}'.");
                return 1;
            }

            // Create instance with constructor if needed
            object? instance;
            if (moduleInfo.Constructor != null && moduleInfo.Constructor.GetParameters().Length > 0)
            {
                var parentJson = await fnCall.ParentAsync();
                using var parentDoc = JsonDocument.Parse(parentJson.Value);
                var parentElement = parentDoc.RootElement;

                var ctorParams = moduleInfo.Constructor.GetParameters();
                var ctorArgs = new object?[ctorParams.Length];

                for (var i = 0; i < ctorParams.Length; i++)
                {
                    var param = ctorParams[i];
                    var paramName = ToCamelCase(param.Name ?? $"arg{i}");

                    if (parentElement.TryGetProperty(paramName, out var propElement))
                    {
                        ctorArgs[i] = await ConvertArgumentAsync(propElement, param.ParameterType, dag);
                    }
                    else if (param.HasDefaultValue)
                    {
                        ctorArgs[i] = param.DefaultValue;
                    }
                    else if (param.IsOptional)
                    {
                        ctorArgs[i] = null;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Missing required constructor argument '{paramName}'.");
                    }
                }

                instance = moduleInfo.Constructor.Invoke(ctorArgs);
            }
            else
            {
                instance = Activator.CreateInstance(moduleInfo.ClrType);
            }

            if (instance == null)
            {
                throw new InvalidOperationException($"Unable to create instance of '{moduleInfo.ClrType.FullName}'.");
            }

            var argumentValues = await LoadArgumentsAsync(dag, fnCall, functionInfo);

            object? invocationResult;
            try
            {
                invocationResult = functionInfo.Method.Invoke(instance, argumentValues);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            if (functionInfo.ReturnsTask)
            {
                if (functionInfo.Method.ReturnType.IsGenericType)
                {
                    dynamic awaitedTask = invocationResult!;
                    invocationResult = await awaitedTask;
                }
                else
                {
                    await (Task)invocationResult!;
                    invocationResult = null;
                }
            }
            else if (functionInfo.ReturnsValueTask)
            {
                if (functionInfo.Method.ReturnType.IsGenericType)
                {
                    dynamic awaitedValueTask = invocationResult!;
                    invocationResult = await awaitedValueTask;
                }
                else
                {
                    await (ValueTask)invocationResult!;
                    invocationResult = null;
                }
            }

            var normalizedResult = await NormalizeResultAsync(invocationResult);
            var jsonResult = new Json
            {
                Value = JsonSerializer.Serialize(normalizedResult, SerializerOptions)
            };

            await fnCall.ReturnValueAsync(jsonResult);
            return 0;
        }
        catch (Exception ex)
        {
            await ReturnErrorAsync(dag, fnCall, ex);
            return 1;
        }
    }

    private static async Task<object?[]> LoadArgumentsAsync(Query dag, FunctionCall fnCall, FunctionInfo functionInfo)
    {
        var providedArgs = await fnCall.InputArgsAsync();
        var argumentMap = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var arg in providedArgs)
        {
            var name = await arg.NameAsync();
            var value = await arg.ValueAsync();
            using var document = JsonDocument.Parse(value.Value);
            argumentMap[name] = document.RootElement.Clone();
        }

        var result = new object?[functionInfo.Parameters.Count];

        for (var i = 0; i < functionInfo.Parameters.Count; i++)
        {
            var parameter = functionInfo.Parameters[i];

            if (parameter.IsCancellationToken)
            {
                result[i] = CancellationToken.None;
                continue;
            }

            if (!argumentMap.TryGetValue(parameter.Name, out var element))
            {
                if (parameter.Parameter.HasDefaultValue)
                {
                    result[i] = parameter.Parameter.DefaultValue;
                }
                else if (parameter.IsOptional)
                {
                    result[i] = null;
                }
                else
                {
                    throw new InvalidOperationException($"Missing required argument '{parameter.Name}'.");
                }

                continue;
            }

            result[i] = await ConvertArgumentAsync(element, parameter.ParameterType, dag);
        }

        return result;
    }

    private static IReadOnlyList<ModuleTypeInfo> BuildModuleTypeInfos()
    {
        var types = DiscoverModuleTypes();
        var moduleTypes = new List<ModuleTypeInfo>();

        foreach (var type in types)
        {
            var daggerAttr = type.GetCustomAttribute<DaggerObjectAttribute>();
            if (daggerAttr is null)
            {
                continue;
            }

            var moduleInfo = new ModuleTypeInfo
            {
                Name = daggerAttr.Name ?? type.Name,
                Description = daggerAttr.Description ?? GetTypeDescription(type),
                Deprecated = daggerAttr.Deprecated,
                ClrType = type,
                Constructor = GetModuleConstructor(type)
            };

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                // Skip methods marked with DaggerIgnore
                if (method.GetCustomAttribute<DaggerIgnoreAttribute>() != null)
                {
                    continue;
                }

                var functionAttr = method.GetCustomAttribute<DaggerFunctionAttribute>();
                if (functionAttr is null)
                {
                    continue;
                }

                var functionName = functionAttr.Name ?? method.Name;
                var returnType = UnwrapReturnType(method.ReturnType, out var returnsTask, out var returnsValueTask, out var returnsVoid);

                var functionInfo = new FunctionInfo
                {
                    Name = ToCamelCase(functionName),
                    Description = functionAttr.Description ?? GetMethodDescription(method),
                    Deprecated = functionAttr.Deprecated,
                    CachePolicy = functionAttr.Cache,
                    Method = method,
                    ReturnType = returnType,
                    ReturnsTask = returnsTask,
                    ReturnsValueTask = returnsValueTask,
                    ReturnsVoid = returnsVoid
                };

                foreach (var parameter in method.GetParameters())
                {
                    if (parameter.GetCustomAttribute<DaggerIgnoreAttribute>() != null)
                    {
                        continue;
                    }

                    var parameterName = ToCamelCase(parameter.Name ?? $"arg{functionInfo.Parameters.Count}");
                    var parameterMetadata = new ParameterMetadata
                    {
                        Name = parameterName,
                        Description = null,
                        Parameter = parameter,
                        ParameterType = parameter.ParameterType,
                        IsOptional = parameter.HasDefaultValue || parameter.IsOptional || Nullable.GetUnderlyingType(parameter.ParameterType) is not null,
                        IsCancellationToken = parameter.ParameterType == typeof(CancellationToken)
                    };

                    functionInfo.Parameters.Add(parameterMetadata);
                }

                moduleInfo.Functions.Add(functionInfo);
            }

            // Discover fields (properties) marked with [DaggerField]
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var fieldAttr = property.GetCustomAttribute<DaggerFieldAttribute>();
                if (fieldAttr is null)
                {
                    continue;
                }

                // Skip properties marked with DaggerIgnore
                if (property.GetCustomAttribute<DaggerIgnoreAttribute>() != null)
                {
                    continue;
                }

                var fieldInfo = new FieldInfo
                {
                    Name = ToCamelCase(fieldAttr.Name ?? property.Name),
                    Description = fieldAttr.Description,
                    Deprecated = fieldAttr.Deprecated,
                    PropertyInfo = property,
                    PropertyType = property.PropertyType
                };

                moduleInfo.Fields.Add(fieldInfo);
            }

            if (moduleInfo.Functions.Count > 0 || moduleInfo.Fields.Count > 0)
            {
                moduleTypes.Add(moduleInfo);
            }
        }

        return moduleTypes;
    }

    private static Type UnwrapReturnType(Type returnType, out bool returnsTask, out bool returnsValueTask, out bool returnsVoid)
    {
        returnsTask = false;
        returnsValueTask = false;
        returnsVoid = false;

        if (returnType == typeof(void))
        {
            returnsVoid = true;
            return typeof(void);
        }

        if (returnType == typeof(Task))
        {
            returnsTask = true;
            returnsVoid = true;
            return typeof(void);
        }

        if (returnType == typeof(ValueTask))
        {
            returnsValueTask = true;
            returnsVoid = true;
            return typeof(void);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnsTask = true;
            var innerType = returnType.GetGenericArguments()[0];
            returnsVoid = innerType == typeof(void);
            return innerType;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            returnsValueTask = true;
            var innerType = returnType.GetGenericArguments()[0];
            returnsVoid = innerType == typeof(void);
            return innerType;
        }

        return returnType;
    }

    private static (TypeDef typeDef, bool isNullable) BuildTypeDef(Query dag, Type clrType)
    {
        var isNullable = false;
        var underlyingNullable = Nullable.GetUnderlyingType(clrType);
        if (underlyingNullable is not null)
        {
            clrType = underlyingNullable;
            isNullable = true;
        }

        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType()!;
            var (elementTypeDef, _) = BuildTypeDef(dag, elementType);
            return (dag.TypeDef().WithListOf(elementTypeDef), isNullable);
        }

        if (clrType.IsGenericType)
        {
            var genericDefinition = clrType.GetGenericTypeDefinition();

            if (genericDefinition == typeof(IEnumerable<>) || genericDefinition == typeof(IReadOnlyList<>) || genericDefinition == typeof(IList<>) || genericDefinition == typeof(List<>))
            {
                var elementType = clrType.GetGenericArguments()[0];
                var (elementTypeDef, _) = BuildTypeDef(dag, elementType);
                return (dag.TypeDef().WithListOf(elementTypeDef), isNullable);
            }
        }

        if (clrType == typeof(string))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.STRING_KIND), isNullable);
        }

        if (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short) || clrType == typeof(byte))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.INTEGER_KIND), isNullable);
        }

        if (clrType == typeof(float) || clrType == typeof(double) || clrType == typeof(decimal))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.FLOAT_KIND), isNullable);
        }

        if (clrType == typeof(bool))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.BOOLEAN_KIND), isNullable);
        }

        if (typeof(Scalar).IsAssignableFrom(clrType))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.SCALAR_KIND), isNullable);
        }

        if (clrType.IsEnum)
        {
            return (dag.TypeDef().WithEnum(clrType.Name), isNullable);
        }

        if (typeof(DaggerObject).IsAssignableFrom(clrType))
        {
            return (dag.TypeDef().WithObject(clrType.Name), isNullable);
        }

        if (clrType == typeof(void))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.VOID_KIND), true);
        }

        if (clrType == typeof(JsonElement) || clrType == typeof(JsonDocument))
        {
            return (dag.TypeDef().WithKind(TypeDefKind.SCALAR_KIND), isNullable);
        }

        throw new NotSupportedException($"Unsupported type '{clrType.FullName}'.");
    }

    private static object? NormalizeDefaultValue(object? defaultValue)
    {
        return defaultValue switch
        {
            null => null,
            string or bool or int or long or short or byte or double or float or decimal => defaultValue,
            Enum enumValue => enumValue.ToString(),
            _ => null
        };
    }

    private static async Task<object?> ConvertArgumentAsync(JsonElement element, Type targetType, Query dag)
    {
        var underlyingNullable = Nullable.GetUnderlyingType(targetType);
        if (underlyingNullable is not null)
        {
            targetType = underlyingNullable;
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(string))
        {
            return element.GetString();
        }

        if (targetType == typeof(int))
        {
            return element.GetInt32();
        }

        if (targetType == typeof(long))
        {
            return element.GetInt64();
        }

        if (targetType == typeof(short))
        {
            return (short)element.GetInt32();
        }

        if (targetType == typeof(byte))
        {
            return (byte)element.GetInt32();
        }

        if (targetType == typeof(bool))
        {
            return element.GetBoolean();
        }

        if (targetType == typeof(double))
        {
            return element.GetDouble();
        }

        if (targetType == typeof(float))
        {
            return element.GetSingle();
        }

        if (targetType == typeof(decimal))
        {
            return element.GetDecimal();
        }

        if (targetType == typeof(Guid))
        {
            return element.GetGuid();
        }

        if (targetType.IsEnum)
        {
            var stringValue = element.GetString();
            if (stringValue is null)
            {
                throw new InvalidOperationException($"Cannot convert null to enum '{targetType.Name}'.");
            }

            return Enum.Parse(targetType, stringValue, ignoreCase: true);
        }

        if (typeof(Scalar).IsAssignableFrom(targetType))
        {
            var scalar = (Scalar)Activator.CreateInstance(targetType)!;
            scalar.Value = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
            return scalar;
        }

        if (typeof(DaggerObject).IsAssignableFrom(targetType))
        {
            var id = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Object when element.TryGetProperty("id", out var idProperty) => idProperty.GetString(),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            var loadMethod = typeof(Query).GetMethod($"Load{targetType.Name}FromId");
            if (loadMethod is null)
            {
                throw new NotSupportedException($"Cannot load '{targetType.Name}' from id.");
            }

            var idType = targetType.Assembly.GetType($"{targetType.Namespace}.{targetType.Name}Id")
                ?? throw new NotSupportedException($"Missing id type for '{targetType.Name}'.");

            var idInstance = Activator.CreateInstance(idType);
            idType.GetProperty("Value")?.SetValue(idInstance, id);

            return loadMethod.Invoke(dag, new[] { idInstance });
        }

        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType()!;
            var items = new List<object?>();
            foreach (var item in element.EnumerateArray())
            {
                items.Add(await ConvertArgumentAsync(item, elementType, dag));
            }

            var array = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }

            return array;
        }

        if (targetType.IsGenericType)
        {
            var genericDefinition = targetType.GetGenericTypeDefinition();

            if (genericDefinition == typeof(IEnumerable<>) || genericDefinition == typeof(IReadOnlyList<>) || genericDefinition == typeof(IList<>) || genericDefinition == typeof(List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;

                foreach (var item in element.EnumerateArray())
                {
                    list.Add(await ConvertArgumentAsync(item, elementType, dag));
                }

                if (genericDefinition == typeof(List<>))
                {
                    return list;
                }

                return list;
            }

            if (genericDefinition == typeof(Dictionary<,>))
            {
                return JsonSerializer.Deserialize(element.GetRawText(), targetType, SerializerOptions);
            }
        }

        if (targetType == typeof(JsonElement))
        {
            return element.Clone();
        }

        if (targetType == typeof(JsonDocument))
        {
            return JsonDocument.Parse(element.GetRawText());
        }

        return JsonSerializer.Deserialize(element.GetRawText(), targetType, SerializerOptions);
    }

    private static async Task<object?> NormalizeResultAsync(object? value)
    {
        if (value is null)
        {
            return null;
        }

        switch (value)
        {
            case string or bool or int or long or short or byte or double or float or decimal:
                return value;
            case Enum enumValue:
                return enumValue.ToString();
            case Scalar scalar:
                return scalar.Value;
            case JsonElement element:
                return JsonSerializer.Deserialize<object>(element.GetRawText(), SerializerOptions);
            case JsonDocument document:
                return JsonSerializer.Deserialize<object>(document.RootElement.GetRawText(), SerializerOptions);
            case IEnumerable sequence when value is not string:
            {
                var list = new List<object?>();
                foreach (var item in sequence)
                {
                    list.Add(await NormalizeResultAsync(item));
                }

                return list;
            }
        }

        if (value is DaggerObject daggerObject)
        {
            dynamic dynamicObject = daggerObject;
            var id = await dynamicObject.IdAsync();
            return id.Value;
        }

        return JsonSerializer.Deserialize<object>(
            JsonSerializer.Serialize(value, SerializerOptions),
            SerializerOptions
        );
    }

    private static async Task ReturnErrorAsync(Query dag, FunctionCall fnCall, Exception ex)
    {
        Console.Error.WriteLine(ex);
        await ReturnErrorAsync(dag, fnCall, ex.Message);
    }

    private static async Task ReturnErrorAsync(Query dag, FunctionCall fnCall, string message)
    {
        var error = dag.Error(message);
        await fnCall.ReturnErrorAsync(error);
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
    /// Gets the description from a type's XML documentation or attributes.
    /// </summary>
    private static string? GetTypeDescription(Type type)
    {
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

        return null;
    }

    /// <summary>
    /// Gets the description from a method's XML documentation or attributes.
    /// </summary>
    private static string? GetMethodDescription(MethodInfo method)
    {
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

        return null;
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

    /// <summary>
    /// Discovers all enum types in the entry assembly marked with [DaggerEnum].
    /// </summary>
    private static IReadOnlyList<EnumTypeInfo> BuildEnumTypeInfos()
    {
        var enumTypes = new List<EnumTypeInfo>();
        var assembly = Assembly.GetEntryAssembly();

        if (assembly == null)
        {
            return enumTypes;
        }

        foreach (var type in assembly.GetTypes().Where(t => t.IsEnum))
        {
            var enumAttr = type.GetCustomAttribute<DaggerEnumAttribute>();
            if (enumAttr is null)
            {
                continue; // Only process enums with [DaggerEnum]
            }

            var enumInfo = new EnumTypeInfo
            {
                Name = enumAttr.Name ?? type.Name,
                Description = enumAttr.Description,
                EnumType = type
            };

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var valueAttr = field.GetCustomAttribute<DaggerEnumValueAttribute>();
                var value = field.GetRawConstantValue()?.ToString() ?? field.Name;

                enumInfo.Values.Add(new EnumValueInfo
                {
                    Name = field.Name,
                    Value = value,
                    Description = valueAttr?.Description,
                    Deprecated = valueAttr?.Deprecated
                });
            }

            enumTypes.Add(enumInfo);
        }

        return enumTypes;
    }

    /// <summary>
    /// Gets the module constructor if it exists and has parameters.
    /// </summary>
    private static ConstructorInfo? GetModuleConstructor(Type moduleType)
    {
        var constructors = moduleType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        // Prefer default constructor for parameter-less case
        var defaultCtor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (defaultCtor != null && constructors.Length == 1)
        {
            return null; // Only default constructor, no need to register
        }

        // Return the first constructor with parameters, or null if only default exists
        return constructors.FirstOrDefault(c => c.GetParameters().Length > 0);
    }
}
#endif
