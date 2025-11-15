// -----------------------------------------------------------------------
// <copyright file="ExecutionState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Core;

using System.Collections.Concurrent;
using ExecutionEngine.Contexts;

/// <summary>
/// Shared execution state contract accessible by both C# and PowerShell scripts.
/// Provides unified access to workflow context, node context, and helper functions.
/// </summary>
public class ExecutionState
{
    /// <summary>
    /// Gets or sets the workflow execution context.
    /// </summary>
    public WorkflowExecutionContext WorkflowContext { get; set; } = null!;

    /// <summary>
    /// Gets or sets the node execution context.
    /// </summary>
    public NodeExecutionContext NodeContext { get; set; } = null!;

    /// <summary>
    /// Gets or sets the workflow-level global variables.
    /// </summary>
    public ConcurrentDictionary<string, object> GlobalVariables { get; set; } = new();

    /// <summary>
    /// Gets or sets the input data from previous node.
    /// </summary>
    public Dictionary<string, object> Input { get; set; } = new();

    /// <summary>
    /// Gets or sets the local variables for this node.
    /// </summary>
    public ConcurrentDictionary<string, object> Local { get; set; } = new();

    /// <summary>
    /// Gets or sets the output data for downstream nodes.
    /// </summary>
    public Dictionary<string, object> Output { get; set; } = new();

    /// <summary>
    /// Helper function to set output value.
    /// </summary>
    public Action<string, object> SetOutput { get; set; } = null!;

    /// <summary>
    /// Helper function to get input value.
    /// </summary>
    public Func<string, object?> GetInput { get; set; } = null!;

    /// <summary>
    /// Helper function to get global variable value.
    /// </summary>
    public Func<string, object?> GetGlobal { get; set; } = null!;

    /// <summary>
    /// Helper function to set global variable value.
    /// </summary>
    public Action<string, object> SetGlobal { get; set; } = null!;
}
