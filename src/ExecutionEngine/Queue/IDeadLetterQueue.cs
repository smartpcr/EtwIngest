// -----------------------------------------------------------------------
// <copyright file="IDeadLetterQueue.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

/// <summary>
/// Interface for storing messages that failed processing and exceeded retry limits.
/// Provides diagnostics and manual reprocessing capabilities.
/// </summary>
public interface IDeadLetterQueue
{
    /// <summary>
    /// Gets the current count of dead letter entries.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum size of the dead letter queue.
    /// </summary>
    int MaxSize { get; }

    /// <summary>
    /// Adds a failed message envelope to the dead letter queue.
    /// </summary>
    /// <param name="envelope">The message envelope that failed.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    /// <returns>True if added successfully.</returns>
    Task<bool> AddAsync(MessageEnvelope envelope, string reason, Exception? exception = null);

    /// <summary>
    /// Gets all dead letter entries.
    /// </summary>
    /// <returns>Array of dead letter entries.</returns>
    Task<DeadLetterEntry[]> GetAllEntriesAsync();

    /// <summary>
    /// Gets a specific dead letter entry by ID.
    /// </summary>
    /// <param name="entryId">The entry ID to retrieve.</param>
    /// <returns>The dead letter entry, or null if not found.</returns>
    Task<DeadLetterEntry?> GetEntryAsync(Guid entryId);

    /// <summary>
    /// Clears all entries from the dead letter queue.
    /// </summary>
    /// <returns>The number of entries cleared.</returns>
    Task<int> ClearAsync();

    /// <summary>
    /// Removes a specific entry from the dead letter queue.
    /// </summary>
    /// <param name="entryId">The entry ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    Task<bool> RemoveEntryAsync(Guid entryId);
}
