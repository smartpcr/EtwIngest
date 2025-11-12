// -----------------------------------------------------------------------
// <copyright file="ICircularBuffer.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

/// <summary>
/// Interface for lock-free circular buffer message queue.
/// </summary>
public interface ICircularBuffer
{
    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the current count of messages in the buffer.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Enqueues a message into the buffer.
    /// If buffer is full, drops oldest Ready message.
    /// </summary>
    /// <param name="envelope">The message envelope to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enqueued successfully.</returns>
    Task<bool> EnqueueAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out (leases) a message of the specified type for processing.
    /// Transitions message from Ready to InFlight with lease expiry.
    /// </summary>
    /// <param name="messageType">The type of message to checkout.</param>
    /// <param name="handlerId">The ID of the handler checking out the message.</param>
    /// <param name="leaseDuration">How long the lease is valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The leased message envelope, or null if no message available.</returns>
    Task<MessageEnvelope?> CheckoutAsync(
        Type messageType,
        string handlerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges successful processing and removes the message from the buffer.
    /// </summary>
    /// <param name="messageId">The message ID to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if acknowledged successfully.</returns>
    Task<bool> AcknowledgeAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues a message for retry (transitions back to Ready, increments retry count).
    /// </summary>
    /// <param name="messageId">The message ID to requeue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if requeued successfully.</returns>
    Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing message with the same deduplication key.
    /// Marks old message as superseded and enqueues new message.
    /// </summary>
    /// <param name="envelope">The new message envelope.</param>
    /// <param name="deduplicationKey">The deduplication key to match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if replaced successfully.</returns>
    Task<bool> ReplaceAsync(
        MessageEnvelope envelope,
        string deduplicationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a message from the buffer by ID.
    /// </summary>
    /// <param name="messageId">The message ID to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if removed successfully.</returns>
    Task<bool> RemoveAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a message to the buffer (used for state recovery).
    /// Does not modify the envelope's status or lease.
    /// </summary>
    /// <param name="envelope">The message envelope to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if restored successfully.</returns>
    Task<bool> RestoreAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current count of messages asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of messages.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages in the buffer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of message envelopes.</returns>
    Task<MessageEnvelope[]> GetAllMessagesAsync(CancellationToken cancellationToken = default);
}
