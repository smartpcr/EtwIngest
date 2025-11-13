// -----------------------------------------------------------------------
// <copyright file="WorkflowCancelledEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a workflow execution is cancelled.
/// </summary>
public class WorkflowCancelledEvent : WorkflowEvent
{
    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total execution duration before cancellation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
