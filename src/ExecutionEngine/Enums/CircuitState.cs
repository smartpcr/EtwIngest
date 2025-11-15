// -----------------------------------------------------------------------
// <copyright file="CircuitState.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums
{
    /// <summary>
    /// Defines the state of a circuit breaker.
    /// Implements the Circuit Breaker pattern state machine.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuit is closed - normal operation.
        /// All requests are allowed through.
        /// If failure threshold is exceeded, transitions to Open.
        /// </summary>
        Closed = 0,

        /// <summary>
        /// Circuit is open - too many failures detected.
        /// All requests are rejected immediately without attempting execution.
        /// After OpenDuration expires, transitions to HalfOpen.
        /// </summary>
        Open = 1,

        /// <summary>
        /// Circuit is half-open - testing recovery.
        /// Limited requests are allowed through to test if the issue is resolved.
        /// If HalfOpenSuccesses are achieved, transitions to Closed.
        /// If any failure occurs, transitions back to Open.
        /// </summary>
        HalfOpen = 2
    }
}
