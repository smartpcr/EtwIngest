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

        // Handle collections
        if (IsCollectionType(propertyType))
        {
            return this.ReadCollection(parser, propertyType, deserializer);
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

    private object? ReadNestedObject(IParser parser, Type objectType, ObjectDeserializer deserializer)
    {
        // Use reflection to create converter for nested type
        var converterType = typeof(ReflectionYamlConverter<>).MakeGenericType(objectType);
        var converter = (IYamlTypeConverter)Activator.CreateInstance(converterType)!;
        return converter.ReadYaml(parser, objectType, deserializer);
    }

    private void WriteProperty(IEmitter emitter, object? value, Type propertyType, ObjectSerializer serializer)
    {
        if (value == null)
        {
            emitter.Emit(new Scalar("null"));
            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            this.WriteCollection(emitter, enumerable, serializer);
        }
        else if (IsComplexType(propertyType))
        {
            this.WriteNestedObject(emitter, value, propertyType, serializer);
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

    private void WriteNestedObject(IEmitter emitter, object value, Type objectType, ObjectSerializer serializer)
    {
        var converterType = typeof(ReflectionYamlConverter<>).MakeGenericType(objectType);
        var converter = (IYamlTypeConverter)Activator.CreateInstance(converterType)!;
        converter.WriteYaml(emitter, value, objectType, serializer);
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
                : Convert.ChangeType(value, underlyingType)
        };
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