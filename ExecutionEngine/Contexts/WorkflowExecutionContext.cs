// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionContext.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Contexts;

using System.Collections.Concurrent;
using ExecutionEngine.Enums;

/// <summary>
/// Represents the execution context for an entire workflow instance.
/// Contains workflow-level state, per-node message queues, and routing infrastructure.
/// </summary>
public class WorkflowExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the WorkflowExecutionContext class.
    /// </summary>
    public WorkflowExecutionContext()
    {
        this.InstanceId = Guid.NewGuid();
        this.StartTime = DateTime.UtcNow;
        this.Status = WorkflowExecutionStatus.Pending;
    }

    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid InstanceId { get; }

    /// <summary>
    /// Gets the workflow graph ID.
    /// </summary>
    public string GraphId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow execution status.
    /// </summary>
    public WorkflowExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets the workflow start time.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets or sets the workflow end time.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the workflow-level global variables.
    /// Accessible by all nodes in the workflow.
    /// </summary>
    public ConcurrentDictionary<string, object> Variables { get; } = new();

    /// <summary>
    /// Gets the per-node message queues.
    /// Each node has its own dedicated queue for message isolation.
    /// Key: NodeId, Value: NodeMessageQueue
    /// </summary>
    public ConcurrentDictionary<string, object> NodeQueues { get; } = new();

    /// <summary>
    /// Gets or sets the message router for routing messages to target node queues.
    /// </summary>
    public object? Router { get; set; }

    /// <summary>
    /// Gets or sets the dead letter queue for failed messages.
    /// </summary>
    public object? DeadLetterQueue { get; set; }

    /// <summary>
    /// Gets the duration of workflow execution.
    /// </summary>
    public TimeSpan? Duration => this.EndTime.HasValue ? this.EndTime.Value - this.StartTime : null;
}
