// -----------------------------------------------------------------------
// <copyright file="DeadLetterQueue.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Stores messages that failed processing and exceeded retry limits.
/// Provides diagnostics and manual reprocessing capabilities.
/// </summary>
public class DeadLetterQueue : IDeadLetterQueue
{
    private readonly ILogger<DeadLetterQueue> logger;
    private readonly ConcurrentQueue<DeadLetterEntry> entries;
    private readonly int maxSize;
    private int currentSize;

    /// <summary>
    /// Initializes a new instance of the DeadLetterQueue class.
    /// </summary>
    /// <param name="maxSize">Maximum number of entries to retain (default 1000).</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public DeadLetterQueue(int maxSize = 1000, ILogger<DeadLetterQueue>? logger = null)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be greater than zero.");
        }

        this.logger = logger ?? NullLogger<DeadLetterQueue>.Instance;
        this.maxSize = maxSize;
        this.entries = new ConcurrentQueue<DeadLetterEntry>();
        this.currentSize = 0;
    }

    /// <summary>
    /// Gets the current count of dead letter entries.
    /// </summary>
    public int Count => this.currentSize;

    /// <summary>
    /// Gets the maximum size of the dead letter queue.
    /// </summary>
    public int MaxSize => this.maxSize;

    /// <summary>
    /// Adds a failed message envelope to the dead letter queue.
    /// </summary>
    /// <param name="envelope">The message envelope that failed.</param>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    /// <returns>True if added successfully.</returns>
    public Task<bool> AddAsync(MessageEnvelope envelope, string reason, Exception? exception = null)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be null or whitespace.", nameof(reason));
        }

        // If queue is at max size, dequeue oldest entry
        if (this.currentSize >= this.maxSize)
        {
            if (this.entries.TryDequeue(out var oldestEntry))
            {
                Interlocked.Decrement(ref this.currentSize);
                this.logger.LogWarning("Dead letter queue is full ({MaxSize}), removing oldest entry {OldestEntryId}",
                    this.maxSize, oldestEntry.EntryId);
            }
        }

        var entry = new DeadLetterEntry
        {
            Envelope = envelope,
            Reason = reason,
            Exception = exception,
            Timestamp = DateTime.UtcNow,
            EntryId = Guid.NewGuid()
        };

        this.entries.Enqueue(entry);
        Interlocked.Increment(ref this.currentSize);

        if (exception != null)
        {
            this.logger.LogError(exception, "Message added to dead letter queue. Reason: {Reason}, MessageId: {MessageId}, MessageType: {MessageType}",
                reason, envelope.MessageId, envelope.MessageType);
        }
        else
        {
            this.logger.LogWarning("Message added to dead letter queue. Reason: {Reason}, MessageId: {MessageId}, MessageType: {MessageType}",
                reason, envelope.MessageId, envelope.MessageType);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets all dead letter entries.
    /// </summary>
    /// <returns>Array of dead letter entries.</returns>
    public Task<DeadLetterEntry[]> GetAllEntriesAsync()
    {
        return Task.FromResult(this.entries.ToArray());
    }

    /// <summary>
    /// Gets a specific dead letter entry by ID.
    /// </summary>
    /// <param name="entryId">The entry ID to retrieve.</param>
    /// <returns>The dead letter entry, or null if not found.</returns>
    public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId)
    {
        var entry = this.entries.FirstOrDefault(e => e.EntryId == entryId);
        return Task.FromResult(entry);
    }

    /// <summary>
    /// Clears all entries from the dead letter queue.
    /// </summary>
    /// <returns>The number of entries cleared.</returns>
    public Task<int> ClearAsync()
    {
        var count = 0;
        while (this.entries.TryDequeue(out _))
        {
            count++;
        }

        this.currentSize = 0;
        this.logger.LogInformation("Cleared {Count} entries from dead letter queue", count);
        return Task.FromResult(count);
    }

    /// <summary>
    /// Removes a specific entry from the dead letter queue.
    /// </summary>
    /// <param name="entryId">The entry ID to remove.</param>
    /// <returns>True if removed successfully.</returns>
    public Task<bool> RemoveEntryAsync(Guid entryId)
    {
        var allEntries = this.entries.ToArray();
        var entryToRemove = allEntries.FirstOrDefault(e => e.EntryId == entryId);

        if (entryToRemove == null)
        {
            this.logger.LogWarning("Failed to remove dead letter entry {EntryId}: Entry not found", entryId);
            return Task.FromResult(false);
        }

        // Create a new queue without the removed entry
        var newQueue = new ConcurrentQueue<DeadLetterEntry>();
        foreach (var entry in allEntries)
        {
            if (entry.EntryId != entryId)
            {
                newQueue.Enqueue(entry);
            }
        }

        // Clear current queue and add all entries back except the removed one
        while (this.entries.TryDequeue(out _))
        {
        }

        foreach (var entry in newQueue)
        {
            this.entries.Enqueue(entry);
        }

        Interlocked.Decrement(ref this.currentSize);
        this.logger.LogInformation("Removed dead letter entry {EntryId} from queue", entryId);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Represents an entry in the dead letter queue.
/// </summary>
public class DeadLetterEntry
{
    /// <summary>
    /// Gets or sets the unique identifier for this entry.
    /// </summary>
    public Guid EntryId { get; set; }

    /// <summary>
    /// Gets or sets the failed message envelope.
    /// </summary>
    public MessageEnvelope Envelope { get; set; } = null!;

    /// <summary>
    /// Gets or sets the reason for failure.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception that caused the failure.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this entry was added.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
