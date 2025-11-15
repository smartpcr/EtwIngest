// -----------------------------------------------------------------------
// <copyright file="WorkflowDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System.Collections.Generic;
using ExecutionEngine.Factory;

/// <summary>
/// Defines a workflow as a directed graph of nodes and connections.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for the workflow.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable name of the workflow.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the workflow.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the version of the workflow definition.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the collection of node definitions in the workflow.
    /// </summary>
    public List<NodeDefinition> Nodes { get; set; } = new List<NodeDefinition>();

    /// <summary>
    /// Gets or sets the collection of connections between nodes.
    /// </summary>
    public List<NodeConnection> Connections { get; set; } = new List<NodeConnection>();

    /// <summary>
    /// Gets or sets the metadata for the workflow.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the entry point node ID (optional - if not specified, nodes without incoming connections are entry points).
    /// </summary>
    public string? EntryPointNodeId { get; set; }

    /// <summary>
    /// Gets or sets the maximum concurrent node executions allowed (0 = unlimited).
    /// </summary>
    public int MaxConcurrency { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether the workflow execution can be paused.
    /// </summary>
    public bool AllowPause { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout in seconds for the entire workflow execution (0 = no timeout).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Gets or sets the default variables for the workflow that are available to all nodes.
    /// </summary>
    public Dictionary<string, object>? DefaultVariables { get; set; }
}
