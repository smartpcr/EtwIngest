//-----------------------------------------------------------------------
// <copyright file="ProgressTreeManager.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Spectre.Console;

    /// <summary>
    /// Manages a hierarchical progress tree using Spectre.Console.
    /// </summary>
    public class ProgressTreeManager : IProgressTreeManager
    {
        private ProgressContext? context;
        private IProgressNode? rootTask;
        private bool disposed = false;

        /// <inheritdoc/>
        public IProgressNode? RootTask => this.rootTask;

        /// <inheritdoc/>
        public int TaskCount => this.rootTask == null ? 0 : this.CountAllTasks(this.rootTask);

        /// <inheritdoc/>
        public int CompletedTaskCount => this.rootTask == null ? 0 : this.CountCompletedTasks(this.rootTask);

        /// <inheritdoc/>
        public double OverallProgress
        {
            get
            {
                if (this.rootTask == null)
                {
                    return 0;
                }

                return this.rootTask.Value;
            }
        }

        /// <inheritdoc/>
        public async Task RunAsync(string rootDescription, ExecutionMode executionMode, Func<IProgressNode, Task> action)
        {
            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .AutoClear(true)  // Auto-clear to hide live rendering
                .HideCompleted(true)  // Hide completed tasks during live rendering
                .Columns(
                    new TaskDescriptionColumn { Alignment = Justify.Left },
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    this.context = ctx;
                    var rootProgressTask = ctx.AddTask($"[bold cyan]{rootDescription}[/]", maxValue: 100);

                    // Create root task without parent
                    this.rootTask = new ProgressNode(
                        "__root__",
                        $"[bold cyan]{rootDescription}[/]",
                        rootProgressTask,
                        ctx,
                        this,
                        null,
                        TaskType.Job,
                        executionMode,
                        1.0,
                        null);

                    await action(this.rootTask);

                    // Root task is automatically updated by child completion cascading up the tree
                });

            // After execution completes, render the final tree with proportional progress bars
            if (this.rootTask != null)
            {
                Console.WriteLine();
                WorkflowTreeRenderer.RenderCompleted(this.rootTask);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.context = null;
                    this.rootTask = null;
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Counts all tasks in the tree recursively.
        /// </summary>
        /// <param name="node">Node to start counting from.</param>
        /// <returns>Total task count.</returns>
        private int CountAllTasks(IProgressNode node)
        {
            var count = 1; // Count this node
            foreach (var child in node.Children)
            {
                count += this.CountAllTasks(child);
            }

            return count;
        }

        /// <summary>
        /// Counts completed tasks in the tree recursively.
        /// </summary>
        /// <param name="node">Node to start counting from.</param>
        /// <returns>Completed task count.</returns>
        private int CountCompletedTasks(IProgressNode node)
        {
            var count = node.IsCompleted ? 1 : 0;
            foreach (var child in node.Children)
            {
                count += this.CountCompletedTasks(child);
            }

            return count;
        }
    }
}
