// -----------------------------------------------------------------------
// <copyright file="WorkflowStartedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a workflow execution starts.
/// </summary>
public class WorkflowStartedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of nodes in the workflow.
    /// </summary>
    public int TotalNodes { get; set; }
}
