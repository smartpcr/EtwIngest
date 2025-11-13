// -----------------------------------------------------------------------
// <copyright file="WorkflowFailedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a workflow execution fails.
/// </summary>
public class WorkflowFailedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total execution duration before failure.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the error message describing why the workflow failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
