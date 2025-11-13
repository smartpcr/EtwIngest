// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerManager.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Resilience
{
    using System;
    using System.Collections.Concurrent;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Policies;

    /// <summary>
    /// Manages circuit breakers for node types.
    /// Implements the Circuit Breaker pattern to prevent cascading failures.
    /// Tracks failure rates and automatically transitions between Closed, Open, and HalfOpen states.
    /// </summary>
    public class CircuitBreakerManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, CircuitBreakerState> circuitStates;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerManager"/> class.
        /// </summary>
        public CircuitBreakerManager()
        {
            this.circuitStates = new ConcurrentDictionary<string, CircuitBreakerState>();
        }

        /// <summary>
        /// Registers a node with circuit breaker policy.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        /// <param name="policy">The circuit breaker policy.</param>
        public void RegisterNode(string nodeId, CircuitBreakerPolicy policy)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            policy.Validate();

            this.circuitStates.TryAdd(nodeId, new CircuitBreakerState(policy));
        }

        /// <summary>
        /// Checks if a request can proceed through the circuit breaker.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        /// <returns>True if request can proceed; false if circuit is open.</returns>
        public bool AllowRequest(string nodeId)
        {
            if (!this.circuitStates.TryGetValue(nodeId, out var state))
            {
                // No circuit breaker configured - allow request
                return true;
            }

            lock (state)
            {
                // Check if circuit should transition from Open to HalfOpen
                if (state.State == CircuitState.Open)
                {
                    if (DateTime.UtcNow >= state.OpenUntil)
                    {
                        // Transition to HalfOpen
                        state.State = CircuitState.HalfOpen;
                        state.HalfOpenSuccessCount = 0;
                    }
                    else
                    {
                        // Circuit is still open
                        return false;
                    }
                }

                // Allow request in Closed or HalfOpen state
                return true;
            }
        }

        /// <summary>
        /// Records a successful execution.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        public void RecordSuccess(string nodeId)
        {
            if (!this.circuitStates.TryGetValue(nodeId, out var state))
            {
                return;
            }

            lock (state)
            {
                state.TotalRequests++;
                state.SuccessfulRequests++;

                if (state.State == CircuitState.HalfOpen)
                {
                    state.HalfOpenSuccessCount++;

                    // Check if we can close the circuit
                    if (state.HalfOpenSuccessCount >= state.Policy.HalfOpenSuccesses)
                    {
                        // Transition to Closed
                        state.State = CircuitState.Closed;
                        state.ResetMetrics();
                    }
                }
            }
        }

        /// <summary>
        /// Records a failed execution.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        public void RecordFailure(string nodeId)
        {
            if (!this.circuitStates.TryGetValue(nodeId, out var state))
            {
                return;
            }

            lock (state)
            {
                state.TotalRequests++;
                state.FailedRequests++;

                if (state.State == CircuitState.HalfOpen)
                {
                    // Any failure in HalfOpen immediately opens circuit
                    this.OpenCircuit(state);
                }
                else if (state.State == CircuitState.Closed)
                {
                    // Check if we should open the circuit
                    if (state.TotalRequests >= state.Policy.MinimumThroughput)
                    {
                        double failureRate = (double)state.FailedRequests / state.TotalRequests * 100;

                        if (failureRate >= state.Policy.FailureThreshold)
                        {
                            this.OpenCircuit(state);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current circuit state for a node.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        /// <returns>The circuit state, or Closed if not found.</returns>
        public CircuitState GetState(string nodeId)
        {
            if (this.circuitStates.TryGetValue(nodeId, out var state))
            {
                lock (state)
                {
                    return state.State;
                }
            }

            return CircuitState.Closed;
        }

        /// <summary>
        /// Gets the failure rate for a node.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        /// <returns>The failure rate percentage (0-100).</returns>
        public double GetFailureRate(string nodeId)
        {
            if (this.circuitStates.TryGetValue(nodeId, out var state))
            {
                lock (state)
                {
                    if (state.TotalRequests == 0)
                    {
                        return 0;
                    }

                    return (double)state.FailedRequests / state.TotalRequests * 100;
                }
            }

            return 0;
        }

        /// <summary>
        /// Resets the circuit breaker for a node.
        /// </summary>
        /// <param name="nodeId">The node ID.</param>
        public void Reset(string nodeId)
        {
            if (this.circuitStates.TryGetValue(nodeId, out var state))
            {
                lock (state)
                {
                    state.State = CircuitState.Closed;
                    state.ResetMetrics();
                }
            }
        }

        /// <summary>
        /// Opens the circuit breaker.
        /// </summary>
        /// <param name="state">The circuit breaker state.</param>
        private void OpenCircuit(CircuitBreakerState state)
        {
            state.State = CircuitState.Open;
            state.OpenUntil = DateTime.UtcNow.Add(state.Policy.OpenDuration);
            state.ResetMetrics();
        }

        /// <summary>
        /// Disposes the circuit breaker manager.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.circuitStates.Clear();
                this.disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Represents the state of a circuit breaker for a specific node.
    /// </summary>
    internal class CircuitBreakerState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerState"/> class.
        /// </summary>
        /// <param name="policy">The circuit breaker policy.</param>
        public CircuitBreakerState(CircuitBreakerPolicy policy)
        {
            this.Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.State = CircuitState.Closed;
            this.ResetMetrics();
        }

        /// <summary>
        /// Gets the circuit breaker policy.
        /// </summary>
        public CircuitBreakerPolicy Policy { get; }

        /// <summary>
        /// Gets or sets the current circuit state.
        /// </summary>
        public CircuitState State { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the circuit will transition from Open to HalfOpen.
        /// </summary>
        public DateTime OpenUntil { get; set; }

        /// <summary>
        /// Gets or sets the total number of requests.
        /// </summary>
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of successful requests.
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of failed requests.
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of consecutive successes in HalfOpen state.
        /// </summary>
        public int HalfOpenSuccessCount { get; set; }

        /// <summary>
        /// Resets the metrics counters.
        /// </summary>
        public void ResetMetrics()
        {
            this.TotalRequests = 0;
            this.SuccessfulRequests = 0;
            this.FailedRequests = 0;
            this.HalfOpenSuccessCount = 0;
        }
    }
}
