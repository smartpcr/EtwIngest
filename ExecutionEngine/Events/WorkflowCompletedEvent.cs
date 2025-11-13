// -----------------------------------------------------------------------
// <copyright file="WorkflowCompletedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

using ExecutionEngine.Enums;

/// <summary>
/// Event published when a workflow execution completes successfully.
/// </summary>
public class WorkflowCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the final workflow status.
    /// </summary>
    public WorkflowExecutionStatus Status { get; set; }
}
