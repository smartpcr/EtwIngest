//-----------------------------------------------------------------------
// <copyright file="IProgressNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Event handler for when a progress node starts execution.
    /// </summary>
    /// <param name="node">The node that started.</param>
    public delegate void ProgressNodeStartedEventHandler(IProgressNode node);

    /// <summary>
    /// Event handler for when a progress node's value changes.
    /// </summary>
    /// <param name="node">The node whose progress changed.</param>
    /// <param name="statusMessage">The status message (description) at the time of progress update.</param>
    /// <param name="value">The new progress value.</param>
    public delegate void ProgressNodeProgressEventHandler(IProgressNode node, string statusMessage, double value);

    /// <summary>
    /// Event handler for when a progress node finishes successfully.
    /// </summary>
    /// <param name="node">The node that finished.</param>
    public delegate void ProgressNodeFinishedEventHandler(IProgressNode node);

    /// <summary>
    /// Event handler for when a progress node fails.
    /// </summary>
    /// <param name="node">The node that failed.</param>
    /// <param name="error">The error that occurred.</param>
    public delegate void ProgressNodeFailedEventHandler(IProgressNode node, Exception error);

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
        /// Reports the current progress value (absolute, not delta).
        /// This is the preferred way to update progress.
        /// </summary>
        /// <param name="currentValue">The current absolute progress value.</param>
        void ReportProgress(double currentValue);

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
        /// Gets the actual start time when this node's work began (Value > 0 or ExecuteAsync called).
        /// </summary>
        DateTime? StartTime { get; }

        /// <summary>
        /// Gets the actual end time when this node's work finished.
        /// </summary>
        DateTime? EndTime { get; }

        /// <summary>
        /// Gets the effective start time.
        /// For leaf nodes: same as StartTime.
        /// For parent nodes: earliest of own StartTime and children's EffectiveStartTime.
        /// </summary>
        DateTime EffectiveStartTime { get; }

        /// <summary>
        /// Gets the effective end time.
        /// For leaf nodes: same as EndTime or DateTime.Now if not finished.
        /// For parent nodes: latest of own EndTime and children's EffectiveEndTime.
        /// </summary>
        DateTime EffectiveEndTime { get; }

        /// <summary>
        /// Gets the actual duration in seconds (EndTime - StartTime).
        /// Only meaningful for leaf nodes that have completed.
        /// </summary>
        double ActualDuration { get; }

        /// <summary>
        /// Gets the effective duration in seconds (EffectiveEndTime - EffectiveStartTime).
        /// For parent nodes, this represents the total time span covering all children.
        /// </summary>
        double EffectiveDuration { get; }

        /// <summary>
        /// Gets the detected execution mode based on actual time overlaps of children.
        /// Returns null if no children or children haven't started yet.
        /// </summary>
        ExecutionMode? DetectedExecutionMode { get; }

        /// <summary>
        /// Gets the calculated duration in seconds.
        /// For leaf tasks: finish time - start time.
        /// For parent tasks with Sequential execution: sum of children durations.
        /// For parent tasks with Parallel execution: max of children durations.
        /// </summary>
        [Obsolete("Use ActualDuration or EffectiveDuration instead")]
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
        /// <param name="workFunc">Optional work function to execute for this child node.</param>
        /// <returns>The created child node.</returns>
        IProgressNode AddChild(string id, string description, TaskType taskType = TaskType.Job, ExecutionMode executionMode = ExecutionMode.Sequential, double maxValue = 100, double weight = 1.0, Func<IProgressNode, CancellationToken, Task>? workFunc = null);

        /// <summary>
        /// Event raised when the node starts execution.
        /// </summary>
        event ProgressNodeStartedEventHandler? OnStart;

        /// <summary>
        /// Event raised when the node's progress value changes.
        /// </summary>
        event ProgressNodeProgressEventHandler? OnProgress;

        /// <summary>
        /// Event raised when the node finishes execution successfully.
        /// </summary>
        event ProgressNodeFinishedEventHandler? OnFinish;

        /// <summary>
        /// Event raised when the node fails during execution.
        /// </summary>
        event ProgressNodeFailedEventHandler? OnFail;

        /// <summary>
        /// Executes this node and its children based on execution mode.
        /// For leaf nodes: executes the work function if provided.
        /// For parent nodes with Parallel mode: executes all children with Task.WhenAll.
        /// For parent nodes with Sequential mode: executes children one by one.
        /// Automatically raises OnStart, OnProgress, OnFinish, or OnFail events.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the execution.</returns>
        Task ExecuteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually marks this node as started at the specified time.
        /// Use this when not using ExecuteAsync pattern (e.g., when updating progress via events).
        /// </summary>
        /// <param name="startTime">The start time. If null, uses DateTime.Now.</param>
        void MarkStarted(DateTime? startTime = null);

        /// <summary>
        /// Manually marks this node as completed at the specified time.
        /// Use this when not using ExecuteAsync pattern (e.g., when updating progress via events).
        /// </summary>
        /// <param name="endTime">The end time. If null, uses DateTime.Now.</param>
        void MarkCompleted(DateTime? endTime = null);
    }
}
