// -----------------------------------------------------------------------
// <copyright file="ProgressUpdate.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Represents a real-time progress snapshot of workflow execution.
/// Published as an observable stream for monitoring and UI updates.
/// </summary>
public class ProgressUpdate
{
    /// <summary>
    /// Gets or sets the workflow instance ID.
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the overall completion percentage (0-100).
    /// </summary>
    public double PercentComplete { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes that have completed successfully.
    /// </summary>
    public int NodesCompleted { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes currently running.
    /// </summary>
    public int NodesRunning { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes pending execution.
    /// </summary>
    public int NodesPending { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes that have failed.
    /// </summary>
    public int NodesFailed { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes that were cancelled.
    /// </summary>
    public int NodesCancelled { get; set; }

    /// <summary>
    /// Gets or sets the total number of nodes in the workflow.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Gets or sets the estimated time remaining for workflow completion.
    /// Calculated based on average node execution time.
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this progress update was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
