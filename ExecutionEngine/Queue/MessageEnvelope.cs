// -----------------------------------------------------------------------
// <copyright file="MessageEnvelope.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

using ExecutionEngine.Enums;

/// <summary>
/// Wraps a message with queue metadata for circular buffer processing.
/// Tracks status, retries, leases, and deduplication.
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// Gets or sets the unique message identifier.
    /// </summary>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the message type (full type name).
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message payload (serialized).
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Gets or sets the deduplication key for message replacement.
    /// </summary>
    public string? DeduplicationKey { get; set; }

    /// <summary>
    /// Gets or sets the message status in the queue.
    /// </summary>
    public MessageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the current retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the lease information if message is in-flight.
    /// </summary>
    public LeaseInfo? Lease { get; set; }

    /// <summary>
    /// Gets or sets the last persisted version (for state recovery).
    /// </summary>
    public long LastPersistedVersion { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was enqueued.
    /// </summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether this message has been superseded by a newer one.
    /// </summary>
    public bool IsSuperseded { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message becomes visible for processing.
    /// Used for implementing visibility timeout without Task.Run overhead.
    /// </summary>
    public DateTime? NotBefore { get; set; }
}
