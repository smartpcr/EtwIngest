// -----------------------------------------------------------------------
// <copyright file="NodeInstance.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Core;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;

/// <summary>
/// Represents a specific instance of node execution within a workflow.
/// Tracks the lifecycle and state of a single node execution.
/// </summary>
public class NodeInstance
{
    /// <summary>
    /// Gets or sets the unique identifier for this node instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the node ID from the workflow definition.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance ID this node belongs to.
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the execution status of this node instance.
    /// </summary>
    public NodeExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the start time of execution.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of execution.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the node execution context.
    /// </summary>
    public NodeExecutionContext? ExecutionContext { get; set; }

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception if execution failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the source port name for multi-port routing.
    /// Used for control flow nodes that can output messages on different named ports.
    /// If null or empty, indicates the default/primary output port.
    /// </summary>
    public string? SourcePort { get; set; }

    /// <summary>
    /// Gets the duration of node execution.
    /// </summary>
    public TimeSpan? Duration =>
        this.StartTime.HasValue && this.EndTime.HasValue
            ? this.EndTime.Value - this.StartTime.Value
            : null;
}
