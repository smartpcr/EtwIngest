// -----------------------------------------------------------------------
// <copyright file="WorkflowEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Base class for all workflow-related events.
/// Events are published as observable streams for reactive state management.
/// </summary>
public abstract class WorkflowEvent
{
    /// <summary>
    /// Gets or sets the workflow instance ID that generated this event.
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets optional metadata for this event.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
