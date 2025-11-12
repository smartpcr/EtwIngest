//-----------------------------------------------------------------------
// <copyright file="IProgressNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a node in the progress tree.
    /// </summary>
    public interface IProgressNode
    {
        /// <summary>
        /// Gets the unique identifier for this node.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets or sets the description of the task.
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Gets or sets the current progress value (0-100).
        /// </summary>
        double Value { get; set; }

        /// <summary>
        /// Gets or sets the maximum value for this task.
        /// </summary>
        double MaxValue { get; set; }

        /// <summary>
        /// Gets a value indicating whether this task is completed.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Gets a value indicating whether this task is running.
        /// </summary>
        bool IsStarted { get; }

        /// <summary>
        /// Gets the parent node, or null if this is the root.
        /// </summary>
        IProgressNode? Parent { get; }

        /// <summary>
        /// Gets the read-only list of child nodes.
        /// </summary>
        IReadOnlyList<IProgressNode> Children { get; }

        /// <summary>
        /// Gets the execution mode for child tasks.
        /// </summary>
        ExecutionMode ExecutionMode { get; }

        /// <summary>
        /// Gets the task type.
        /// </summary>
        TaskType TaskType { get; }

        /// <summary>
        /// Gets the depth in the tree (0 for root).
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// Gets the weight of this node relative to its siblings (default 1.0).
        /// Used to calculate parent progress based on weighted average of children.
        /// </summary>
        double Weight { get; }

        /// <summary>
        /// Gets the calculated duration in seconds.
        /// For leaf tasks: finish time - start time.
        /// For parent tasks with Sequential execution: sum of children durations.
        /// For parent tasks with Parallel execution: max of children durations.
        /// </summary>
        double DurationSeconds { get; }

        /// <summary>
        /// Adds a child task to this node.
        /// </summary>
        /// <param name="id">Unique identifier for the child.</param>
        /// <param name="description">Child task description.</param>
        /// <param name="taskType">Type of task.</param>
        /// <param name="executionMode">Execution mode for the child's children.</param>
        /// <param name="maxValue">Maximum value for the child task.</param>
        /// <param name="weight">Weight of this child relative to siblings (default 1.0).</param>
        /// <returns>The created child node.</returns>
        IProgressNode AddChild(string id, string description, TaskType taskType = TaskType.Job, ExecutionMode executionMode = ExecutionMode.Sequential, double maxValue = 100, double weight = 1.0);

        /// <summary>
        /// Increments the progress value.
        /// </summary>
        /// <param name="amount">Amount to increment.</param>
        void Increment(double amount);

        /// <summary>
        /// Marks the task as completed.
        /// </summary>
        void Complete();

        /// <summary>
        /// Marks the task as failed.
        /// </summary>
        /// <param name="errorMessage">Error message.</param>
        void Fail(string errorMessage);
    }
}
