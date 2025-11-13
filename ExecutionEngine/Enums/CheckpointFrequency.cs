// -----------------------------------------------------------------------
// <copyright file="CheckpointFrequency.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Defines when checkpoints should be created during workflow execution.
/// </summary>
public enum CheckpointFrequency
{
    /// <summary>
    /// Never create checkpoints automatically.
    /// </summary>
    Never = 0,

    /// <summary>
    /// Create a checkpoint after each node completes.
    /// </summary>
    AfterEachNode = 1,

    /// <summary>
    /// Create a checkpoint after every N nodes complete.
    /// </summary>
    AfterNNodes = 2,

    /// <summary>
    /// Create a checkpoint at regular time intervals.
    /// </summary>
    TimeInterval = 3,

    /// <summary>
    /// Create checkpoints only when manually triggered.
    /// </summary>
    Manual = 4
}
