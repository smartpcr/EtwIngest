//-----------------------------------------------------------------------
// <copyright file="IProgressNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using Spectre.Console;

    #region delegates
    /// <summary>
    /// Event handler for when a progress node starts execution.
    /// </summary>
    /// <param name="progressNode">The node that started.</param>
    public delegate void ProgressNodeStartedEventHandler(IProgressNode progressNode, ProgressTask task);

    /// <summary>
    /// Event handler for when a progress progress node's value changes.
    /// </summary>
    /// <param name="progressNode">The progress node whose progress changed.</param>
    /// <param name="statusMessage">The status message (description) at the time of progress update.</param>
    /// <param name="value">The new progress value.</param>
    public delegate void ProgressNodeProgressEventHandler(IProgressNode progressNode, ProgressTask task, string statusMessage, double value);

    /// <summary>
    /// Event handler for when a progress node finishes successfully.
    /// </summary>
    /// <param name="progressNode">The progress node that finished.</param>
    public delegate void ProgressNodeFinishedEventHandler(IProgressNode progressNode, ProgressTask task);

    /// <summary>
    /// Event handler for when a progress node fails.
    /// </summary>
    /// <param name="progressNode">The progress node that failed.</param>
    /// <param name="error">The error that occurred.</param>
    public delegate void ProgressNodeFailedEventHandler(IProgressNode progressNode, ProgressTask task, Exception error);

    /// <summary>
    /// Event handler for when a progress node is canceled.
    /// </summary>
    public delegate void ProgressNodeCanceledEventHandler(IProgressNode progressNode, ProgressTask task);

    /// <summary>
    /// Event handler for when a new progress node is created as a child of a parent node.
    /// </summary>
    public delegate void ProgressNodeCreatedEventHandler(IProgressNode parentNode, IProgressNode childNode, ProgressTask childTask);
    #endregion

    /// <summary>
    /// Represents a progress node in the progress tree.
    /// </summary>
    public interface IProgressNode
    {
        #region props

        ProgressTask ProgressTask { get; }

        /// <summary>
        /// Gets the unique identifier for this progress node.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets or sets the description of the task.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the current progress value (0.0 - 1.0).
        /// </summary>
        double ProgressPercent { get; set; }

        /// <summary>
        /// Gets the read-only list of child nodes.
        /// </summary>
        List<IProgressNode> Children { get; }

        /// <summary>
        /// Gets the depth in the tree (0 for root).
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// Gets the creation time when this progress node was created.
        /// </summary>
        DateTime? CreationTime { get; }

        /// <summary>
        /// Gets the actual start time when this progress node's work began (Value > 0 or ExecuteAsync called).
        /// </summary>
        DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets the actual end time when this progress node's work finished.
        /// </summary>
        DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets the actual duration in seconds (EndTime - StartTime).
        /// Only meaningful for leaf nodes that have completed.
        /// </summary>
        double Duration { get; }

        /// <summary>
        /// Gets the current status of this progress node.
        /// </summary>
        ProgressStatus Status { get; set; }

        /// <summary>
        /// Gets the current status message of this progress node.
        /// </summary>
        string StatusMessage { get; set; }

        /// <summary>
        /// Gets the error message if the node has failed.
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// Gets whether this node runs its children in parallel.
        /// </summary>
        bool RunChildrenInParallel { get; }
        #endregion

        #region events
        /// <summary>
        /// Event raised when the progress node starts execution.
        /// </summary>
        event ProgressNodeStartedEventHandler? OnStart;

        /// <summary>
        /// Event raised when the progress node's progress value changes.
        /// </summary>
        event ProgressNodeProgressEventHandler? OnProgress;

        /// <summary>
        /// Event raised when the progress node finishes execution successfully.
        /// </summary>
        event ProgressNodeFinishedEventHandler? OnComplete;

        /// <summary>
        /// Event raised when the progress node fails during execution.
        /// </summary>
        event ProgressNodeFailedEventHandler? OnFail;

        event ProgressNodeCanceledEventHandler? OnCancel;

        event ProgressNodeCreatedEventHandler? OnChildCreated;
        #endregion

        Task ExecuteAsync(CancellationToken cancel);

        /// <summary>
        /// Adds a child task to this progress node.
        /// </summary>
        /// <param name="id">Unique identifier for the child.</param>
        /// <param name="name">Child task name.</param>
        /// <param name="runInParallel">Run child in parallel.</param>
        /// <param name="workerFunc">Main execution logic.</param>
        /// <returns>The created child progress node.</returns>
        IProgressNode AddChild(string id, string name, bool runInParallel, Func<IProgressNode, CancellationToken, Task>? workerFunc = null);

        /// <summary>
        /// Gets the current progress status of this node.
        /// </summary>
        /// <returns></returns>
        NodeProgress GetProgress();

        void Start();

        void Complete();

        void UpdateProgress(string statusMessage, double value);

        void Fail(Exception error);

        void Cancel();
    }
}
