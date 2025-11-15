// -----------------------------------------------------------------------
// <copyright file="INode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Core;

using ExecutionEngine.Contexts;
using ExecutionEngine.Nodes.Definitions;

/// <summary>
/// Base interface for all workflow nodes.
/// Nodes are the fundamental units of execution in a workflow.
/// </summary>
public interface INode
{
    /// <summary>
    /// Gets the unique identifier for this node.
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// Gets the display name for this node.
    /// </summary>
    string NodeName { get; }

    /// <summary>
    /// Initializes the node with its definition.
    /// Called once after the node is created by the factory.
    /// </summary>
    /// <param name="definition">The node definition containing configuration.</param>
    void Initialize(NodeDefinition definition);

    /// <summary>
    /// Executes the node asynchronously.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The node instance representing the execution result.</returns>
    Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the list of available output ports for this node.
    /// Used for multi-port routing where a node can produce messages on different named ports.
    /// If not overridden, returns an empty array (single default port).
    /// </summary>
    /// <returns>Array of port names, or empty array for default single port.</returns>
    string[] GetAvailablePorts()
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Event raised when node starts execution.
    /// </summary>
    event EventHandler<NodeStartEventArgs>? OnStart;

    /// <summary>
    /// Event raised when node reports progress.
    /// </summary>
    event EventHandler<ProgressEventArgs>? OnProgress;

    /// <summary>
    /// Event raised when a loop node produces next iteration output.
    /// Used by ForEach/While nodes to emit iteration messages.
    /// </summary>
    event EventHandler<NodeNextEventArgs>? OnNext;
}

/// <summary>
/// Event arguments for node start event.
/// </summary>
public class NodeStartEventArgs : EventArgs
{
    public string NodeId { get; set; } = string.Empty;
    public Guid NodeInstanceId { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Event arguments for progress event.
/// </summary>
public class ProgressEventArgs : EventArgs
{
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
}

/// <summary>
/// Event arguments for node next (iteration) event.
/// </summary>
public class NodeNextEventArgs : EventArgs
{
    public string NodeId { get; set; } = string.Empty;
    public Guid NodeInstanceId { get; set; }
    public int IterationIndex { get; set; }
    public NodeExecutionContext? IterationContext { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
