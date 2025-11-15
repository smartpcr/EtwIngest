// -----------------------------------------------------------------------
// <copyright file="ConcurrentDictionaryJsonConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// JSON converter for ConcurrentDictionary&lt;string, object&gt; that handles
/// serialization and deserialization of workflow variables and node queues.
/// Required because get-only dictionary properties can't be deserialized by default.
/// </summary>
public class ConcurrentDictionaryJsonConverter : JsonConverter<ConcurrentDictionary<string, object>>
{
    /// <inheritdoc/>
    public override ConcurrentDictionary<string, object> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        var dictionary = new ConcurrentDictionary<string, object>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            // Read the property name
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            string key = reader.GetString() ?? throw new JsonException("Property name is null");

            // Read the property value
            reader.Read();
            object? value = ReadValue(ref reader);

            if (value != null)
            {
                dictionary[key] = value;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        ConcurrentDictionary<string, object> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(kvp.Key) ?? kvp.Key);
            WriteValue(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads a JSON value and converts it to an object.
    /// </summary>
    private static object? ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt32(out int intValue) ? intValue : reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => ReadObject(ref reader),
            JsonTokenType.StartArray => ReadArray(ref reader),
            _ => throw new JsonException($"Unsupported token type: {reader.TokenType}")
        };
    }

    /// <summary>
    /// Reads a JSON object.
    /// </summary>
    private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader)
    {
        var obj = new Dictionary<string, object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return obj;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            string key = reader.GetString() ?? throw new JsonException("Property name is null");
            reader.Read();
            obj[key] = ReadValue(ref reader);
        }

        throw new JsonException("Unexpected end of object");
    }

    /// <summary>
    /// Reads a JSON array.
    /// </summary>
    private static List<object?> ReadArray(ref Utf8JsonReader reader)
    {
        var array = new List<object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return array;
            }

            array.Add(ReadValue(ref reader));
        }

        throw new JsonException("Unexpected end of array");
    }

    /// <summary>
    /// Writes a value to JSON.
    /// </summary>
    private static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            default:
                // For complex objects, use default serialization
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
                break;
        }
    }
}
