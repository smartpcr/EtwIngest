// -----------------------------------------------------------------------
// <copyright file="ConcurrencyLimiter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Concurrency
{
    using ExecutionEngine.Enums;

    /// <summary>
    /// Manages workflow-level concurrency limits with priority-based scheduling.
    /// Ensures that high-priority nodes are executed before lower priority nodes
    /// when the concurrency limit is reached. Implements fair scheduling with
    /// round-robin across priority levels to prevent starvation.
    /// </summary>
    public class ConcurrencyLimiter : IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private readonly int maxConcurrency;
        private readonly Dictionary<NodePriority, Queue<TaskCompletionSource<bool>>> priorityQueues;
        private readonly object queueLock = new object();
        private int currentPriorityIndex = 0;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyLimiter"/> class.
        /// </summary>
        /// <param name="maxConcurrency">Maximum number of concurrent node executions (0 = unlimited).</param>
        public ConcurrencyLimiter(int maxConcurrency)
        {
            if (maxConcurrency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Max concurrency cannot be negative.");
            }

            this.maxConcurrency = maxConcurrency;
            this.semaphore = maxConcurrency == 0
                ? new SemaphoreSlim(int.MaxValue, int.MaxValue)
                : new SemaphoreSlim(maxConcurrency, maxConcurrency);

            this.priorityQueues = new Dictionary<NodePriority, Queue<TaskCompletionSource<bool>>>
            {
                { NodePriority.High, new Queue<TaskCompletionSource<bool>>() },
                { NodePriority.Normal, new Queue<TaskCompletionSource<bool>>() },
                { NodePriority.Low, new Queue<TaskCompletionSource<bool>>() }
            };
        }

        /// <summary>
        /// Gets the maximum concurrency limit.
        /// </summary>
        public int MaxConcurrency => this.maxConcurrency;

        /// <summary>
        /// Gets the current number of available slots.
        /// </summary>
        public int AvailableSlots => this.semaphore.CurrentCount;

        /// <summary>
        /// Gets the total number of queued requests across all priorities.
        /// </summary>
        public int QueuedCount
        {
            get
            {
                lock (this.queueLock)
                {
                    return this.priorityQueues.Values.Sum(q => q.Count);
                }
            }
        }

        /// <summary>
        /// Acquires a concurrency slot for the specified priority.
        /// If the limit is reached, the request is queued according to priority.
        /// High priority requests are processed before normal, which are processed before low.
        /// Fair scheduling (round-robin) prevents starvation of lower priorities.
        /// </summary>
        /// <param name="priority">The priority level of the request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when a slot is acquired.</returns>
        public async Task<ConcurrencySlot> AcquireAsync(NodePriority priority, CancellationToken cancellationToken = default)
        {
            // Try to acquire immediately
            if (await this.semaphore.WaitAsync(0, cancellationToken))
            {
                return new ConcurrencySlot(this);
            }

            // Need to queue - create a TCS for this request
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (this.queueLock)
            {
                this.priorityQueues[priority].Enqueue(tcs);
            }

            // Register cancellation
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                // Wait for our turn
                await tcs.Task;
            }

            // Acquired!
            return new ConcurrencySlot(this);
        }

        /// <summary>
        /// Releases a concurrency slot and processes the next queued request if any.
        /// Uses fair scheduling with round-robin across priority levels.
        /// </summary>
        internal void Release()
        {
            TaskCompletionSource<bool>? nextTcs = null;

            lock (this.queueLock)
            {
                // Try to find next request using fair scheduling
                // Round-robin: check each priority starting from current index
                for (var i = 0; i < 3; i++)
                {
                    var priorityToCheck = (NodePriority)((this.currentPriorityIndex + i) % 3);
                    var reversePriority = (NodePriority)(2 - (int)priorityToCheck); // Reverse mapping: 0->2, 1->1, 2->0

                    if (this.priorityQueues[reversePriority].Count > 0)
                    {
                        nextTcs = this.priorityQueues[reversePriority].Dequeue();
                        this.currentPriorityIndex = ((int)reversePriority + 1) % 3; // Move to next priority for fairness
                        break;
                    }
                }
            }

            if (nextTcs != null)
            {
                // Signal the next waiter without releasing semaphore
                nextTcs.TrySetResult(true);
            }
            else
            {
                // No waiters, release the semaphore
                this.semaphore.Release();
            }
        }

        /// <summary>
        /// Gets the number of queued requests for a specific priority.
        /// </summary>
        /// <param name="priority">The priority level to check.</param>
        /// <returns>The count of queued requests at this priority.</returns>
        public int GetQueuedCount(NodePriority priority)
        {
            lock (this.queueLock)
            {
                return this.priorityQueues[priority].Count;
            }
        }

        /// <summary>
        /// Disposes the concurrency limiter.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the concurrency limiter.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.semaphore?.Dispose();

                    lock (this.queueLock)
                    {
                        // Cancel all pending requests
                        foreach (var queue in this.priorityQueues.Values)
                        {
                            while (queue.Count > 0)
                            {
                                var tcs = queue.Dequeue();
                                tcs.TrySetCanceled();
                            }
                        }
                    }
                }

                this.disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents an acquired concurrency slot that must be disposed to release.
    /// </summary>
    public class ConcurrencySlot : IDisposable
    {
        private readonly ConcurrencyLimiter limiter;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencySlot"/> class.
        /// </summary>
        /// <param name="limiter">The concurrency limiter that owns this slot.</param>
        internal ConcurrencySlot(ConcurrencyLimiter limiter)
        {
            this.limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
        }

        /// <summary>
        /// Releases the concurrency slot back to the limiter.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.limiter.Release();
                this.disposed = true;
            }
        }
    }
}
