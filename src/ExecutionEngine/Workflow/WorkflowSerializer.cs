// -----------------------------------------------------------------------
// <copyright file="WorkflowSerializer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Nodes.Definitions.Converters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Provides serialization and deserialization for workflow definitions.
/// Supports both JSON and YAML formats.
/// </summary>
public class WorkflowSerializer
{
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ISerializer yamlSerializer;
    private readonly IDeserializer yamlDeserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowSerializer"/> class.
    /// </summary>
    public WorkflowSerializer()
    {
        // Configure JSON serialization
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ReflectionJsonConverter<NoopNodeDefinition>(),
                new ReflectionJsonConverter<CSharpNodeDefinition>(),
                new ReflectionJsonConverter<CSharpScriptNodeDefinition>(),
                new ReflectionJsonConverter<CSharpTaskNodeDefinition>(),
                new ReflectionJsonConverter<PowerShellScriptNodeDefinition>(),
                new ReflectionJsonConverter<PowerShellTaskNodeDefinition>(),
                new ReflectionJsonConverter<IfElseNodeDefinition>(),
                new ReflectionJsonConverter<ForEachNodeDefinition>(),
                new ReflectionJsonConverter<WhileNodeDefinition>(),
                new ReflectionJsonConverter<SwitchNodeDefinition>(),
                new ReflectionJsonConverter<SubflowNodeDefinition>(),
                new ReflectionJsonConverter<TimerNodeDefinition>(),
                new ReflectionJsonConverter<ContainerNodeDefinition>()
            }
        };

        // YAML serialization
        this.yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new ReflectionYamlConverter<NoopNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpScriptNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpTaskNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<PowerShellScriptNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<PowerShellTaskNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<IfElseNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<ForEachNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<WhileNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<SwitchNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<SubflowNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<TimerNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<ContainerNodeDefinition>())
            .Build();
        this.yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new ReflectionYamlConverter<NoopNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpScriptNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<CSharpTaskNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<PowerShellScriptNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<PowerShellTaskNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<IfElseNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<ForEachNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<WhileNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<SwitchNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<SubflowNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<TimerNodeDefinition>())
            .WithTypeConverter(new ReflectionYamlConverter<ContainerNodeDefinition>())
            .Build();
    }

    /// <summary>
    /// Serializes a workflow definition to JSON string.
    /// </summary>
    /// <param name="workflow">The workflow definition to serialize.</param>
    /// <returns>JSON string representation of the workflow.</returns>
    public string ToJson(WorkflowDefinition workflow)
    {
        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        return JsonSerializer.Serialize(workflow, this.jsonOptions);
    }

    /// <summary>
    /// Deserializes a workflow definition from JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized workflow definition.</returns>
    public WorkflowDefinition FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json, this.jsonOptions);
        if (workflow == null)
        {
            throw new InvalidOperationException("Failed to deserialize workflow from JSON.");
        }

        return workflow;
    }

    /// <summary>
    /// Serializes a workflow definition to YAML string.
    /// </summary>
    /// <param name="workflow">The workflow definition to serialize.</param>
    /// <returns>YAML string representation of the workflow.</returns>
    public string ToYaml(WorkflowDefinition workflow)
    {
        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        return this.yamlSerializer.Serialize(workflow);
    }

    /// <summary>
    /// Deserializes a workflow definition from YAML string.
    /// </summary>
    /// <param name="yaml">The YAML string to deserialize.</param>
    /// <returns>The deserialized workflow definition.</returns>
    public WorkflowDefinition FromYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException("YAML string cannot be null or empty.", nameof(yaml));
        }

        var workflow = this.yamlDeserializer.Deserialize<WorkflowDefinition>(yaml);
        if (workflow == null)
        {
            throw new InvalidOperationException("Failed to deserialize workflow from YAML.");
        }

        return workflow;
    }

    /// <summary>
    /// Saves a workflow definition to a file.
    /// Format is determined by file extension (.json or .yaml/.yml).
    /// </summary>
    /// <param name="workflow">The workflow definition to save.</param>
    /// <param name="filePath">The file path where the workflow should be saved.</param>
    public void SaveToFile(WorkflowDefinition workflow, string filePath)
    {
        if (workflow == null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        string content;

        switch (extension)
        {
            case ".json":
                content = this.ToJson(workflow);
                break;

            case ".yaml":
            case ".yml":
                content = this.ToYaml(workflow);
                break;

            default:
                throw new NotSupportedException($"File extension '{extension}' is not supported. Use .json, .yaml, or .yml");
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, content);
    }

    /// <summary>
    /// Loads a workflow definition from a file.
    /// Format is determined by file extension (.json or .yaml/.yml).
    /// </summary>
    /// <param name="filePath">The file path to load the workflow from.</param>
    /// <returns>The loaded workflow definition.</returns>
    public WorkflowDefinition LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Workflow file not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var content = File.ReadAllText(filePath);

        return extension switch
        {
            ".json" => this.FromJson(content),
            ".yaml" or ".yml" => this.FromYaml(content),
            _ => throw new NotSupportedException($"File extension '{extension}' is not supported. Use .json, .yaml, or .yml")
        };
    }
}
