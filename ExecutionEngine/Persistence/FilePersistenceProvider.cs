// -----------------------------------------------------------------------
// <copyright file="FilePersistenceProvider.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Text.Json;
using System.Text.Json.Serialization;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;

/// <summary>
/// File-based implementation of workflow state persistence.
/// Stores checkpoints as JSON files in a configurable directory.
/// </summary>
public class FilePersistenceProvider : IStatePersistence
{
    private readonly string checkpointDirectory;
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePersistenceProvider"/> class.
    /// </summary>
    /// <param name="checkpointDirectory">Directory path where checkpoints will be stored. Created if it doesn't exist.</param>
    public FilePersistenceProvider(string checkpointDirectory)
    {
        this.checkpointDirectory = checkpointDirectory ?? throw new ArgumentNullException(nameof(checkpointDirectory));

        // Create directory if it doesn't exist
        Directory.CreateDirectory(this.checkpointDirectory);

        // Configure JSON serialization options
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
                new WorkflowExecutionContextJsonConverter()
            }
        };
    }

    /// <inheritdoc/>
    public async Task<CheckpointMetadata> SaveCheckpointAsync(
        string checkpointId,
        Guid workflowInstanceId,
        WorkflowExecutionContext context,
        IEnumerable<NodeInstance> nodeInstances,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            throw new ArgumentException("Checkpoint ID cannot be null or empty", nameof(checkpointId));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (nodeInstances == null)
        {
            throw new ArgumentNullException(nameof(nodeInstances));
        }

        var nodeList = nodeInstances.ToList();
        var completedNodes = nodeList.Count(n => n.Status == Enums.NodeExecutionStatus.Completed);
        var pendingNodes = nodeList.Count(n => n.Status == Enums.NodeExecutionStatus.Pending);

        // Create checkpoint state
        var checkpoint = new CheckpointState
        {
            Metadata = new CheckpointMetadata
            {
                CheckpointId = checkpointId,
                WorkflowInstanceId = workflowInstanceId,
                WorkflowId = context.WorkflowId,
                Timestamp = DateTime.UtcNow,
                TotalNodes = nodeList.Count,
                CompletedNodes = completedNodes,
                PendingNodes = pendingNodes
            },
            Context = context,
            NodeInstances = nodeList
        };

        // Serialize to JSON
        var json = JsonSerializer.Serialize(checkpoint, this.jsonOptions);
        var filePath = this.GetCheckpointFilePath(checkpointId);

        // Write to file
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        // Update file size in metadata
        var fileInfo = new FileInfo(filePath);
        checkpoint.Metadata.SizeBytes = fileInfo.Length;

        return checkpoint.Metadata;
    }

    /// <inheritdoc/>
    public async Task<CheckpointState> LoadCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            throw new ArgumentException("Checkpoint ID cannot be null or empty", nameof(checkpointId));
        }

        var filePath = this.GetCheckpointFilePath(checkpointId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Checkpoint '{checkpointId}' not found at path: {filePath}");
        }

        // Read from file
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Deserialize
        var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json, this.jsonOptions);

        if (checkpoint == null)
        {
            throw new InvalidOperationException($"Failed to deserialize checkpoint '{checkpointId}'");
        }

        return checkpoint;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CheckpointMetadata>> ListCheckpointsAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var checkpoints = new List<CheckpointMetadata>();

        // Find all checkpoint files
        var files = Directory.GetFiles(this.checkpointDirectory, "*.checkpoint.json");

        foreach (var file in files)
        {
            try
            {
                // Read just the metadata (optimization: could read only first part of file)
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json, this.jsonOptions);

                if (checkpoint?.Metadata != null && checkpoint.Metadata.WorkflowInstanceId == workflowInstanceId)
                {
                    checkpoints.Add(checkpoint.Metadata);
                }
            }
            catch (Exception)
            {
                // Skip corrupted or invalid checkpoint files
                continue;
            }
        }

        // Return ordered by timestamp descending (most recent first)
        return checkpoints.OrderByDescending(c => c.Timestamp);
    }

    /// <inheritdoc/>
    public Task DeleteCheckpointAsync(
        string checkpointId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkpointId))
        {
            throw new ArgumentException("Checkpoint ID cannot be null or empty", nameof(checkpointId));
        }

        var filePath = this.GetCheckpointFilePath(checkpointId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task DeleteAllCheckpointsAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var checkpoints = await this.ListCheckpointsAsync(workflowInstanceId, cancellationToken);

        foreach (var checkpoint in checkpoints)
        {
            await this.DeleteCheckpointAsync(checkpoint.CheckpointId, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the full file path for a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID.</param>
    /// <returns>Full file path.</returns>
    private string GetCheckpointFilePath(string checkpointId)
    {
        // Sanitize checkpoint ID to prevent directory traversal
        var safeId = string.Join("_", checkpointId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(this.checkpointDirectory, $"{safeId}.checkpoint.json");
    }
}
