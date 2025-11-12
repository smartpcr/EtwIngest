// -----------------------------------------------------------------------
// <copyright file="MessageLease.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Queue
{
    using ExecutionEngine.Messages;

    /// <summary>
    /// Represents a leased message from the queue.
    /// Provides information about the message and its lease expiration.
    /// </summary>
    public class MessageLease
    {
        /// <summary>
        /// Initializes a new instance of the MessageLease class.
        /// </summary>
        /// <param name="message">The leased message.</param>
        /// <param name="messageId">The unique message identifier.</param>
        /// <param name="leaseExpiry">When the lease expires.</param>
        /// <param name="retryCount">The current retry count.</param>
        public MessageLease(INodeMessage message, Guid messageId, DateTime leaseExpiry, int retryCount)
        {
            this.Message = message ?? throw new ArgumentNullException(nameof(message));
            this.MessageId = messageId;
            this.LeaseExpiry = leaseExpiry;
            this.RetryCount = retryCount;
            this.LeasedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the leased message.
        /// </summary>
        public INodeMessage Message { get; }

        /// <summary>
        /// Gets the unique message identifier.
        /// </summary>
        public Guid MessageId { get; }

        /// <summary>
        /// Gets when the lease expires.
        /// After this time, the message becomes visible again for reprocessing.
        /// </summary>
        public DateTime LeaseExpiry { get; }

        /// <summary>
        /// Gets when the message was leased.
        /// </summary>
        public DateTime LeasedAt { get; }

        /// <summary>
        /// Gets the current retry count.
        /// </summary>
        public int RetryCount { get; }

        /// <summary>
        /// Gets a value indicating whether the lease has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= this.LeaseExpiry;

        /// <summary>
        /// Gets the time remaining until the lease expires.
        /// </summary>
        public TimeSpan TimeRemaining => this.LeaseExpiry > DateTime.UtcNow
            ? this.LeaseExpiry - DateTime.UtcNow
            : TimeSpan.Zero;
    }
}
