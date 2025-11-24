// -----------------------------------------------------------------------
// <copyright file="NodeMessageQueue.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue
{
    using System.Threading.Channels;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Messages;

    /// <summary>
    /// Per-node message queue with lease-based message processing.
    /// Implements visibility timeout pattern similar to AWS SQS/Azure Service Bus.
    /// Phase 2.1 implementation with enhanced lease management and dead letter queue integration.
    /// Uses CircularBuffer for lock-free, high-performance message storage.
    /// Supports channel-based signaling for efficient message-driven processing (zero CPU when idle).
    /// </summary>
    public class NodeMessageQueue
    {
        private readonly ICircularBuffer buffer;
        private readonly TimeSpan visibilityTimeout;
        private readonly int maxRetries;
        private readonly DeadLetterQueue? deadLetterQueue;
        private readonly Channel<INodeMessage> messageSignalChannel;

        /// <summary>
        /// Gets the channel reader for subscribing to incoming messages.
        /// Workers can efficiently wait for messages via channel.Reader.WaitToReadAsync().
        /// </summary>
        public ChannelReader<INodeMessage> MessageSignals => this.messageSignalChannel.Reader;

        /// <summary>
        /// Initializes a new instance of the NodeMessageQueue class.
        /// </summary>
        /// <param name="capacity">The maximum capacity of the queue (default 100).</param>
        /// <param name="visibilityTimeout">How long messages are invisible after leasing (default 30 seconds).</param>
        /// <param name="maxRetries">Maximum retry attempts before moving to dead letter queue (default 3).</param>
        /// <param name="deadLetterQueue">Optional dead letter queue for failed messages.</param>
        public NodeMessageQueue(
            int capacity = 100,
            TimeSpan? visibilityTimeout = null,
            int maxRetries = 3,
            DeadLetterQueue? deadLetterQueue = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }

            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries cannot be negative.");
            }

            this.buffer = new CircularBuffer(capacity);
            this.visibilityTimeout = visibilityTimeout ?? TimeSpan.FromSeconds(30);
            this.maxRetries = maxRetries;
            this.deadLetterQueue = deadLetterQueue;

            // Create bounded channel with capacity 1 for signal coalescing
            // Multiple enqueues signal once - workers wake up and check for messages
            this.messageSignalChannel = Channel.CreateBounded<INodeMessage>(new BoundedChannelOptions(1)
            {
                SingleReader = false, // Multiple consumers can subscribe
                SingleWriter = false, // Multiple producers can enqueue
                FullMode = BoundedChannelFullMode.DropWrite // Coalesce signals - don't need duplicate signals
            });
        }

        /// <summary>
        /// Gets the current count of messages in the queue (including leased messages).
        /// </summary>
        public int Count => this.buffer.Count;

        /// <summary>
        /// Gets the maximum capacity of the queue.
        /// </summary>
        public int Capacity => this.buffer.Capacity;

        /// <summary>
        /// Gets the visibility timeout duration.
        /// </summary>
        public TimeSpan VisibilityTimeout => this.visibilityTimeout;

        /// <summary>
        /// Gets the maximum retry attempts.
        /// </summary>
        public int MaxRetries => this.maxRetries;

        /// <summary>
        /// Enqueues a message into the queue.
        /// Signals waiting workers via channel (zero-CPU efficient blocking).
        /// </summary>
        /// <param name="message">The message to enqueue.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if enqueued successfully, false if queue is at capacity.</returns>
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
                Status = MessageStatus.Ready,
                EnqueuedAt = DateTime.UtcNow,
                RetryCount = 0,
                MaxRetries = this.maxRetries
            };

            var result = await this.buffer.EnqueueAsync(envelope, cancellationToken);

            // Signal waiting workers via channel (non-blocking, coalescing)
            if (result)
            {
                this.messageSignalChannel.Writer.TryWrite(message);
            }

            return result;
        }

        /// <summary>
        /// Leases a message from the queue for processing.
        /// The message becomes invisible to other consumers until the lease expires or is completed/abandoned.
        ///
        /// IMPORTANT LEASE SEMANTICS:
        /// - Lease duration MUST be greater than handler execution timeout
        /// - If lease expires, we assume the handler has crashed or hung
        /// - CircularBuffer automatically requeues expired messages during checkout (if retry count allows)
        /// - This provides self-healing behavior without needing explicit monitoring
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A message lease, or null if no messages are available.</returns>
        public async Task<MessageLease?> LeaseAsync(CancellationToken cancellationToken = default)
        {
            // CircularBuffer.CheckoutAsync automatically handles expired lease requeuing
            // No manual cleanup needed - it happens just-in-time during message scan

            // CircularBuffer.CheckoutAsync requires a message type filter
            // For NodeMessageQueue, we accept any INodeMessage
            // We need to iterate through known message types
            var messageTypes = new[]
            {
                typeof(NodeCompleteMessage),
                typeof(NodeFailMessage),
                typeof(NodeNextMessage),
                typeof(ProgressMessage),
                typeof(NodeCancelMessage)
            };

            foreach (var messageType in messageTypes)
            {
                var envelope = await this.buffer.CheckoutAsync(
                    messageType,
                    Guid.NewGuid().ToString(), // handler ID
                    this.visibilityTimeout,
                    cancellationToken);

                if (envelope != null)
                {
                    var lease = new MessageLease(
                        (INodeMessage)envelope.Payload!,
                        envelope.MessageId,
                        envelope.Lease!.LeaseExpiry,
                        envelope.RetryCount);

                    return lease;
                }
            }

            return null;
        }

        /// <summary>
        /// Completes a leased message, removing it from the queue.
        /// </summary>
        /// <param name="lease">The message lease to complete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the completion.</returns>
        public async Task CompleteAsync(MessageLease lease, CancellationToken cancellationToken = default)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            await this.buffer.AcknowledgeAsync(lease.MessageId, cancellationToken);
        }

        /// <summary>
        /// Abandons a leased message, returning it to the queue for retry.
        /// Increments the retry count and moves to dead letter queue if max retries exceeded.
        /// </summary>
        /// <param name="lease">The message lease to abandon.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the abandonment.</returns>
        public async Task AbandonAsync(MessageLease lease, CancellationToken cancellationToken = default)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            // Get all messages from buffer to find the one we're abandoning
            var allMessages = await this.buffer.GetAllMessagesAsync(cancellationToken);
            var envelope = allMessages.FirstOrDefault(m => m.MessageId == lease.MessageId);

            if (envelope != null)
            {
                // Check if max retries exceeded
                if (envelope.RetryCount + 1 > this.maxRetries)
                {
                    // Move to dead letter queue
                    if (this.deadLetterQueue != null)
                    {
                        await this.deadLetterQueue.AddAsync(
                            envelope,
                            $"Message exceeded max retries ({this.maxRetries})",
                            null);
                    }

                    // Remove from main queue
                    await this.buffer.RemoveAsync(lease.MessageId, cancellationToken);
                }
                else
                {
                    // Requeue for retry with visibility timeout
                    await this.buffer.RequeueAsync(lease.MessageId, DateTime.UtcNow + this.visibilityTimeout, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Moves a leased message directly to the dead letter queue without retry.
        /// Used for fatal errors like node creation failures that should not be retried.
        /// </summary>
        /// <param name="lease">The message lease to move to dead letter.</param>
        /// <param name="reason">The reason for moving to dead letter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task MoveToDeadLetterAsync(MessageLease lease, string reason, CancellationToken cancellationToken = default)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            // Get all messages from buffer to find the one we're moving
            var allMessages = await this.buffer.GetAllMessagesAsync(cancellationToken);
            var envelope = allMessages.FirstOrDefault(m => m.MessageId == lease.MessageId);

            if (envelope != null)
            {
                // Move to dead letter queue
                if (this.deadLetterQueue != null)
                {
                    await this.deadLetterQueue.AddAsync(
                        envelope,
                        reason,
                        null);
                }

                // Remove from main queue
                await this.buffer.RemoveAsync(lease.MessageId, cancellationToken);
            }
        }

        /// <summary>
        /// Gets the count of messages asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The count of messages.</returns>
        public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        {
            return this.buffer.GetCountAsync(cancellationToken);
        }

        /// <summary>
        /// Gets all messages in the queue.
        /// Used for checkpoint serialization.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Array of all messages in the queue.</returns>
        public Task<MessageEnvelope[]> GetAllMessagesAsync(CancellationToken cancellationToken = default)
        {
            return this.buffer.GetAllMessagesAsync(cancellationToken);
        }

        /// <summary>
        /// Restores a message to the queue from a checkpoint.
        /// Used during workflow resume to restore message queue state.
        /// </summary>
        /// <param name="envelope">The message envelope to restore.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if restored successfully.</returns>
        public async Task<bool> RestoreFromCheckpointAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            // Restore the message to the buffer
            var restored = await this.buffer.RestoreAsync(envelope, cancellationToken);

            // If message is ready and visible, signal the channel to wake up consumers
            if (restored && envelope.Status == MessageStatus.Ready &&
                (!envelope.NotBefore.HasValue || envelope.NotBefore.Value <= DateTime.UtcNow))
            {
                // Construct a minimal message for signaling
                INodeMessage signal = new NodeCompleteMessage
                {
                    NodeId = string.Empty,
                    Timestamp = DateTime.UtcNow
                };

                await this.messageSignalChannel.Writer.WriteAsync(signal, cancellationToken);
            }

            return restored;
        }
    }
}
