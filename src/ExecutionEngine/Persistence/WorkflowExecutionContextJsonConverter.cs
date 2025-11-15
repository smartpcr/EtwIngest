// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionContextJsonConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Text.Json;
using System.Text.Json.Serialization;
using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;

/// <summary>
/// Custom JSON converter for WorkflowExecutionContext that properly handles
/// get-only ConcurrentDictionary properties (Variables and NodeQueues).
/// </summary>
public class WorkflowExecutionContextJsonConverter : JsonConverter<WorkflowExecutionContext>
{
    private readonly ConcurrentDictionaryJsonConverter dictionaryConverter = new();

    /// <inheritdoc/>
    public override WorkflowExecutionContext Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object");
        }

        var context = new WorkflowExecutionContext();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return context;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name");
            }

            var propertyName = reader.GetString() ?? throw new JsonException("Property name is null");
            reader.Read();

            // Handle properties based on the naming policy (camelCase in this case)
            switch (propertyName.ToLowerInvariant())
            {
                case "instanceid":
                    // InstanceId is read-only and set in constructor, skip it
                    _ = reader.GetGuid();
                    break;

                case "graphid":
                    context.GraphId = reader.GetString() ?? string.Empty;
                    break;

                case "workflowid":
                    context.WorkflowId = reader.GetString() ?? string.Empty;
                    break;

                case "status":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var statusString = reader.GetString();
                        if (Enum.TryParse<WorkflowExecutionStatus>(statusString, true, out var status))
                        {
                            context.Status = status;
                        }
                    }
                    break;

                case "starttime":
                    // StartTime is read-only and set in constructor, skip it
                    _ = reader.GetDateTime();
                    break;

                case "endtime":
                    context.EndTime = reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();
                    break;

                case "variables":
                    // Populate the existing Variables dictionary
                    var variables = dictionaryConverter.Read(ref reader, typeof(System.Collections.Concurrent.ConcurrentDictionary<string, object>), options);
                    if (variables != null)
                    {
                        foreach (var kvp in variables)
                        {
                            context.Variables[kvp.Key] = kvp.Value;
                        }
                    }
                    break;

                case "nodequeues":
                    // Populate the existing NodeQueues dictionary
                    var nodeQueues = dictionaryConverter.Read(ref reader, typeof(System.Collections.Concurrent.ConcurrentDictionary<string, object>), options);
                    if (nodeQueues != null)
                    {
                        foreach (var kvp in nodeQueues)
                        {
                            context.NodeQueues[kvp.Key] = kvp.Value;
                        }
                    }
                    break;

                default:
                    // Skip unknown properties
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        WorkflowExecutionContext value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write all properties
        writer.WriteString(GetPropertyName("InstanceId", options), value.InstanceId);
        writer.WriteString(GetPropertyName("GraphId", options), value.GraphId);
        writer.WriteString(GetPropertyName("WorkflowId", options), value.WorkflowId);
        writer.WriteString(GetPropertyName("Status", options), value.Status.ToString().ToLowerInvariant());
        writer.WriteString(GetPropertyName("StartTime", options), value.StartTime);

        if (value.EndTime.HasValue)
        {
            writer.WriteString(GetPropertyName("EndTime", options), value.EndTime.Value);
        }
        else
        {
            writer.WriteNull(GetPropertyName("EndTime", options));
        }

        // Write Variables
        writer.WritePropertyName(GetPropertyName("Variables", options));
        dictionaryConverter.Write(writer, value.Variables, options);

        // Write NodeQueues
        writer.WritePropertyName(GetPropertyName("NodeQueues", options));
        dictionaryConverter.Write(writer, value.NodeQueues, options);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Gets the property name with the correct naming policy applied.
    /// </summary>
    private static string GetPropertyName(string propertyName, JsonSerializerOptions options)
    {
        return options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
    }
}
