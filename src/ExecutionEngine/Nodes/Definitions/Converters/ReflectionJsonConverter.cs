// -----------------------------------------------------------------------
// <copyright file="ReflectionJsonConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes.Definitions.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Generic reflection-based JSON converter that can be inherited for type-specific converters
/// </summary>
public class ReflectionJsonConverter<T> : JsonConverter<T> where T : class, new()
{
    private static readonly Dictionary<string, PropertyInfo> PropertyMap = BuildPropertyMap();
    private static readonly Dictionary<string, Dictionary<string, PropertyInfo>> PropertyMapByJsonNameCache = new();
    private static readonly object CacheLock = new();

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");
        }

        var instance = new T();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return instance;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");
            }

            var propertyName = reader.GetString()!;

            // Get property map based on the naming policy from options
            var propertyMapByJsonName = GetPropertyMapByJsonName(options);

            // Try to find property by JSON name first, then by actual property name
            if (!propertyMapByJsonName.TryGetValue(propertyName, out var property))
            {
                ReflectionJsonConverter<T>.PropertyMap.TryGetValue(propertyName, out property);
            }

            reader.Read(); // Move to property value

            if (property != null)
            {
                var value = this.ReadProperty(ref reader, property.PropertyType, options);
                property.SetValue(instance, value);
            }
            else
            {
                // Skip unknown property
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        // For NodeDefinition types, write the RuntimeType property first as a discriminator
        if (value is NodeDefinition nodeDef)
        {
            var runtimeTypeValue = nodeDef.RuntimeType.ToString();
            writer.WriteString("runtimeType", runtimeTypeValue);
        }

        foreach (var property in ReflectionJsonConverter<T>.PropertyMap.Values.Distinct())
        {
            var propValue = property.GetValue(value);
            var jsonName = GetJsonPropertyName(property, options);

            writer.WritePropertyName(jsonName);
            this.WriteProperty(writer, propValue, property.PropertyType, options);
        }

        writer.WriteEndObject();
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap()
    {
        return typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToDictionary(
                p => p.Name,
                p => p,
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static Dictionary<string, PropertyInfo> GetPropertyMapByJsonName(JsonSerializerOptions options)
    {
        // Create a cache key based on the naming policy type
        var cacheKey = options.PropertyNamingPolicy?.GetType().FullName ?? "null";

        // Check cache first
        lock (CacheLock)
        {
            if (PropertyMapByJsonNameCache.TryGetValue(cacheKey, out var cachedMap))
            {
                return cachedMap;
            }
        }

        // Build new map for this naming policy
        var map = BuildPropertyMapByJsonName(options);

        // Cache it
        lock (CacheLock)
        {
            PropertyMapByJsonNameCache[cacheKey] = map;
        }

        return map;
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMapByJsonName(JsonSerializerOptions options)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in ReflectionJsonConverter<T>.PropertyMap.Values)
        {
            // Use the naming policy from options to get the JSON property name
            var jsonName = GetJsonPropertyName(property, options);
            map[jsonName] = property;
        }

        return map;
    }

    private static string GetJsonPropertyName(PropertyInfo property, JsonSerializerOptions? options)
    {
        var attr = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (attr != null)
        {
            return attr.Name;
        }

        // Apply naming policy if available (typically camelCase)
        if (options?.PropertyNamingPolicy != null)
        {
            return options.PropertyNamingPolicy.ConvertName(property.Name);
        }

        // Fallback to camelCase conversion
        return ToCamelCase(property.Name);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private object? ReadProperty(ref Utf8JsonReader reader, Type propertyType, JsonSerializerOptions options)
    {
        // Handle null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        // Handle collections
        if (IsCollectionType(propertyType))
        {
            return this.ReadCollection(ref reader, propertyType, options);
        }

        // Handle nested objects
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return this.ReadNestedObject(ref reader, propertyType, options);
        }

        // Handle scalars
        return this.ReadScalar(ref reader, propertyType);
    }

    private object? ReadCollection(ref Utf8JsonReader reader, Type collectionType, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return null;
        }

        var elementType = GetElementType(collectionType);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            var item = this.ReadProperty(ref reader, elementType, options);
            list.Add(item);
        }

        // Convert to array if needed
        if (collectionType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        return list;
    }

    private object? ReadNestedObject(ref Utf8JsonReader reader, Type objectType, JsonSerializerOptions options)
    {
        // Handle polymorphic deserialization for NodeDefinition
        if (typeof(NodeDefinition).IsAssignableFrom(objectType) && objectType.IsAbstract)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            // Look for runtimeType discriminator (try both camelCase and PascalCase)
            JsonElement runtimeTypeProp = default;
            bool foundRuntimeType = root.TryGetProperty("runtimeType", out runtimeTypeProp) ||
                                   root.TryGetProperty("RuntimeType", out runtimeTypeProp);

            if (foundRuntimeType)
            {
                var runtimeTypeName = runtimeTypeProp.GetString();
                var concreteType = MapRuntimeTypeToClass(runtimeTypeName);
                if (concreteType != null)
                {
                    var json = root.GetRawText();
                    return JsonSerializer.Deserialize(json, concreteType, options);
                }
            }

            // Debug: list all properties in the JSON
            var props = string.Join(", ", root.EnumerateObject().Select(p => p.Name));
            throw new JsonException($"Cannot deserialize abstract type {objectType.Name} without 'runtimeType' discriminator. Properties found: {props}");
        }

        // Use JsonSerializer.Deserialize which handles the ref struct properly
        var element = JsonSerializer.Deserialize(ref reader, objectType, options);
        return element;
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

    private object? ReadScalar(ref Utf8JsonReader reader, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle enums
        if (underlyingType.IsEnum)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var enumString = reader.GetString()!;
                return Enum.Parse(underlyingType, enumString, ignoreCase: true);
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                var enumValue = reader.GetInt32();
                return Enum.ToObject(underlyingType, enumValue);
            }
        }

        // Handle primitives and common types
        return Type.GetTypeCode(underlyingType) switch
        {
            TypeCode.String => reader.GetString(),
            TypeCode.Int32 => reader.GetInt32(),
            TypeCode.Int64 => reader.GetInt64(),
            TypeCode.Double => reader.GetDouble(),
            TypeCode.Decimal => reader.GetDecimal(),
            TypeCode.Boolean => reader.GetBoolean(),
            TypeCode.DateTime => reader.GetDateTime(),
            TypeCode.Byte => reader.GetByte(),
            TypeCode.Int16 => reader.GetInt16(),
            TypeCode.UInt32 => reader.GetUInt32(),
            TypeCode.UInt64 => reader.GetUInt64(),
            TypeCode.Single => reader.GetSingle(),
            _ => underlyingType == typeof(Guid)
                ? reader.GetGuid()
                : reader.TokenType == JsonTokenType.String
                    ? Convert.ChangeType(reader.GetString(), underlyingType)
                    : null
        };
    }

    private void WriteProperty(Utf8JsonWriter writer, object? value, Type propertyType, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Handle dictionaries first (before IEnumerable check, since dictionaries implement IEnumerable)
        if (value is System.Collections.IDictionary dict)
        {
            this.WriteDictionary(writer, dict, propertyType, options);
            return;
        }

        // Handle collections
        if (value is IEnumerable enumerable && value is not string)
        {
            this.WriteCollection(writer, enumerable, propertyType, options);
            return;
        }

        // Handle nested objects
        if (IsComplexType(propertyType))
        {
            this.WriteNestedObject(writer, value, propertyType, options);
            return;
        }

        // Handle scalars
        this.WriteScalar(writer, value, propertyType);
    }

    private void WriteCollection(Utf8JsonWriter writer, IEnumerable collection, Type collectionType, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        var elementType = GetElementType(collectionType);
        foreach (var item in collection)
        {
            if (item == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                this.WriteProperty(writer, item, elementType, options);
            }
        }

        writer.WriteEndArray();
    }

    private void WriteDictionary(Utf8JsonWriter writer, System.Collections.IDictionary dictionary, Type dictionaryType, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString();
            if (key == null)
            {
                continue;
            }

            writer.WritePropertyName(key);

            if (entry.Value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                // Get the value type from the dictionary's generic arguments if available
                var valueType = dictionaryType.IsGenericType && dictionaryType.GetGenericArguments().Length == 2
                    ? dictionaryType.GetGenericArguments()[1]
                    : typeof(object);

                this.WriteProperty(writer, entry.Value, valueType, options);
            }
        }

        writer.WriteEndObject();
    }

    private void WriteNestedObject(Utf8JsonWriter writer, object value, Type objectType, JsonSerializerOptions options)
    {
        // Use the actual runtime type instead of the declared type to ensure
        // derived class properties are serialized (e.g., CSharpScriptNodeDefinition properties)
        var actualType = value.GetType();
        JsonSerializer.Serialize(writer, value, actualType, options);
    }

    private void WriteScalar(Utf8JsonWriter writer, object value, Type valueType)
    {
        var underlyingType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (underlyingType.IsEnum)
        {
            writer.WriteStringValue(value.ToString());
            return;
        }

        switch (Type.GetTypeCode(underlyingType))
        {
            case TypeCode.String:
                writer.WriteStringValue((string)value);
                break;
            case TypeCode.Int32:
                writer.WriteNumberValue((int)value);
                break;
            case TypeCode.Int64:
                writer.WriteNumberValue((long)value);
                break;
            case TypeCode.Double:
                writer.WriteNumberValue((double)value);
                break;
            case TypeCode.Decimal:
                writer.WriteNumberValue((decimal)value);
                break;
            case TypeCode.Boolean:
                writer.WriteBooleanValue((bool)value);
                break;
            case TypeCode.DateTime:
                writer.WriteStringValue((DateTime)value);
                break;
            case TypeCode.Byte:
                writer.WriteNumberValue((byte)value);
                break;
            case TypeCode.Int16:
                writer.WriteNumberValue((short)value);
                break;
            case TypeCode.UInt32:
                writer.WriteNumberValue((uint)value);
                break;
            case TypeCode.UInt64:
                writer.WriteNumberValue((ulong)value);
                break;
            case TypeCode.Single:
                writer.WriteNumberValue((float)value);
                break;
            default:
                if (underlyingType == typeof(Guid))
                {
                    writer.WriteStringValue((Guid)value);
                }
                else
                {
                    writer.WriteStringValue(value.ToString());
                }
                break;
        }
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

    private static bool IsComplexType(Type type)
    {
        return type.IsClass &&
               type != typeof(string) &&
               !type.IsPrimitive &&
               type != typeof(Guid) &&
               type != typeof(DateTime) &&
               type != typeof(DateTimeOffset);
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
}