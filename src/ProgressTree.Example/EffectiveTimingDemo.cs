// -----------------------------------------------------------------------
// <copyright file="EffectiveTimingDemo.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ProgressTree.Example
{
    /// <summary>
    /// Demonstrates effective timing features:
    /// - EffectiveStartTime: parent starts at earliest child start
    /// - EffectiveEndTime: parent ends at latest child end
    /// - DetectedExecutionMode: auto-detects parallel/sequential from actual time overlaps
    /// - Dynamic node creation: nodes created during workflow execution
    /// </summary>
    public class EffectiveTimingDemo
    {
        private readonly IProgressTreeMonitor monitor = new ProgressTreeMonitor();

        public async Task RunAsync()
        {
            await this.monitor.StartAsync("Workflow Execution Demo", (root) =>
            {
                // Create parent containers with declared ExecutionMode
                var parallelTasks = root.AddChild(
                    "parallel-group",
                    "Parallel Task Group",
                    true);

                var sequentialTasks = root.AddChild(
                    "sequential-group",
                    "Sequential Task Group",
                    false);

                // Dynamic node creation: add children with work functions
                // Children will execute in parallel and DetectedExecutionMode should reflect this
                parallelTasks.AddChild("task1", "Task 1", false, async (node, ct) =>
                {
                    await Task.Delay(500, ct);
                    node.UpdateProgress("running...", 0.5);
                    await Task.Delay(500, ct);
                    node.UpdateProgress("Done", 1.0);
                });

                parallelTasks.AddChild("task2", "Task 2", false, async (node, ct) =>
                {
                    await Task.Delay(500, ct);
                    node.UpdateProgress("running...", 0.5);
                    await Task.Delay(500, ct);
                    node.UpdateProgress("Done", 1.0);
                });

                parallelTasks.AddChild("task3", "Task 3", false, async (node, ct) =>
                {
                    await Task.Delay(500, ct);
                    node.UpdateProgress("running...", 0.5);
                    await Task.Delay(500, ct);
                    node.UpdateProgress("Done", 1.0);
                });

                // Sequential children
                sequentialTasks.AddChild("step1", "Step 1", false, async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.UpdateProgress("Done", 1.0);
                });

                sequentialTasks.AddChild("step2", "Step 2", false, async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.UpdateProgress("Done", 1.0);
                });

                sequentialTasks.AddChild("step3", "Step 3", false, async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.UpdateProgress("Done", 1.0);
                });

            });
        }
    }
}