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
        private readonly Action? onProgressChanged;
        private readonly ProgressContext context;
        private readonly List<IProgressNode> children = new();
        private readonly IProgressTreeManager manager;
        private readonly double weight;
        private readonly DateTime creationTime;
        private string baseDescription;
        private DateTime? startTime;
        private DateTime? finishTime;

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
        /// <param name="onProgressChanged">Callback when progress changes.</param>
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
            Action? onProgressChanged = null)
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
            this.onProgressChanged = onProgressChanged;
            this.creationTime = DateTime.Now;

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
                this.onProgressChanged?.Invoke();
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
                this.onProgressChanged?.Invoke();
            }
        }

        /// <inheritdoc/>
        public double MaxValue
        {
            get => this.task.MaxValue;
            set => this.task.MaxValue = value;
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
        public IProgressNode AddChild(string id, string description, TaskType taskType = TaskType.Job, ExecutionMode executionMode = ExecutionMode.Sequential, double maxValue = 100, double weight = 1.0)
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
                this.onProgressChanged);

            this.children.Add(child);
            return child;
        }

        /// <inheritdoc/>
        public void Increment(double amount)
        {
            this.Value += amount;
        }

        /// <inheritdoc/>
        public void Complete()
        {
            // If startTime wasn't set (task didn't update Value), use creation time
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
        }

        /// <inheritdoc/>
        public void Fail(string errorMessage)
        {
            var indent = new string(' ', this.Depth * 2);
            var prefix = this.Depth == 0 ? string.Empty : "  ├── ";
            this.task.Description = $"{indent}{prefix}[red]✗ {errorMessage}[/]";
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
                var duration = this.DurationSeconds;

                // Only show duration if there's actually some duration to show
                if (duration > 0)
                {
                    string durationStr;

                    if (duration >= 60)
                    {
                        int minutes = (int)(duration / 60);
                        int seconds = (int)(duration % 60);
                        durationStr = $"{minutes}m{seconds:D2}s";
                    }
                    else if (duration >= 1.0)
                    {
                        durationStr = $"{duration:F1}s";
                    }
                    else
                    {
                        durationStr = $"{duration * 1000:F0}ms";
                    }

                    // For parent tasks, show execution mode indicator
                    if (this.Children.Count > 0)
                    {
                        var mode = this.ExecutionMode == ExecutionMode.Sequential ? "S" : "P";
                        this.task.Description = $"{baseDesc} [grey]({mode} {durationStr})[/]";
                    }
                    else
                    {
                        // For leaf tasks, just show duration
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
