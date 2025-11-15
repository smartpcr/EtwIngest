//-----------------------------------------------------------------------
// <copyright file="ProgressNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Spectre.Console;

    /// <summary>
    /// Concrete implementation of IProgressNode that wraps Spectre.Console's ProgressTask.
    /// </summary>
    public class ProgressNode : IProgressNode
    {
        private readonly ProgressTask task;
        private readonly ProgressContext context;
        private readonly List<IProgressNode> children = new();
        private readonly IProgressTreeManager manager;
        private readonly double weight;
        private readonly DateTime creationTime;
        private readonly Func<IProgressNode, CancellationToken, Task>? workFunc;
        private string baseDescription;
        private DateTime? startTime;
        private DateTime? finishTime;

        /// <inheritdoc/>
        public event ProgressNodeStartedEventHandler? OnStart;

        /// <inheritdoc/>
        public event ProgressNodeProgressEventHandler? OnProgress;

        /// <inheritdoc/>
        public event ProgressNodeFinishedEventHandler? OnFinish;

        /// <inheritdoc/>
        public event ProgressNodeFailedEventHandler? OnFail;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressNode"/> class.
        /// </summary>
        /// <param name="id">Unique identifier.</param>
        /// <param name="baseDescription">Base description without indentation.</param>
        /// <param name="task">Underlying ProgressTask.</param>
        /// <param name="context">Progress context.</param>
        /// <param name="manager">Progress tree manager.</param>
        /// <param name="parent">Parent node.</param>
        /// <param name="taskType">Type of task.</param>
        /// <param name="executionMode">Execution mode for children.</param>
        /// <param name="weight">Weight relative to siblings (default 1.0).</param>
        /// <param name="workFunc">Optional work function to execute.</param>
        public ProgressNode(
            string id,
            string baseDescription,
            ProgressTask task,
            ProgressContext context,
            IProgressTreeManager manager,
            IProgressNode? parent = null,
            TaskType taskType = TaskType.Job,
            ExecutionMode executionMode = ExecutionMode.Sequential,
            double weight = 1.0,
            Func<IProgressNode, CancellationToken, Task>? workFunc = null)
        {
            this.Id = id;
            this.baseDescription = baseDescription;
            this.task = task;
            this.context = context;
            this.manager = manager;
            this.Parent = parent;
            this.TaskType = taskType;
            this.ExecutionMode = executionMode;
            this.weight = weight;
            this.creationTime = DateTime.Now;
            this.workFunc = workFunc;

            // Update description with indentation
            this.UpdateDescriptionWithIndent();
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Description
        {
            get => this.task.Description;
            set
            {
                // Store base description without markup for child indentation calculation
                this.baseDescription = value;
                this.UpdateDescriptionWithTimeInfo();
                this.OnProgress?.Invoke(this, this.task.Description, this.task.Value);
            }
        }

        /// <inheritdoc/>
        public double Value
        {
            get => this.task.Value;
            set
            {
                // Track start time on first value update
                if (!this.startTime.HasValue && value > 0)
                {
                    this.startTime = DateTime.Now;

                    // Start the Spectre.Console task to begin elapsed time tracking
                    if (!this.task.IsStarted)
                    {
                        this.task.StartTask();
                    }
                }

                this.task.Value = Math.Max(0, Math.Min(value, this.MaxValue));
                this.UpdateDescriptionWithTimeInfo();
                this.UpdateParentProgress();

                // Raise progress event
                this.OnProgress?.Invoke(this, this.task.Description, this.task.Value);
            }
        }

        /// <inheritdoc/>
        public double MaxValue
        {
            get => this.task.MaxValue;
            set => this.task.MaxValue = value;
        }

        /// <inheritdoc/>
        public void ReportProgress(double currentValue)
        {
            this.Value = currentValue;
        }

        /// <inheritdoc/>
        public bool IsCompleted => this.task.Value >= this.task.MaxValue;

        /// <inheritdoc/>
        public bool IsStarted => this.task.StartTime.HasValue;

        /// <inheritdoc/>
        public IProgressNode? Parent { get; }

        /// <inheritdoc/>
        public IReadOnlyList<IProgressNode> Children => this.children.AsReadOnly();

        /// <inheritdoc/>
        public ExecutionMode ExecutionMode { get; }

        /// <inheritdoc/>
        public TaskType TaskType { get; }

        /// <inheritdoc/>
        public int Depth
        {
            get
            {
                int depth = 0;
                var current = this.Parent;
                while (current != null)
                {
                    depth++;
                    current = current.Parent;
                }

                return depth;
            }
        }

        /// <inheritdoc/>
        public double Weight => this.weight;

        /// <inheritdoc/>
        public DateTime? StartTime => this.startTime;

        /// <inheritdoc/>
        public DateTime? EndTime => this.finishTime;

        /// <inheritdoc/>
        public DateTime EffectiveStartTime
        {
            get
            {
                if (this.Children.Count > 0 && this.Children.Any(c => c.StartTime.HasValue))
                {
                    var childStart = this.Children.Where(c => c.StartTime.HasValue)
                        .Min(c => c.EffectiveStartTime);
                    return this.startTime.HasValue ?
                        new DateTime(Math.Min(this.startTime.Value.Ticks, childStart.Ticks)) :
                        childStart;
                }

                // If not started yet, use startTime if available, otherwise current time
                // This should only happen for nodes that haven't executed yet
                return this.startTime ?? this.creationTime;
            }
        }

        /// <inheritdoc/>
        public DateTime EffectiveEndTime
        {
            get
            {
                if (this.Children.Count > 0 && this.Children.Any(c => c.EndTime.HasValue))
                {
                    var childEnd = this.Children.Where(c => c.EndTime.HasValue)
                        .Max(c => c.EffectiveEndTime);
                    return this.finishTime.HasValue ?
                        new DateTime(Math.Max(this.finishTime.Value.Ticks, childEnd.Ticks)) :
                        childEnd;
                }

                return this.finishTime ?? DateTime.Now;
            }
        }

        /// <inheritdoc/>
        public double ActualDuration
        {
            get
            {
                if (!this.finishTime.HasValue || !this.startTime.HasValue)
                {
                    return 0;
                }

                return (this.finishTime.Value - this.startTime.Value).TotalSeconds;
            }
        }

        /// <inheritdoc/>
        public double EffectiveDuration
        {
            get
            {
                return (this.EffectiveEndTime - this.EffectiveStartTime).TotalSeconds;
            }
        }

        /// <inheritdoc/>
        public ExecutionMode? DetectedExecutionMode
        {
            get
            {
                if (this.Children.Count == 0)
                {
                    return null;
                }

                // Check if any children overlap in time
                var childTimes = this.Children
                    .Where(c => c.StartTime.HasValue && c.EndTime.HasValue)
                    .Select(c => (Start: c.EffectiveStartTime, End: c.EffectiveEndTime))
                    .OrderBy(c => c.Start)
                    .ToList();

                if (childTimes.Count == 0)
                {
                    return null;
                }

                // If any child starts before previous child ends, it's parallel
                for (int i = 1; i < childTimes.Count; i++)
                {
                    if (childTimes[i].Start < childTimes[i - 1].End)
                    {
                        return ExecutionMode.Parallel;
                    }
                }

                return ExecutionMode.Sequential;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Use ActualDuration or EffectiveDuration instead")]
        public double DurationSeconds
        {
            get
            {
                // If this task has children, calculate from them regardless of own startTime
                if (this.Children.Count > 0)
                {
                    if (this.ExecutionMode == ExecutionMode.Sequential)
                    {
                        // Sequential: sum of all children durations
                        return this.Children.Sum(c => c.DurationSeconds);
                    }
                    else
                    {
                        // Parallel: max of all children durations
                        return this.Children.Max(c => c.DurationSeconds);
                    }
                }

                // For leaf tasks, check if started
                if (!this.startTime.HasValue)
                {
                    return 0;
                }

                // Leaf task: return actual elapsed time
                var endTime = this.finishTime ?? DateTime.Now;
                return (endTime - this.startTime.Value).TotalSeconds;
            }
        }

        /// <inheritdoc/>
        public IProgressNode AddChild(string id, string description, TaskType taskType = TaskType.Job, ExecutionMode executionMode = ExecutionMode.Sequential, double maxValue = 100, double weight = 1.0, Func<IProgressNode, CancellationToken, Task>? workFunc = null)
        {
            // Create tree-style prefix for visual hierarchy
            int childDepth = this.Depth + 1;
            string prefix;

            if (childDepth == 1)
            {
                prefix = "├── ";
            }
            else
            {
                // Build nested tree structure: "│  " for each parent level, then "├── "
                var treeChars = string.Concat(Enumerable.Repeat("│  ", childDepth - 1));
                prefix = $"{treeChars}├── ";
            }

            var displayDescription = $"{prefix}[dim]{description}[/]";
            var childTask = this.context.AddTask(displayDescription, maxValue: maxValue);
            var child = new ProgressNode(
                id,
                description,
                childTask,
                this.context,
                this.manager,
                this,
                taskType,
                executionMode,
                weight,
                workFunc);

            this.children.Add(child);
            return child;
        }

        /// <summary>
        /// Marks this node as completed. Called internally by ExecuteAsync.
        /// </summary>
        private void Complete()
        {
            // StartTime should already be set by ExecuteAsync, but fallback to creation time if needed
            if (!this.startTime.HasValue)
            {
                this.startTime = this.creationTime;
            }

            if (!this.task.IsStarted)
            {
                this.task.StartTask();
            }

            this.finishTime = DateTime.Now;
            this.task.Value = this.MaxValue;

            // Add completion checkmark to description
            if (!this.baseDescription.StartsWith("[green]✓"))
            {
                this.baseDescription = $"[green]✓[/] {this.baseDescription}";
                this.UpdateDescriptionWithTimeInfo();
            }

            this.task.StopTask();

            // Update parent progress after completing
            this.UpdateParentProgress();

            // Raise finish event
            this.OnFinish?.Invoke(this);
        }

        /// <summary>
        /// Marks this node as failed. Called internally by ExecuteAsync.
        /// </summary>
        /// <param name="error">The error that caused the failure.</param>
        private void Fail(Exception error)
        {
            if (!this.startTime.HasValue)
            {
                this.startTime = this.creationTime;
            }

            this.finishTime = DateTime.Now;

            var indent = new string(' ', this.Depth * 2);
            var prefix = this.Depth == 0 ? string.Empty : "  ├── ";
            this.task.Description = $"{indent}{prefix}[red]✗ {error.Message}[/]";
            this.task.StopTask();

            // Raise fail event
            this.OnFail?.Invoke(this, error);
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Set start time when execution begins (if not already set by ReportProgress)
                if (!this.startTime.HasValue)
                {
                    this.startTime = DateTime.Now;
                    if (!this.task.IsStarted)
                    {
                        this.task.StartTask();
                    }
                }

                // Raise OnStart event
                this.OnStart?.Invoke(this);

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                if (this.Children.Count == 0)
                {
                    // Leaf node - execute work function if provided
                    if (this.workFunc != null)
                    {
                        await this.workFunc(this, cancellationToken);
                    }
                }
                else
                {
                    // Parent node - drive children execution based on mode
                    if (this.ExecutionMode == ExecutionMode.Parallel)
                    {
                        // Parallel: execute all children with Task.WhenAll
                        var childTasks = this.Children.Select(c => c.ExecuteAsync(cancellationToken)).ToList();
                        await Task.WhenAll(childTasks);
                    }
                    else
                    {
                        // Sequential: execute children one by one
                        foreach (var child in this.Children)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await child.ExecuteAsync(cancellationToken);
                        }
                    }
                }

                // Mark this node as complete after successful execution
                this.Complete();
            }
            catch (Exception ex)
            {
                // Mark this node as failed and raise OnFail event
                this.Fail(ex);
                throw; // Re-throw to allow caller to handle
            }
        }

        /// <inheritdoc/>
        public void MarkStarted(DateTime? startTime = null)
        {
            if (!this.startTime.HasValue)
            {
                this.startTime = startTime ?? DateTime.Now;
                if (!this.task.IsStarted)
                {
                    this.task.StartTask();
                }
            }
        }

        /// <inheritdoc/>
        public void MarkCompleted(DateTime? endTime = null)
        {
            this.finishTime = endTime ?? DateTime.Now;
            this.task.Value = this.MaxValue;
            if (!this.task.IsStarted)
            {
                this.task.StartTask();
            }
            this.task.StopTask();
        }

        /// <summary>
        /// Builds a tree-style prefix based on depth to show hierarchy.
        /// Uses box-drawing characters to create a visual tree structure.
        /// </summary>
        /// <param name="depth">The depth level.</param>
        /// <returns>Prefix string with tree characters.</returns>
        private string GetTreePrefix(int depth)
        {
            if (depth == 0)
            {
                return string.Empty;
            }
            else if (depth == 1)
            {
                return "├── ";
            }
            else
            {
                // Build nested tree structure: "│  " for each parent level, then "├── " for this level
                var treeChars = string.Concat(Enumerable.Repeat("│  ", depth - 1));
                return $"{treeChars}├── ";
            }
        }

        /// <summary>
        /// Updates the description with proper indentation based on depth.
        /// </summary>
        private void UpdateDescriptionWithIndent()
        {
            if (this.Depth == 0)
            {
                // Root task - no indentation
                this.task.Description = this.baseDescription;
            }
            else
            {
                // Child task - add tree-style prefix
                var prefix = this.GetTreePrefix(this.Depth);
                this.task.Description = $"{prefix}{this.baseDescription}";
            }
        }

        /// <summary>
        /// Updates the description with proper formatting including calculated duration.
        /// </summary>
        private void UpdateDescriptionWithTimeInfo()
        {
            // First, rebuild the base description with indentation
            string baseDesc;
            if (this.Depth == 0)
            {
                baseDesc = this.baseDescription;
            }
            else
            {
                var prefix = this.GetTreePrefix(this.Depth);
                baseDesc = $"{prefix}{this.baseDescription}";
            }

            // Add calculated duration for all tasks that have started OR have children
            // (parent tasks can calculate duration from children even without own startTime)
            if (this.startTime.HasValue || this.Children.Count > 0)
            {
                var effectiveDuration = this.EffectiveDuration;
                var actualDuration = this.ActualDuration;

                // Only show duration if there's actually some duration to show
                if (effectiveDuration > 0 || actualDuration > 0)
                {
                    // For parent tasks with children, show detected execution mode and durations
                    if (this.Children.Count > 0)
                    {
                        var detectedMode = this.DetectedExecutionMode;
                        string modeStr = detectedMode.HasValue
                            ? (detectedMode.Value == ExecutionMode.Sequential ? "S" : "P")
                            : string.Empty;

                        string effectiveDurationStr = this.FormatDuration(effectiveDuration);

                        // Show both actual and effective if they differ significantly (for completed tasks)
                        if (this.IsCompleted && actualDuration > 0 && Math.Abs(effectiveDuration - actualDuration) > 0.1)
                        {
                            string actualDurationStr = this.FormatDuration(actualDuration);
                            if (!string.IsNullOrEmpty(modeStr))
                            {
                                this.task.Description = $"{baseDesc} [grey]({modeStr} {effectiveDurationStr}, actual: {actualDurationStr})[/]";
                            }
                            else
                            {
                                this.task.Description = $"{baseDesc} [grey]({effectiveDurationStr}, actual: {actualDurationStr})[/]";
                            }
                        }
                        else
                        {
                            // Just show effective duration with mode
                            if (!string.IsNullOrEmpty(modeStr))
                            {
                                this.task.Description = $"{baseDesc} [grey]({modeStr} {effectiveDurationStr})[/]";
                            }
                            else
                            {
                                this.task.Description = $"{baseDesc} [grey]({effectiveDurationStr})[/]";
                            }
                        }
                    }
                    else
                    {
                        // For leaf tasks, just show actual duration
                        string durationStr = this.FormatDuration(actualDuration > 0 ? actualDuration : effectiveDuration);
                        this.task.Description = $"{baseDesc} [grey]({durationStr})[/]";
                    }
                }
                else
                {
                    this.task.Description = baseDesc;
                }
            }
            else
            {
                this.task.Description = baseDesc;
            }
        }

        /// <summary>
        /// Formats a duration in seconds to a human-readable string.
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        /// <returns>Formatted duration string.</returns>
        private string FormatDuration(double duration)
        {
            if (duration >= 60)
            {
                int minutes = (int)(duration / 60);
                int seconds = (int)(duration % 60);
                return $"{minutes}m{seconds:D2}s";
            }
            else if (duration >= 1.0)
            {
                return $"{duration:F1}s";
            }
            else
            {
                return $"{duration * 1000:F0}ms";
            }
        }

        /// <summary>
        /// Updates parent progress when this node's progress changes.
        /// </summary>
        private void UpdateParentProgress()
        {
            if (this.Parent == null || this.Parent.Children.Count == 0)
            {
                return;
            }

            // Calculate weighted average progress of all children
            double totalWeightedProgress = this.Parent.Children.Sum(c => c.Value * c.Weight);
            double totalWeight = this.Parent.Children.Sum(c => c.Weight);

            // Avoid division by zero
            double weightedProgress = totalWeight > 0 ? totalWeightedProgress / totalWeight : 0;

            // Update parent's value without triggering recursive updates
            var parentNode = this.Parent as ProgressNode;
            if (parentNode != null)
            {
                // Start parent task if any child has started
                if (this.IsStarted && !parentNode.task.IsStarted)
                {
                    parentNode.task.StartTask();
                    if (!parentNode.startTime.HasValue)
                    {
                        parentNode.startTime = DateTime.Now;
                    }
                }

                parentNode.task.Value = Math.Min(weightedProgress, parentNode.MaxValue);

                // Update parent's description to reflect current progress
                parentNode.UpdateDescriptionWithTimeInfo();

                // Recursively update grandparent
                parentNode.UpdateParentProgress();
            }
        }
    }
}
