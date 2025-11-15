// -----------------------------------------------------------------------
// <copyright file="InMemoryCheckpointStorage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Collections.Concurrent;

/// <summary>
/// In-memory implementation of ICheckpointStorage for testing and development.
/// Thread-safe using ConcurrentDictionary.
/// </summary>
public class InMemoryCheckpointStorage : ICheckpointStorage
{
    private readonly ConcurrentDictionary<Guid, WorkflowCheckpoint> checkpoints = new();

    /// <inheritdoc/>
    public Task SaveCheckpointAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));

        cancellationToken.ThrowIfCancellationRequested();

        // Create a defensive copy to prevent external modifications
        var copy = new WorkflowCheckpoint
        {
            WorkflowInstanceId = checkpoint.WorkflowInstanceId,
            WorkflowId = checkpoint.WorkflowId,
            Status = checkpoint.Status,
            StartTime = checkpoint.StartTime,
            CheckpointTime = checkpoint.CheckpointTime,
            Variables = new Dictionary<string, object>(checkpoint.Variables),
            NodeStates = new Dictionary<string, NodeInstanceState>(checkpoint.NodeStates),
            MessageQueues = new Dictionary<string, List<SerializedMessage>>(checkpoint.MessageQueues),
            ErrorMessages = new List<string>(checkpoint.ErrorMessages),
            Metadata = new Dictionary<string, string>(checkpoint.Metadata)
        };

        this.checkpoints.AddOrUpdate(checkpoint.WorkflowInstanceId, copy, (key, existing) => copy);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<WorkflowCheckpoint?> LoadCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (this.checkpoints.TryGetValue(workflowInstanceId, out var checkpoint))
        {
            // Return a defensive copy to prevent external modifications
            var copy = new WorkflowCheckpoint
            {
                WorkflowInstanceId = checkpoint.WorkflowInstanceId,
                WorkflowId = checkpoint.WorkflowId,
                Status = checkpoint.Status,
                StartTime = checkpoint.StartTime,
                CheckpointTime = checkpoint.CheckpointTime,
                Variables = new Dictionary<string, object>(checkpoint.Variables),
                NodeStates = new Dictionary<string, NodeInstanceState>(checkpoint.NodeStates),
                MessageQueues = new Dictionary<string, List<SerializedMessage>>(checkpoint.MessageQueues),
                ErrorMessages = new List<string>(checkpoint.ErrorMessages),
                Metadata = new Dictionary<string, string>(checkpoint.Metadata)
            };

            return Task.FromResult<WorkflowCheckpoint?>(copy);
        }

        return Task.FromResult<WorkflowCheckpoint?>(null);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var removed = this.checkpoints.TryRemove(workflowInstanceId, out _);
        return Task.FromResult(removed);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<WorkflowCheckpoint>> ListCheckpointsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Return defensive copies
        var checkpointList = this.checkpoints.Values.Select(checkpoint => new WorkflowCheckpoint
        {
            WorkflowInstanceId = checkpoint.WorkflowInstanceId,
            WorkflowId = checkpoint.WorkflowId,
            Status = checkpoint.Status,
            StartTime = checkpoint.StartTime,
            CheckpointTime = checkpoint.CheckpointTime,
            Variables = new Dictionary<string, object>(checkpoint.Variables),
            NodeStates = new Dictionary<string, NodeInstanceState>(checkpoint.NodeStates),
            MessageQueues = new Dictionary<string, List<SerializedMessage>>(checkpoint.MessageQueues),
            ErrorMessages = new List<string>(checkpoint.ErrorMessages),
            Metadata = new Dictionary<string, string>(checkpoint.Metadata)
        }).ToList();

        return Task.FromResult<IReadOnlyCollection<WorkflowCheckpoint>>(checkpointList);
    }
}
