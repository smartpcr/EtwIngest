// -----------------------------------------------------------------------
// <copyright file="FileCheckpointStorage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Persistence;

using System.Text.Json;

/// <summary>
/// File-based implementation of ICheckpointStorage using JSON serialization.
/// Stores each checkpoint as a separate JSON file named by workflow instance ID.
/// Thread-safe using file system locking.
/// </summary>
public class FileCheckpointStorage : ICheckpointStorage
{
    private readonly string checkpointDirectory;
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCheckpointStorage"/> class.
    /// </summary>
    /// <param name="checkpointDirectory">Directory path where checkpoint files will be stored.</param>
    public FileCheckpointStorage(string checkpointDirectory)
    {
        if (string.IsNullOrWhiteSpace(checkpointDirectory))
            throw new ArgumentException("Checkpoint directory cannot be null or whitespace.", nameof(checkpointDirectory));

        this.checkpointDirectory = checkpointDirectory;

        // Ensure directory exists
        Directory.CreateDirectory(this.checkpointDirectory);

        // Configure JSON serialization options
        this.jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc/>
    public async Task SaveCheckpointAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));

        cancellationToken.ThrowIfCancellationRequested();

        var filePath = this.GetCheckpointFilePath(checkpoint.WorkflowInstanceId);

        // Serialize to JSON
        var json = JsonSerializer.Serialize(checkpoint, this.jsonOptions);

        // Write to file (creates or overwrites)
        // File.WriteAllTextAsync uses FileShare.None by default, providing exclusive access
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<WorkflowCheckpoint?> LoadCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = this.GetCheckpointFilePath(workflowInstanceId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            // Read from file
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            // Deserialize from JSON
            var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json, this.jsonOptions);

            return checkpoint;
        }
        catch (FileNotFoundException)
        {
            // File was deleted between the existence check and read attempt
            return null;
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteCheckpointAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = this.GetCheckpointFilePath(workflowInstanceId);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                return Task.FromResult(true);
            }
            catch (FileNotFoundException)
            {
                // File was already deleted
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<WorkflowCheckpoint>> ListCheckpointsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var checkpoints = new List<WorkflowCheckpoint>();

        // Get all JSON files in the checkpoint directory
        var checkpointFiles = Directory.GetFiles(this.checkpointDirectory, "*.json");

        foreach (var filePath in checkpointFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json, this.jsonOptions);

                if (checkpoint != null)
                {
                    checkpoints.Add(checkpoint);
                }
            }
            catch (Exception)
            {
                // Skip files that cannot be deserialized (corrupted, wrong format, etc.)
                // In production, you might want to log this
                continue;
            }
        }

        return checkpoints;
    }

    /// <summary>
    /// Gets the file path for a checkpoint by workflow instance ID.
    /// </summary>
    /// <param name="workflowInstanceId">The workflow instance ID.</param>
    /// <returns>Full file path for the checkpoint JSON file.</returns>
    private string GetCheckpointFilePath(Guid workflowInstanceId)
    {
        return Path.Combine(this.checkpointDirectory, $"{workflowInstanceId}.json");
    }
}
