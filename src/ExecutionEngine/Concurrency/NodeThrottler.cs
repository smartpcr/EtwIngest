// -----------------------------------------------------------------------
// <copyright file="NodeThrottler.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Concurrency
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Manages per-node-type concurrency limits (throttling).
    /// Prevents resource exhaustion by limiting how many instances of a specific
    /// node type can execute concurrently. For example, limiting database query
    /// nodes to 5 concurrent executions even if workflow allows 100 total nodes.
    /// </summary>
    public class NodeThrottler : IDisposable
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> nodeSemaphores;
        private readonly ConcurrentDictionary<string, int> nodeMaxConcurrency;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeThrottler"/> class.
        /// </summary>
        public NodeThrottler()
        {
            this.nodeSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            this.nodeMaxConcurrency = new ConcurrentDictionary<string, int>();
        }

        /// <summary>
        /// Registers a node type with its concurrency limit.
        /// Must be called before acquiring slots for this node type.
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        /// <param name="maxConcurrentExecutions">Maximum concurrent executions (0 = unlimited).</param>
        public void RegisterNode(string nodeId, int maxConcurrentExecutions)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            if (maxConcurrentExecutions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentExecutions), "Max concurrent executions cannot be negative.");
            }

            // Store the limit
            this.nodeMaxConcurrency[nodeId] = maxConcurrentExecutions;

            // Create semaphore if limit is set
            if (maxConcurrentExecutions > 0)
            {
                this.nodeSemaphores.GetOrAdd(
                    nodeId,
                    _ => new SemaphoreSlim(maxConcurrentExecutions, maxConcurrentExecutions));
            }
        }

        /// <summary>
        /// Acquires a concurrency slot for the specified node type.
        /// If the node-specific limit is reached, this will block until a slot is available.
        /// Returns null if the node has no throttling configured (unlimited).
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A node throttle slot, or null if node has unlimited concurrency.</returns>
        public async Task<NodeThrottleSlot?> AcquireAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            // Check if this node has a limit
            if (!this.nodeMaxConcurrency.TryGetValue(nodeId, out var maxConcurrency) || maxConcurrency == 0)
            {
                // No limit configured for this node
                return null;
            }

            // Get or create semaphore
            var semaphore = this.nodeSemaphores.GetOrAdd(
                nodeId,
                _ => new SemaphoreSlim(maxConcurrency, maxConcurrency));

            // Acquire
            await semaphore.WaitAsync(cancellationToken);

            return new NodeThrottleSlot(this, nodeId);
        }

        /// <summary>
        /// Releases a concurrency slot for the specified node type.
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        internal void Release(string nodeId)
        {
            if (this.nodeSemaphores.TryGetValue(nodeId, out var semaphore))
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the current number of available slots for a node type.
        /// Returns int.MaxValue if the node has no throttling configured.
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        /// <returns>The number of available slots.</returns>
        public int GetAvailableSlots(string nodeId)
        {
            if (this.nodeSemaphores.TryGetValue(nodeId, out var semaphore))
            {
                return semaphore.CurrentCount;
            }

            return int.MaxValue; // Unlimited
        }

        /// <summary>
        /// Gets the maximum concurrency limit for a node type.
        /// Returns 0 if the node has no throttling configured (unlimited).
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        /// <returns>The maximum concurrency limit.</returns>
        public int GetMaxConcurrency(string nodeId)
        {
            if (this.nodeMaxConcurrency.TryGetValue(nodeId, out var maxConcurrency))
            {
                return maxConcurrency;
            }

            return 0; // Unlimited
        }

        /// <summary>
        /// Removes a node type registration and releases its semaphore.
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        public void UnregisterNode(string nodeId)
        {
            this.nodeMaxConcurrency.TryRemove(nodeId, out _);

            if (this.nodeSemaphores.TryRemove(nodeId, out var semaphore))
            {
                semaphore.Dispose();
            }
        }

        /// <summary>
        /// Disposes the node throttler and all semaphores.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the node throttler.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    foreach (var semaphore in this.nodeSemaphores.Values)
                    {
                        semaphore?.Dispose();
                    }

                    this.nodeSemaphores.Clear();
                    this.nodeMaxConcurrency.Clear();
                }

                this.disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents an acquired node throttle slot that must be disposed to release.
    /// </summary>
    public class NodeThrottleSlot : IDisposable
    {
        private readonly NodeThrottler throttler;
        private readonly string nodeId;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeThrottleSlot"/> class.
        /// </summary>
        /// <param name="throttler">The node throttler that owns this slot.</param>
        /// <param name="nodeId">The node ID for which this slot was acquired.</param>
        internal NodeThrottleSlot(NodeThrottler throttler, string nodeId)
        {
            this.throttler = throttler ?? throw new ArgumentNullException(nameof(throttler));
            this.nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        }

        /// <summary>
        /// Releases the throttle slot back to the throttler.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.throttler.Release(this.nodeId);
                this.disposed = true;
            }
        }
    }
}
