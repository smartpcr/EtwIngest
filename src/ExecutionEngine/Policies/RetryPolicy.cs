// -----------------------------------------------------------------------
// <copyright file="RetryPolicy.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Policies
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ExecutionEngine.Enums;

    /// <summary>
    /// Defines retry behavior for failed node executions.
    /// Supports multiple retry strategies with configurable delays, jitter, and conditional retry.
    /// </summary>
    public class RetryPolicy
    {
        private static readonly Random Jitter = new Random();

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy"/> class with default values.
        /// </summary>
        public RetryPolicy()
        {
            this.Strategy = RetryStrategy.None;
            this.MaxAttempts = 3;
            this.InitialDelay = TimeSpan.FromSeconds(1);
            this.MaxDelay = TimeSpan.FromSeconds(60);
            this.Multiplier = 2.0;
            this.RetryOn = new List<Type>();
            this.DoNotRetryOn = new List<Type>();
        }

        /// <summary>
        /// Gets or sets the retry strategy.
        /// Determines how delays between retry attempts are calculated.
        /// </summary>
        public RetryStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// Valid range: 1-10. Default: 3.
        /// Total executions = MaxAttempts + 1 (initial attempt).
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the initial delay before the first retry.
        /// Used as the base delay for all retry strategies.
        /// Default: 1 second.
        /// </summary>
        public TimeSpan InitialDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum delay between retries.
        /// Prevents exponential backoff from growing too large.
        /// Default: 60 seconds.
        /// </summary>
        public TimeSpan MaxDelay { get; set; }

        /// <summary>
        /// Gets or sets the multiplier for exponential backoff.
        /// Only used with RetryStrategy.Exponential.
        /// Formula: InitialDelay * (Multiplier ^ retryCount).
        /// Default: 2.0 (double the delay each time).
        /// </summary>
        public double Multiplier { get; set; }

        /// <summary>
        /// Gets or sets the list of exception types that should trigger a retry.
        /// If empty, all exceptions trigger retry (unless in DoNotRetryOn).
        /// Example: { typeof(TimeoutException), typeof(HttpRequestException) }.
        /// </summary>
        public List<Type> RetryOn { get; set; }

        /// <summary>
        /// Gets or sets the list of exception types that should NOT trigger a retry.
        /// Takes precedence over RetryOn.
        /// Example: { typeof(ArgumentException), typeof(InvalidOperationException) }.
        /// </summary>
        public List<Type> DoNotRetryOn { get; set; }

        /// <summary>
        /// Calculates the delay before the next retry attempt.
        /// Applies the selected retry strategy and adds jitter (±25% randomization).
        /// </summary>
        /// <param name="retryCount">The current retry attempt (0-based).</param>
        /// <returns>The delay to wait before the next retry, capped at MaxDelay.</returns>
        public TimeSpan CalculateDelay(int retryCount)
        {
            if (this.Strategy == RetryStrategy.None)
            {
                return TimeSpan.Zero;
            }

            var delayMs = this.Strategy switch
            {
                RetryStrategy.Fixed => this.InitialDelay.TotalMilliseconds,
                RetryStrategy.Exponential => this.InitialDelay.TotalMilliseconds * Math.Pow(this.Multiplier, retryCount),
                RetryStrategy.Linear => this.InitialDelay.TotalMilliseconds * (1 + retryCount),
                _ => this.InitialDelay.TotalMilliseconds
            };

            // Cap at MaxDelay
            delayMs = Math.Min(delayMs, this.MaxDelay.TotalMilliseconds);

            // Add jitter: ±25% randomization to prevent thundering herd
            var jitterFactor = 0.75 + (Jitter.NextDouble() * 0.5); // 0.75 to 1.25
            delayMs *= jitterFactor;

            return TimeSpan.FromMilliseconds(delayMs);
        }

        /// <summary>
        /// Determines whether the specified exception should trigger a retry.
        /// Checks DoNotRetryOn first (takes precedence), then RetryOn.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        /// <returns>True if the exception should trigger a retry; otherwise, false.</returns>
        public bool ShouldRetry(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            // Check DoNotRetryOn first (takes precedence)
            if (this.DoNotRetryOn.Any(type => type.IsAssignableFrom(exception.GetType())))
            {
                return false;
            }

            // If RetryOn is specified, only retry those exception types
            if (this.RetryOn.Any())
            {
                return this.RetryOn.Any(type => type.IsAssignableFrom(exception.GetType()));
            }

            // If RetryOn is empty, retry all exceptions (except those in DoNotRetryOn)
            return true;
        }

        /// <summary>
        /// Validates the retry policy configuration.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if configuration is invalid.</exception>
        public void Validate()
        {
            if (this.MaxAttempts < 1 || this.MaxAttempts > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MaxAttempts), "MaxAttempts must be between 1 and 10.");
            }

            if (this.InitialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(this.InitialDelay), "InitialDelay cannot be negative.");
            }

            if (this.MaxDelay < this.InitialDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(this.MaxDelay), "MaxDelay must be greater than or equal to InitialDelay.");
            }

            if (this.Multiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(this.Multiplier), "Multiplier must be greater than zero.");
            }
        }
    }
}
