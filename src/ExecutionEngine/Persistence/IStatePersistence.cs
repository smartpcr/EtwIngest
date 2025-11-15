// -----------------------------------------------------------------------
// <copyright file="IStatePersistence.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;

/// <summary>
/// Interface for persisting and restoring workflow execution state.
/// Enables checkpoint/restore functionality for long-running workflows.
/// </summary>
public interface IStatePersistence
{
    /// <summary>
    /// Saves a workflow execution checkpoint to persistent storage.
    /// </summary>
    /// <param name="checkpointId">Unique identifier for this checkpoint.</param>
    /// <param name="workflowInstanceId">The workflow instance being checkpointed.</param>
    /// <param name="context">The workflow execution context to save.</param>
    /// <param name="nodeInstances">The collection of node instances and their states.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the saved checkpoint.</returns>
    Task<CheckpointMetadata> SaveCheckpointAsync(
        string checkpointId,
        Guid workflowInstanceId,
        WorkflowExecutionContext context,
        IEnumerable<NodeInstance> nodeInstances,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a workflow execution checkpoint from persistent storage.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The restored checkpoint state.</returns>
    Task<CheckpointState> LoadCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all available checkpoints for a workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of checkpoint metadata ordered by timestamp descending.</returns>
    Task<IEnumerable<CheckpointMetadata>> ListCheckpointsAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific checkpoint from persistent storage.
    /// </summary>
    /// <param name="checkpointId">The checkpoint identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all checkpoints for a workflow instance.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllCheckpointsAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about a saved checkpoint.
/// </summary>
public class CheckpointMetadata
{
    /// <summary>
    /// Gets or sets the unique checkpoint identifier.
    /// </summary>
    public string CheckpointId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow instance ID.
    /// </summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the workflow definition ID.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the checkpoint was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the total number of nodes in the workflow.
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// Gets or sets the number of completed nodes at checkpoint time.
    /// </summary>
    public int CompletedNodes { get; set; }

    /// <summary>
    /// Gets or sets the number of pending nodes at checkpoint time.
    /// </summary>
    public int PendingNodes { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes (for file-based persistence).
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets optional description or reason for checkpoint.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Represents the complete state of a workflow checkpoint.
/// </summary>
public class CheckpointState
{
    /// <summary>
    /// Gets or sets the checkpoint metadata.
    /// </summary>
    public CheckpointMetadata Metadata { get; set; } = new CheckpointMetadata();

    /// <summary>
    /// Gets or sets the workflow execution context.
    /// </summary>
    public WorkflowExecutionContext Context { get; set; } = new WorkflowExecutionContext();

    /// <summary>
    /// Gets or sets the node instances and their execution states.
    /// </summary>
    public List<NodeInstance> NodeInstances { get; set; } = new List<NodeInstance>();
}
