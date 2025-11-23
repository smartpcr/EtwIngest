// -----------------------------------------------------------------------
// <copyright file="ReflectionYamlConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions.Converters;

using System.Collections;
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

public class ReflectionYamlConverter<T> : YamlTypeConverter<T> where T : class, new()
{
    private static readonly Dictionary<string, PropertyInfo> PropertyMap = BuildPropertyMap();

    public override T? Read(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is not MappingStart)
        {
            return null;
        }

        parser.MoveNext();
        var instance = new T();

        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is not Scalar keyScalar)
            {
                throw new YamlException($"Expected property name, got {parser.Current?.GetType().Name}");
            }

            var propertyName = keyScalar.Value;
            parser.MoveNext();

            if (ReflectionYamlConverter<T>.PropertyMap.TryGetValue(propertyName, out var property))
            {
                var value = this.ReadProperty(parser, property.PropertyType, rootDeserializer);
                property.SetValue(instance, value);
            }
            else
            {
                this.SkipValue(parser);
            }
        }

        parser.MoveNext(); // Move past MappingEnd
        return instance;
    }

    public override void Write(IEmitter emitter, T? value, Type type, ObjectSerializer serializer)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        emitter.Emit(new MappingStart());

        // For NodeDefinition types, write the runtimeType property first as a discriminator
        if (value is NodeDefinition nodeDef)
        {
            emitter.Emit(new Scalar("runtimeType"));
            emitter.Emit(new Scalar(nodeDef.RuntimeType.ToString()));
        }

        foreach (var (name, property) in ReflectionYamlConverter<T>.PropertyMap)
        {
            var propValue = property.GetValue(value);

            emitter.Emit(new Scalar(name));
            this.WriteProperty(emitter, propValue, property.PropertyType, serializer);
        }

        emitter.Emit(new MappingEnd());
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToDictionary(
                p => p.GetCustomAttribute<YamlMemberAttribute>()?.Alias ?? p.Name,
                p => p,
                StringComparer.OrdinalIgnoreCase
            );
    }

    private object? ReadProperty(IParser parser, Type propertyType, ObjectDeserializer deserializer)
    {
        // Handle null
        if (parser.Current is Scalar { Value: "null" or "" })
        {
            parser.MoveNext();
            return null;
        }

        // For object type, use default deserializer to infer the type from YAML structure
        // This allows Dictionary<string, object> to contain any type of data (strings, numbers, arrays, etc.)
        if (propertyType == typeof(object))
        {
            return deserializer(propertyType);
        }

        // Handle collections
        if (IsCollectionType(propertyType))
        {
            return this.ReadCollection(parser, propertyType, deserializer);
        }

        // Handle dictionaries
        if (IsDictionaryType(propertyType) && parser.Current is MappingStart)
        {
            return this.ReadDictionary(parser, propertyType, deserializer);
        }

        // Handle nested objects
        if (parser.Current is MappingStart)
        {
            return this.ReadNestedObject(parser, propertyType, deserializer);
        }

        // Handle scalars
        if (parser.Current is Scalar scalar)
        {
            var value = ConvertScalar(scalar.Value, propertyType);
            parser.MoveNext();
            return value;
        }

        parser.MoveNext();
        return null;
    }

    private object? ReadCollection(IParser parser, Type collectionType, ObjectDeserializer deserializer)
    {
        if (parser.Current is not SequenceStart)
        {
            return null;
        }

        parser.MoveNext();

        var elementType = GetElementType(collectionType);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        while (parser.Current is not SequenceEnd)
        {
            var item = this.ReadProperty(parser, elementType, deserializer);
            list.Add(item);
        }

        parser.MoveNext(); // Move past SequenceEnd

        // Convert to array if needed
        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        return list;
    }

    private object? ReadDictionary(IParser parser, Type dictionaryType, ObjectDeserializer deserializer)
    {
        if (parser.Current is not MappingStart)
        {
            return null;
        }

        parser.MoveNext();

        var keyType = dictionaryType.GetGenericArguments()[0];
        var valueType = dictionaryType.GetGenericArguments()[1];
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = Activator.CreateInstance(dictType)!;
        var addMethod = dictType.GetMethod("Add")!;

        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is not Scalar keyScalar)
            {
                parser.MoveNext();
                continue;
            }

            var key = Convert.ChangeType(keyScalar.Value, keyType);
            parser.MoveNext();

            var value = this.ReadProperty(parser, valueType, deserializer);
            addMethod.Invoke(dict, new[] { key, value });
        }

        parser.MoveNext(); // Move past MappingEnd
        return dict;
    }

    private object? ReadNestedObject(IParser parser, Type objectType, ObjectDeserializer deserializer)
    {
        // Handle polymorphic deserialization for NodeDefinition
        if (typeof(NodeDefinition).IsAssignableFrom(objectType) && objectType.IsAbstract)
        {
            return this.ReadPolymorphicNodeDefinition(parser, objectType, deserializer);
        }

        // Use reflection to create converter for nested type
        var converterType = typeof(ReflectionYamlConverter<>).MakeGenericType(objectType);
        var converter = (IYamlTypeConverter)Activator.CreateInstance(converterType)!;
        return converter.ReadYaml(parser, objectType, deserializer);
    }

    private object? ReadPolymorphicNodeDefinition(IParser parser, Type objectType, ObjectDeserializer deserializer)
    {
        if (parser.Current is not MappingStart)
        {
            return null;
        }

        parser.MoveNext();

        // Read all properties and look for runtimeType
        string? runtimeType = null;
        var properties = new Dictionary<string, object?>();

        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is not Scalar keyScalar)
            {
                throw new YamlException($"Expected property name, got {parser.Current?.GetType().Name}");
            }

            var propertyName = keyScalar.Value;
            parser.MoveNext();

            // Special handling for runtimeType
            if (propertyName.Equals("runtimeType", StringComparison.OrdinalIgnoreCase))
            {
                if (parser.Current is Scalar runtimeTypeScalar)
                {
                    runtimeType = runtimeTypeScalar.Value;
                }
                parser.MoveNext();
                continue;
            }

            // Read property value and store it
            var value = this.ReadPropertyValue(parser, deserializer);
            properties[propertyName] = value;
        }

        parser.MoveNext(); // Move past MappingEnd

        if (string.IsNullOrEmpty(runtimeType))
        {
            throw new YamlException($"Cannot deserialize abstract type {objectType.Name} without 'runtimeType' discriminator");
        }

        var concreteType = MapRuntimeTypeToClass(runtimeType);
        if (concreteType == null)
        {
            throw new YamlException($"Unknown runtime type: {runtimeType}");
        }

        // Create instance and set properties
        var instance = Activator.CreateInstance(concreteType);
        if (instance == null)
        {
            throw new YamlException($"Failed to create instance of type {concreteType.Name}");
        }

        // Set properties using reflection
        var propertyMap = concreteType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in properties)
        {
            if (propertyMap.TryGetValue(name, out var property))
            {
                try
                {
                    // Convert value to correct type if needed
                    object? convertedValue = ConvertPropertyValue(value, property.PropertyType);
                    property.SetValue(instance, convertedValue);
                }
                catch (Exception ex)
                {
                    throw new YamlException($"Failed to set property {name} on type {concreteType.Name}: {ex.Message}", ex);
                }
            }
        }

        return instance;
    }

    private object? ReadPropertyValue(IParser parser, ObjectDeserializer deserializer)
    {
        // Handle null
        if (parser.Current is Scalar { Value: "null" or "" })
        {
            parser.MoveNext();
            return null;
        }

        // Handle sequences (lists)
        if (parser.Current is SequenceStart)
        {
            return this.ReadSequenceValue(parser, deserializer);
        }

        // Handle mappings (nested objects)
        if (parser.Current is MappingStart)
        {
            return this.ReadMappingValue(parser, deserializer);
        }

        // Handle scalars
        if (parser.Current is Scalar scalar)
        {
            var value = scalar.Value;
            parser.MoveNext();
            return value;
        }

        parser.MoveNext();
        return null;
    }

    private object? ReadSequenceValue(IParser parser, ObjectDeserializer deserializer)
    {
        parser.MoveNext(); // Move past SequenceStart
        var list = new List<object?>();

        while (parser.Current is not SequenceEnd)
        {
            var item = this.ReadPropertyValue(parser, deserializer);
            list.Add(item);
        }

        parser.MoveNext(); // Move past SequenceEnd
        return list;
    }

    private object? ReadMappingValue(IParser parser, ObjectDeserializer deserializer)
    {
        parser.MoveNext(); // Move past MappingStart
        var dict = new Dictionary<string, object?>();

        while (parser.Current is not MappingEnd)
        {
            if (parser.Current is Scalar keyScalar)
            {
                var key = keyScalar.Value;
                parser.MoveNext();
                var value = this.ReadPropertyValue(parser, deserializer);
                dict[key] = value;
            }
            else
            {
                parser.MoveNext();
            }
        }

        parser.MoveNext(); // Move past MappingEnd
        return dict;
    }

    private Type? MapRuntimeTypeToClass(string? runtimeType)
    {
        return runtimeType switch
        {
            "Noop" => typeof(NoopNodeDefinition),
            "CSharp" => typeof(CSharpNodeDefinition),
            "CSharpScript" => typeof(CSharpScriptNodeDefinition),
            "CSharpTask" => typeof(CSharpTaskNodeDefinition),
            "PowerShell" => typeof(PowerShellScriptNodeDefinition),
            "PowerShellTask" => typeof(PowerShellTaskNodeDefinition),
            "IfElse" => typeof(IfElseNodeDefinition),
            "ForEach" => typeof(ForEachNodeDefinition),
            "While" => typeof(WhileNodeDefinition),
            "Switch" => typeof(SwitchNodeDefinition),
            "Subflow" => typeof(SubflowNodeDefinition),
            "Timer" => typeof(TimerNodeDefinition),
            "Container" => typeof(ContainerNodeDefinition),
            _ => null
        };
    }

    private void WriteProperty(IEmitter emitter, object? value, Type propertyType, ObjectSerializer serializer)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        // Handle dictionaries before IEnumerable check (since dictionaries implement IEnumerable)
        if (IsDictionaryType(propertyType) || (value is System.Collections.IDictionary && propertyType == typeof(object)))
        {
            this.WriteDictionary(emitter, value, propertyType, serializer);
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            this.WriteCollection(emitter, enumerable, serializer);
        }
        else if (IsComplexType(value.GetType()))
        {
            this.WriteNestedObject(emitter, value, value.GetType(), serializer);
        }
        else
        {
            emitter.Emit(new Scalar(value.ToString() ?? ""));
        }
    }

    private void WriteCollection(IEmitter emitter, IEnumerable collection, ObjectSerializer serializer)
    {
        emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));

        foreach (var item in collection)
        {
            if (item == null)
            {
                emitter.Emit(new Scalar("null"));
            }
            else if (IsComplexType(item.GetType()))
            {
                this.WriteNestedObject(emitter, item, item.GetType(), serializer);
            }
            else
            {
                emitter.Emit(new Scalar(item.ToString() ?? ""));
            }
        }

        emitter.Emit(new SequenceEnd());
    }

    private void WriteDictionary(IEmitter emitter, object value, Type dictionaryType, ObjectSerializer serializer)
    {
        emitter.Emit(new MappingStart());

        var dict = value as System.Collections.IDictionary;
        if (dict != null)
        {
            var valueType = dictionaryType.GetGenericArguments().Length > 1
                ? dictionaryType.GetGenericArguments()[1]
                : typeof(object);

            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString();
                if (key != null)
                {
                    emitter.Emit(new Scalar(key));
                    this.WriteProperty(emitter, entry.Value, valueType, serializer);
                }
            }
        }

        emitter.Emit(new MappingEnd());
    }

    private void WriteNestedObject(IEmitter emitter, object value, Type objectType, ObjectSerializer serializer)
    {
        // Use the actual runtime type instead of the declared type to ensure
        // derived class properties are serialized (e.g., CSharpScriptNodeDefinition properties)
        var actualType = value.GetType();
        var converterType = typeof(ReflectionYamlConverter<>).MakeGenericType(actualType);
        var converter = (IYamlTypeConverter)Activator.CreateInstance(converterType)!;
        converter.WriteYaml(emitter, value, actualType, serializer);
    }

    private static bool IsCollectionType(Type type)
    {
        return type.IsArray ||
               (type.IsGenericType &&
                (type.GetGenericTypeDefinition() == typeof(List<>) ||
                 type.GetGenericTypeDefinition() == typeof(IList<>) ||
                 type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                 type.GetGenericTypeDefinition() == typeof(IEnumerable<>)));
    }

    private static bool IsDictionaryType(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static bool IsComplexType(Type type)
    {
        return type.IsClass &&
               type != typeof(string) &&
               !type.IsPrimitive;
    }

    private static Type GetElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType()!;
        }

        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    private static object? ConvertScalar(string value, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType.IsEnum)
        {
            return Enum.Parse(underlyingType, value, ignoreCase: true);
        }

        return Type.GetTypeCode(underlyingType) switch
        {
            TypeCode.String => value,
            TypeCode.Int32 => int.Parse(value),
            TypeCode.Int64 => long.Parse(value),
            TypeCode.Double => double.Parse(value),
            TypeCode.Decimal => decimal.Parse(value),
            TypeCode.Boolean => bool.Parse(value),
            TypeCode.DateTime => DateTime.Parse(value),
            _ => underlyingType == typeof(Guid)
                ? Guid.Parse(value)
                : underlyingType == typeof(TimeSpan)
                    ? TimeSpan.Parse(value)
                    : throw new InvalidOperationException(
                        $"Cannot convert scalar value '{value}' to type {underlyingType.Name}. " +
                        $"Complex types must be deserialized as objects, not scalars.")
        };
    }

    private object? ConvertPropertyValue(object? value, Type targetType)
    {
        Console.WriteLine($"DEBUG ConvertPropertyValue called: value type = {value?.GetType().FullName}, targetType = {targetType.FullName}");

        // Handle null
        if (value == null)
        {
            return null;
        }

        // If value already matches target type, check if it's a dictionary that needs value conversion
        if (targetType.IsInstanceOfType(value))
        {
            // If it's a dictionary, we still need to convert the values inside it
            if (value is Dictionary<string, object?> dict && IsDictionaryType(targetType))
            {
                Console.WriteLine($"DEBUG ConvertPropertyValue: Dictionary matches target type, but need to convert values");
                // Don't return yet - fall through to dictionary handling
            }
            else
            {
                Console.WriteLine($"DEBUG ConvertPropertyValue: value already matches target type, returning as-is");
                return value;
            }
        }

        // Handle string to scalar conversion
        if (value is string strValue)
        {
            return ConvertScalar(strValue, targetType);
        }

        // Handle collections - convert List<object> to properly typed lists
        if (value is IList<object> listValue)
        {
            Type elementType;
            if (IsCollectionType(targetType))
            {
                elementType = GetElementType(targetType);
            }
            else if (targetType == typeof(object) && listValue.Count > 0)
            {
                // Infer type from first item - if it's a dict with runtimeType, assume NodeDefinition
                var firstItem = listValue[0];
                Console.WriteLine($"DEBUG ConvertPropertyValue: firstItem type = {firstItem?.GetType().FullName}");
                if (firstItem is Dictionary<string, object?> dict &&
                    (dict.ContainsKey("runtimeType") || dict.ContainsKey("RuntimeType")))
                {
                    Console.WriteLine($"DEBUG ConvertPropertyValue: Found runtimeType, converting to List<NodeDefinition>");
                    elementType = typeof(NodeDefinition);
                }
                else
                {
                    Console.WriteLine($"DEBUG ConvertPropertyValue: No runtimeType found, returning as-is");
                    if (firstItem is Dictionary<string, object?> d)
                    {
                        Console.WriteLine($"DEBUG ConvertPropertyValue: Dictionary keys: {string.Join(", ", d.Keys)}");
                    }
                    // Can't convert - return as-is
                    return value;
                }
            }
            else
            {
                // Can't convert - return as-is
                return value;
            }

            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in listValue)
            {
                var convertedItem = ConvertPropertyValue(item, elementType);
                list.Add(convertedItem);
            }

            // Convert to array if needed
            if (targetType.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        // Handle dictionary to object conversion
        if (value is Dictionary<string, object?> dictValue)
        {
            // If target is also a dictionary type, convert the values inside
            if (IsDictionaryType(targetType))
            {
                var keyType = targetType.GetGenericArguments()[0];
                var valueType = targetType.GetGenericArguments().Length > 1
                    ? targetType.GetGenericArguments()[1]
                    : typeof(object);

                // Create a dictionary of the correct generic type
                var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                var newDict = Activator.CreateInstance(dictType)!;
                var addMethod = dictType.GetMethod("Add")!;

                foreach (var (key, val) in dictValue)
                {
                    var convertedVal = ConvertPropertyValue(val, valueType);
                    addMethod.Invoke(newDict, new[] { key, convertedVal });
                }
                return newDict;
            }
            else
            {
                return ConvertDictionaryToObject(dictValue, targetType);
            }
        }

        // Fallback
        return value;
    }

    private object? ConvertDictionaryToObject(Dictionary<string, object?> dict, Type targetType)
    {
        // Handle polymorphic NodeDefinition types
        if (typeof(NodeDefinition).IsAssignableFrom(targetType))
        {
            // Look for runtimeType discriminator
            if (dict.TryGetValue("runtimeType", out var runtimeTypeValue) && runtimeTypeValue is string runtimeTypeStr)
            {
                var concreteType = MapRuntimeTypeToClass(runtimeTypeStr);
                if (concreteType != null)
                {
                    var instance = Activator.CreateInstance(concreteType);
                    if (instance == null)
                    {
                        throw new YamlException($"Failed to create instance of type {concreteType.Name}");
                    }

                    // Set properties using reflection
                    var propertyMap = concreteType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanWrite)
                        .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

                    foreach (var (name, value) in dict)
                    {
                        if (name.Equals("runtimeType", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // Skip discriminator
                        }

                        if (propertyMap.TryGetValue(name, out var property))
                        {
                            var convertedValue = ConvertPropertyValue(value, property.PropertyType);
                            property.SetValue(instance, convertedValue);
                        }
                    }

                    return instance;
                }
            }
        }

        // For non-polymorphic types, create instance and set properties
        var targetInstance = Activator.CreateInstance(targetType);
        if (targetInstance == null)
        {
            return null;
        }

        var props = targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in dict)
        {
            if (props.TryGetValue(name, out var property))
            {
                var convertedValue = ConvertPropertyValue(value, property.PropertyType);
                property.SetValue(targetInstance, convertedValue);
            }
        }

        return targetInstance;
    }

    private void SkipValue(IParser parser)
    {
        var depth = 0;
        do
        {
            switch (parser.Current)
            {
                case MappingStart or SequenceStart:
                    depth++;
                    break;
                case MappingEnd or SequenceEnd:
                    depth--;
                    break;
            }
            parser.MoveNext();
        } while (depth > 0);
    }
}