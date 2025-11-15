//-----------------------------------------------------------------------
// <copyright file="IProgressTreeManager.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages a hierarchical progress tree with automatic parent progress calculation.
    /// </summary>
    public interface IProgressTreeManager : IDisposable
    {
        /// <summary>
        /// Starts the progress tracking session.
        /// </summary>
        /// <param name="rootDescription">Description for the root task.</param>
        /// <param name="executionMode">Execution mode for root's children.</param>
        /// <param name="action">Action to execute within the progress context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RunAsync(string rootDescription, ExecutionMode executionMode, Func<IProgressNode, Task> action);

        /// <summary>
        /// Gets the root task.
        /// </summary>
        IProgressNode? RootTask { get; }

        /// <summary>
        /// Gets the total number of tasks (including all descendants).
        /// </summary>
        int TaskCount { get; }

        /// <summary>
        /// Gets the number of completed tasks.
        /// </summary>
        int CompletedTaskCount { get; }

        /// <summary>
        /// Gets the overall progress percentage (0-100).
        /// </summary>
        double OverallProgress { get; }
    }
}
