// -----------------------------------------------------------------------
// <copyright file="RetryStrategy.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums
{
    /// <summary>
    /// Defines the retry strategy for failed node executions.
    /// Controls how delays between retry attempts are calculated.
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// No retry - fail immediately on first failure.
        /// Use for operations that should not be retried (e.g., validation failures).
        /// </summary>
        None = 0,

        /// <summary>
        /// Fixed delay between retries.
        /// Each retry waits the same amount of time (InitialDelay).
        /// Example: 1s, 1s, 1s, 1s
        /// </summary>
        Fixed = 1,

        /// <summary>
        /// Exponential backoff - delay doubles on each retry.
        /// Helps prevent overwhelming failing services.
        /// Formula: InitialDelay * (Multiplier ^ retryCount)
        /// Example with 1s initial, 2.0 multiplier: 1s, 2s, 4s, 8s, 16s
        /// </summary>
        Exponential = 2,

        /// <summary>
        /// Linear backoff - delay increases linearly.
        /// Formula: InitialDelay * (1 + retryCount)
        /// Example with 1s initial: 1s, 2s, 3s, 4s, 5s
        /// </summary>
        Linear = 3
    }
}
