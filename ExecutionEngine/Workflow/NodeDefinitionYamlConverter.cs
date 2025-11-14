// -----------------------------------------------------------------------
// <copyright file="NodeDefinitionYamlConverter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System;
using System.Collections.Generic;
using ExecutionEngine.Factory;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

/// <summary>
/// Custom YAML type converter that ensures nested node structures (like ChildNodes in Container)
/// are properly deserialized as List&lt;NodeDefinition&gt; instead of List&lt;Dictionary&gt;.
/// This eliminates the need for double serialization (Dict → JSON → NodeDefinition).
/// </summary>
public class NodeDefinitionYamlConverter : IYamlTypeConverter
{
    private readonly IDeserializer nestedDeserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeDefinitionYamlConverter"/> class.
    /// </summary>
    /// <param name="deserializer">The nested deserializer to use for converting YAML to objects.</param>
    public NodeDefinitionYamlConverter(IDeserializer deserializer)
    {
        this.nestedDeserializer = deserializer;
    }

    /// <inheritdoc/>
    public bool Accepts(Type type)
    {
        // Handle Dictionary<string, object> which is used for Configuration
        return type == typeof(Dictionary<string, object>);
    }

    /// <inheritdoc/>
    public object? ReadYaml(IParser parser, Type type)
    {
        // Deserialize as a generic dictionary first
        var dict = this.nestedDeserializer.Deserialize<Dictionary<object, object>>(parser);

        if (dict == null)
        {
            return null;
        }

        var result = new Dictionary<string, object>();

        foreach (var kvp in dict)
        {
            var key = kvp.Key?.ToString() ?? string.Empty;
            var value = kvp.Value;

            // Special handling for known nested structure keys
            if (key == "ChildNodes" && value is List<object> childNodesList)
            {
                // Convert list of dictionaries to list of NodeDefinitions
                result[key] = this.ConvertToNodeDefinitions(childNodesList);
            }
            else if (key == "ChildConnections" && value is List<object> childConnsList)
            {
                // Convert list of dictionaries to list of NodeConnections
                result[key] = this.ConvertToNodeConnections(childConnsList);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        // Use default serialization for writing
        throw new NotImplementedException("Serialization is handled by default serializer");
    }

    private List<NodeDefinition> ConvertToNodeDefinitions(List<object> items)
    {
        var result = new List<NodeDefinition>();

        foreach (var item in items)
        {
            if (item is Dictionary<object, object> dict)
            {
                var nodeDef = this.DictionaryToNodeDefinition(dict);
                if (nodeDef != null)
                {
                    result.Add(nodeDef);
                }
            }
        }

        return result;
    }

    private NodeDefinition? DictionaryToNodeDefinition(Dictionary<object, object> dict)
    {
        var nodeDef = new NodeDefinition();

        foreach (var kvp in dict)
        {
            var key = kvp.Key?.ToString();
            var value = kvp.Value;

            switch (key?.ToLowerInvariant())
            {
                case "nodeid":
                    nodeDef.NodeId = value?.ToString() ?? string.Empty;
                    break;
                case "nodename":
                    nodeDef.NodeName = value?.ToString() ?? string.Empty;
                    break;
                case "description":
                    nodeDef.Description = value?.ToString();
                    break;
                case "type":
                    nodeDef.Type = value?.ToString() ?? string.Empty;
                    break;
                case "runtimetype":
                    if (Enum.TryParse<Enums.RuntimeType>(value?.ToString(), true, out var runtimeType))
                    {
                        nodeDef.RuntimeType = runtimeType;
                    }
                    break;
                case "assemblypath":
                    nodeDef.AssemblyPath = value?.ToString();
                    break;
                case "typename":
                    nodeDef.TypeName = value?.ToString();
                    break;
                case "scriptpath":
                    nodeDef.ScriptPath = value?.ToString();
                    break;
                case "requiredmodules":
                    if (value is List<object> modulesList)
                    {
                        nodeDef.RequiredModules = modulesList.Select(m => m?.ToString() ?? string.Empty).ToList();
                    }
                    break;
                case "modulepaths":
                    if (value is Dictionary<object, object> modulePaths)
                    {
                        nodeDef.ModulePaths = modulePaths.ToDictionary(
                            k => k.Key?.ToString() ?? string.Empty,
                            v => v.Value?.ToString() ?? string.Empty);
                    }
                    break;
                case "configuration":
                    if (value is Dictionary<object, object> configDict)
                    {
                        // Recursively handle nested configuration
                        nodeDef.Configuration = this.ConvertConfiguration(configDict);
                    }
                    break;
                case "jointype":
                    if (Enum.TryParse<Enums.JoinType>(value?.ToString(), true, out var joinType))
                    {
                        nodeDef.JoinType = joinType;
                    }
                    break;
                case "priority":
                    if (Enum.TryParse<Enums.NodePriority>(value?.ToString(), true, out var priority))
                    {
                        nodeDef.Priority = priority;
                    }
                    break;
                case "maxconcurrentexecutions":
                    if (int.TryParse(value?.ToString(), out var maxConcurrent))
                    {
                        nodeDef.MaxConcurrentExecutions = maxConcurrent;
                    }
                    break;
                case "compensationnodeid":
                    nodeDef.CompensationNodeId = value?.ToString();
                    break;
                case "fallbacknodeid":
                    nodeDef.FallbackNodeId = value?.ToString();
                    break;
            }
        }

        return nodeDef;
    }

    private Dictionary<string, object> ConvertConfiguration(Dictionary<object, object> dict)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in dict)
        {
            var key = kvp.Key?.ToString() ?? string.Empty;
            var value = kvp.Value;

            // Recursively handle ChildNodes within configuration
            if (key == "ChildNodes" && value is List<object> childNodesList)
            {
                result[key] = this.ConvertToNodeDefinitions(childNodesList);
            }
            else if (key == "ChildConnections" && value is List<object> childConnsList)
            {
                result[key] = this.ConvertToNodeConnections(childConnsList);
            }
            else
            {
                result[key] = value;
            }
        }

        return result;
    }

    private List<NodeConnection> ConvertToNodeConnections(List<object> items)
    {
        var result = new List<NodeConnection>();

        foreach (var item in items)
        {
            if (item is Dictionary<object, object> dict)
            {
                var conn = this.DictionaryToNodeConnection(dict);
                if (conn != null)
                {
                    result.Add(conn);
                }
            }
        }

        return result;
    }

    private NodeConnection? DictionaryToNodeConnection(Dictionary<object, object> dict)
    {
        string? sourceNodeId = null;
        string? targetNodeId = null;
        var triggerMessageType = Enums.MessageType.Complete;
        var isEnabled = true;

        foreach (var kvp in dict)
        {
            var key = kvp.Key?.ToString();
            var value = kvp.Value;

            switch (key?.ToLowerInvariant())
            {
                case "sourcenodeid":
                    sourceNodeId = value?.ToString();
                    break;
                case "targetnodeid":
                    targetNodeId = value?.ToString();
                    break;
                case "triggermessagetype":
                    if (Enum.TryParse<Enums.MessageType>(value?.ToString(), true, out var msgType))
                    {
                        triggerMessageType = msgType;
                    }
                    break;
                case "isenabled":
                    // Handle both boolean and string "true"/"false"
                    if (value is bool boolValue)
                    {
                        isEnabled = boolValue;
                    }
                    else if (bool.TryParse(value?.ToString(), out var parsedBool))
                    {
                        isEnabled = parsedBool;
                    }
                    break;
            }
        }

        if (string.IsNullOrEmpty(sourceNodeId) || string.IsNullOrEmpty(targetNodeId))
        {
            return null;
        }

        return new NodeConnection
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            TriggerMessageType = triggerMessageType,
            IsEnabled = isEnabled
        };
    }
}
