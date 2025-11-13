// -----------------------------------------------------------------------
// <copyright file="ICheckpointStorage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

/// <summary>
/// Interface for persisting and retrieving workflow checkpoints.
/// Implementations can store checkpoints in memory, files, databases, etc.
/// </summary>
public interface ICheckpointStorage
{
    /// <summary>
    /// Saves a workflow checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the save operation.</returns>
    Task SaveCheckpointAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a workflow checkpoint by instance ID.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint, or null if not found.</returns>
    Task<WorkflowCheckpoint?> LoadCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workflow checkpoint.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all workflow checkpoints, optionally filtered by status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of all checkpoints.</returns>
    Task<IReadOnlyCollection<WorkflowCheckpoint>> ListCheckpointsAsync(CancellationToken cancellationToken = default);
}
