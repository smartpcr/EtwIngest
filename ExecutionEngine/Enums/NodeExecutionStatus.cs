// -----------------------------------------------------------------------
// <copyright file="NodeExecutionStatus.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Represents the execution status of a node instance.
/// </summary>
public enum NodeExecutionStatus
{
    /// <summary>
    /// Node is waiting to start execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Node is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Node completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Node execution failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Node execution was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Node execution was skipped (e.g., conditional branch not taken).
    /// </summary>
    Skipped
}
