// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerPolicy.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Policies
{
    using System;

    /// <summary>
    /// Defines circuit breaker behavior for node executions.
    /// Prevents cascading failures by temporarily blocking calls to failing nodes.
    /// Implements the Circuit Breaker pattern with three states: Closed, Open, HalfOpen.
    /// </summary>
    public class CircuitBreakerPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerPolicy"/> class with default values.
        /// </summary>
        public CircuitBreakerPolicy()
        {
            this.FailureThreshold = 50; // 50% failure rate
            this.MinimumThroughput = 10; // At least 10 requests before opening
            this.OpenDuration = TimeSpan.FromSeconds(30);
            this.HalfOpenSuccesses = 3; // 3 successes to close circuit
        }

        /// <summary>
        /// Gets or sets the failure threshold percentage (0-100).
        /// Circuit opens when failure rate exceeds this threshold.
        /// Default: 50% (circuit opens when half of requests fail).
        /// </summary>
        public int FailureThreshold { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of requests before circuit can open.
        /// Prevents opening circuit on low sample sizes.
        /// Default: 10 requests.
        /// </summary>
        public int MinimumThroughput { get; set; }

        /// <summary>
        /// Gets or sets how long the circuit stays open before transitioning to HalfOpen.
        /// During this time, all requests are rejected immediately.
        /// Default: 30 seconds.
        /// </summary>
        public TimeSpan OpenDuration { get; set; }

        /// <summary>
        /// Gets or sets the number of consecutive successes needed in HalfOpen state to close the circuit.
        /// Default: 3 successful requests.
        /// </summary>
        public int HalfOpenSuccesses { get; set; }

        /// <summary>
        /// Validates the circuit breaker policy configuration.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if configuration is invalid.</exception>
        public void Validate()
        {
            if (this.FailureThreshold < 0 || this.FailureThreshold > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(this.FailureThreshold), "FailureThreshold must be between 0 and 100.");
            }

            if (this.MinimumThroughput < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MinimumThroughput), "MinimumThroughput must be at least 1.");
            }

            if (this.OpenDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(this.OpenDuration), "OpenDuration cannot be negative.");
            }

            if (this.HalfOpenSuccesses < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(this.HalfOpenSuccesses), "HalfOpenSuccesses must be at least 1.");
            }
        }
    }
}
