// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionStatus.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Represents the execution status of a workflow instance.
/// </summary>
public enum WorkflowExecutionStatus
{
    /// <summary>
    /// Workflow is pending execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Workflow is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Workflow completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Workflow execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Workflow execution was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Workflow execution is paused.
    /// </summary>
    Paused
}
