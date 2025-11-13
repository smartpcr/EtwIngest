// -----------------------------------------------------------------------
// <copyright file="IWorkflowEngine.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Engine
{
    using ExecutionEngine.Contexts;
    using ExecutionEngine.Workflow;

    /// <summary>
    /// Interface for workflow execution engine.
    /// Provides lifecycle management: Start, Pause, Resume, Cancel.
    /// </summary>
    public interface IWorkflowEngine
    {
        /// <summary>
        /// Starts a new workflow execution.
        /// </summary>
        /// <param name="workflowDefinition">The workflow definition to execute.</param>
        /// <param name="timeout">Optional timeout for the entire workflow execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        Task<WorkflowExecutionContext> StartAsync(
            WorkflowDefinition workflowDefinition,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes a paused workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to resume.</param>
        /// <param name="timeout">Optional timeout for the remaining workflow execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        Task<WorkflowExecutionContext> ResumeAsync(
            Guid workflowInstanceId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses a running workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to pause.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the pause operation.</returns>
        Task PauseAsync(
            Guid workflowInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a running or paused workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the cancel operation.</returns>
        Task CancelAsync(
            Guid workflowInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context, or null if not found.</returns>
        Task<WorkflowExecutionContext?> GetWorkflowStatusAsync(
            Guid workflowInstanceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Recovers incomplete workflows from persistent storage and resumes their execution.
        /// Used for failure recovery and resuming workflows after engine restart.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of recovered workflow instance IDs.</returns>
        Task<IReadOnlyCollection<Guid>> RecoverIncompleteWorkflowsAsync(
            CancellationToken cancellationToken = default);
    }
}
