// -----------------------------------------------------------------------
// <copyright file="NodeMessageQueue.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue;

using ExecutionEngine.Messages;

/// <summary>
/// Per-node message queue that wraps CircularBuffer with type-safe message operations.
/// Each node has its own dedicated queue for message isolation.
/// </summary>
public class NodeMessageQueue
{
    private readonly ICircularBuffer buffer;
    private readonly string nodeId;

    /// <summary>
    /// Initializes a new instance of the NodeMessageQueue class.
    /// </summary>
    /// <param name="nodeId">The node ID this queue belongs to.</param>
    /// <param name="capacity">The maximum capacity of the queue.</param>
    public NodeMessageQueue(string nodeId, int capacity = 100)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node ID cannot be null or whitespace.", nameof(nodeId));
        }

        this.nodeId = nodeId;
        this.buffer = new CircularBuffer(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the NodeMessageQueue class with a custom buffer.
    /// </summary>
    /// <param name="nodeId">The node ID this queue belongs to.</param>
    /// <param name="buffer">The circular buffer implementation.</param>
    public NodeMessageQueue(string nodeId, ICircularBuffer buffer)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node ID cannot be null or whitespace.", nameof(nodeId));
        }

        this.nodeId = nodeId;
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    /// <summary>
    /// Gets the node ID this queue belongs to.
    /// </summary>
    public string NodeId => this.nodeId;

    /// <summary>
    /// Gets the current count of messages in the queue.
    /// </summary>
    public int Count => this.buffer.Count;

    /// <summary>
    /// Gets the maximum capacity of the queue.
    /// </summary>
    public int Capacity => this.buffer.Capacity;

    /// <summary>
    /// Enqueues a node message into the queue.
    /// </summary>
    /// <param name="message">The node message to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if enqueued successfully.</returns>
    public async Task<bool> EnqueueAsync(INodeMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var envelope = new MessageEnvelope
        {
            MessageId = message.MessageId,
            MessageType = message.GetType().FullName ?? message.GetType().Name,
            Payload = message,
            EnqueuedAt = DateTime.UtcNow
        };

        return await this.buffer.EnqueueAsync(envelope, cancellationToken);
    }

    /// <summary>
    /// Checks out a message of the specified type for processing.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to checkout.</typeparam>
    /// <param name="handlerId">The ID of the handler checking out the message.</param>
    /// <param name="leaseDuration">How long the lease is valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message, or null if no message available.</returns>
    public async Task<TMessage?> CheckoutAsync<TMessage>(
        string handlerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
        where TMessage : class, INodeMessage
    {
        var envelope = await this.buffer.CheckoutAsync(
            typeof(TMessage),
            handlerId,
            leaseDuration,
            cancellationToken);

        if (envelope?.Payload is TMessage message)
        {
            return message;
        }

        return null;
    }

    /// <summary>
    /// Acknowledges successful processing and removes the message from the queue.
    /// </summary>
    /// <param name="messageId">The message ID to acknowledge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if acknowledged successfully.</returns>
    public async Task<bool> AcknowledgeAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await this.buffer.AcknowledgeAsync(messageId, cancellationToken);
    }

    /// <summary>
    /// Requeues a message for retry.
    /// </summary>
    /// <param name="messageId">The message ID to requeue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if requeued successfully.</returns>
    public async Task<bool> RequeueAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await this.buffer.RequeueAsync(messageId, cancellationToken);
    }

    /// <summary>
    /// Gets all messages currently in the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of node messages.</returns>
    public async Task<INodeMessage[]> GetAllMessagesAsync(CancellationToken cancellationToken = default)
    {
        var envelopes = await this.buffer.GetAllMessagesAsync(cancellationToken);
        return envelopes
            .Where(e => e.Payload is INodeMessage)
            .Select(e => (INodeMessage)e.Payload!)
            .ToArray();
    }

    /// <summary>
    /// Gets the count of messages asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of messages.</returns>
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await this.buffer.GetCountAsync(cancellationToken);
    }
}
