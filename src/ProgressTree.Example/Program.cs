//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree.Example
{
    /// <summary>
    /// Example demonstrating the hierarchical ProgressTree library for Azure Stack deployment.
    /// Demonstrates both static tree construction (build tree first) and dynamic tree construction (add nodes during execution).
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== PROGRESSTREE WITH EXECUTEAFYNC & EVENTS ===");
            Console.WriteLine("Build tree with work functions, then call root.ExecuteAsync()");
            Console.WriteLine("Parent automatically drives children execution (Parallel/Sequential)");
            Console.WriteLine("Use events (OnStart, OnProgress, OnFinish, OnFail) to hook additional logic\n");

            var deployment = new ExecuteAsyncDeployment();
            await deployment.DeployAsync(new List<string> { "AzS-Node1", "AzS-Node2", "AzS-Node3" });

            Console.WriteLine("\n\n=== PROGRESSTREE WITH EFFECTIVE TIMING DEMO ===");
            Console.WriteLine("Demonstrates automatic execution mode detection and effective timing");
            Console.WriteLine("Parent starts at earliest child start, ends at latest child end\n");

            var timingDemo = new EffectiveTimingDemo();
            await timingDemo.RunAsync();
        }
    }


    /// <summary>
    /// ExecuteAsync pattern: Build tree with work functions attached, then call root.ExecuteAsync().
    /// Parent nodes automatically drive children execution based on ExecutionMode.
    /// </summary>
    public class ExecuteAsyncDeployment
    {
        private IProgressTreeManager manager = new ProgressTreeManager();

        /// <summary>
        /// Executes the deployment using ExecuteAsync pattern.
        /// </summary>
        /// <param name="nodeNames">List of node names to deploy to.</param>
        /// <returns>Task.</returns>
        public async Task DeployAsync(List<string> nodeNames)
        {
            await this.manager.RunAsync("Azure Stack Deployment", ExecutionMode.Sequential, async (root) =>
            {
                Console.WriteLine("Building tree with work functions attached...\n");

                // Track metrics using events without disrupting console display
                var completedPhases = 0;
                var eventLog = new List<string>();

                // Hook up event handlers - log to memory instead of console during rendering
                root.OnStart += (node) => eventLog.Add($"Root started: {node.Id}");
                root.OnFinish += (node) => eventLog.Add($"Root finished! Total duration: {node.EffectiveDuration:F1}s");
                root.OnFail += (node, error) => eventLog.Add($"Root failed: {error.Message}");

                // Phase 1: Pre-deployment checks with work functions
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Sequential,  // Sequential: network → storage → prereq
                    weight: 1.0);

                // Event handlers collect metrics without console output
                preDeployment.OnFinish += (node) =>
                {
                    completedPhases++;
                    eventLog.Add($"Pre-deployment completed in {node.EffectiveDuration:F1}s");
                };

                var networkNode = preDeployment.AddChild("network", "Network Connectivity", workFunc: this.CheckNetworkWorkFunc);
                var storageNode = preDeployment.AddChild("storage", "Storage Validation", workFunc: this.CheckStorageWorkFunc);
                var prereqNode = preDeployment.AddChild("prereq", "Prerequisites Check", workFunc: this.CheckPrerequisitesWorkFunc);

                // Phase 2: Deploy to nodes
                var deployment = root.AddChild(
                    "deployment",
                    "Deploy to Nodes",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 8.0);

                deployment.OnFinish += (node) =>
                {
                    completedPhases++;
                    eventLog.Add($"Deployment to all nodes completed in {node.EffectiveDuration:F1}s");
                };

                foreach (var nodeName in nodeNames)
                {
                    var nodeTask = deployment.AddChild(
                        $"deploy_{nodeName}",
                        $"Deploy to {nodeName}",
                        TaskType.Job,
                        ExecutionMode.Sequential,
                        workFunc: async (node, ct) => await this.DeployToNodeWorkFunc(node, nodeName, ct));

                    // Track individual node completion
                    nodeTask.OnFinish += (node) => eventLog.Add($"{nodeName} deployment finished in {node.EffectiveDuration:F1}s");
                }

                // Phase 3: Post-deployment validation
                var postDeployment = root.AddChild(
                    "post-deployment",
                    "Post-deployment Validation",
                    TaskType.Job,
                    ExecutionMode.Sequential,
                    weight: 1.0);

                postDeployment.OnFinish += (node) =>
                {
                    completedPhases++;
                    eventLog.Add($"Post-deployment validation completed in {node.EffectiveDuration:F1}s");
                };

                var healthTask = postDeployment.AddChild(
                    "health",
                    "Health Check",
                    TaskType.Job,
                    ExecutionMode.Sequential);

                healthTask.OnFinish += (node) => eventLog.Add($"All health checks passed!");

                var serviceNames = new[] { "Portal", "ARM", "Storage", "Compute", "Network" };
                foreach (var serviceName in serviceNames)
                {
                    healthTask.AddChild(
                        $"service_{serviceName.ToLowerInvariant()}",
                        $"{serviceName} service",
                        TaskType.Stage,
                        workFunc: async (node, ct) => await this.PerformServiceCheckWorkFunc(node, serviceName, ct));
                }

                Console.WriteLine("Tree structure complete. Starting execution...\n");

                // Execute the entire tree - root drives execution of all children
                // Parent nodes automatically execute children based on ExecutionMode (Parallel/Sequential)
                // Complete() is called automatically by ExecuteAsync after successful execution
                // Events (OnStart, OnProgress, OnFinish, OnFail) fire during execution
                await root.ExecuteAsync();

                await Task.Delay(500);

                // Print event log and summary after rendering completes
                Console.WriteLine($"\n=== Event Log ===");
                foreach (var logEntry in eventLog)
                {
                    Console.WriteLine($"  • {logEntry}");
                }

                Console.WriteLine($"\n=== Summary ===");
                Console.WriteLine($"Total Duration: {root.EffectiveDuration:F1}s");
                Console.WriteLine($"Pre-deployment: {preDeployment.EffectiveDuration:F1}s (Detected: {preDeployment.DetectedExecutionMode})");
                Console.WriteLine($"Deployment: {deployment.EffectiveDuration:F1}s (Detected: {deployment.DetectedExecutionMode})");
                Console.WriteLine($"Post-deployment: {postDeployment.EffectiveDuration:F1}s (Detected: {postDeployment.DetectedExecutionMode})");
                Console.WriteLine($"Completed Phases: {completedPhases}/3");
            });
        }

        private async Task CheckNetworkWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var endpoints = new[] { "Node1", "Node2", "Node3", "Storage" };

            for (var i = 0; i < endpoints.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                task.Description = $"[blue]Network Connectivity[/]: [grey]Testing {endpoints[i]}[/]";
                await Task.Delay(300, ct);
                task.ReportProgress(((i + 1) * 100.0) / endpoints.Length);
            }

            task.Description = "[green]✓ Network Connectivity[/]: [grey]All endpoints reachable[/]";
        }

        private async Task CheckStorageWorkFunc(IProgressNode task, CancellationToken ct)
        {
            for (var i = 0; i <= 100; i += 10)
            {
                ct.ThrowIfCancellationRequested();
                var status = i < 50 ? "Checking capacity" : "Validating IOPS";
                task.Description = $"[blue]Storage Validation[/]: [grey]{status}[/]";
                task.ReportProgress(i);
                await Task.Delay(200, ct);
            }

            task.Description = "[green]✓ Storage Validation[/]: [grey]Storage ready[/]";
        }

        private async Task CheckPrerequisitesWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var checks = new[] { "OS version", "PowerShell", "Certificates", "Permissions" };

            for (var i = 0; i < checks.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                task.Description = $"[blue]Prerequisites Check[/]: [grey]Verifying {checks[i]}[/]";
                await Task.Delay(250, ct);
                task.ReportProgress(((i + 1) * 100.0) / checks.Length);
            }

            task.Description = "[green]✓ Prerequisites Check[/]: [grey]All checks passed[/]";
        }

        private async Task DeployToNodeWorkFunc(IProgressNode parent, string nodeName, CancellationToken ct)
        {
            var stages = new[]
            {
                ("Connecting to node", 10),
                ("Copying binaries", 20),
                ("Installing packages", 30),
                ("Configuring services", 20),
                ("Starting services", 15),
                ("Verifying deployment", 5)
            };

            double currentProgress = 0;
            var progressPerStage = 100.0 / stages.Length;

            foreach (var (stepName, stepWeight) in stages)
            {
                ct.ThrowIfCancellationRequested();
                parent.Description = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";

                for (var i = 0; i < stepWeight; i++)
                {
                    await Task.Delay(100, ct);
                    currentProgress += progressPerStage / stepWeight;
                    parent.ReportProgress(currentProgress);
                }
            }

            parent.Description = $"[green]✓ {nodeName}[/]: [grey]Deployment complete[/]";
        }

        private async Task PerformServiceCheckWorkFunc(IProgressNode serviceTask, string serviceName, CancellationToken ct)
        {
            serviceTask.Description = $"[blue]{serviceName} service[/]: [grey]Testing...[/]";
            await Task.Delay(400, ct);
            serviceTask.Description = $"[green]✓ {serviceName} service[/]: [grey]Healthy[/]";
        }
    }

    /// <summary>
    /// Demonstrates effective timing features:
    /// - EffectiveStartTime: parent starts at earliest child start
    /// - EffectiveEndTime: parent ends at latest child end
    /// - DetectedExecutionMode: auto-detects parallel/sequential from actual time overlaps
    /// - Dynamic node creation: nodes created during workflow execution
    /// </summary>
    public class EffectiveTimingDemo
    {
        private IProgressTreeManager manager = new ProgressTreeManager();

        public async Task RunAsync()
        {
            await this.manager.RunAsync("Workflow Execution Demo", ExecutionMode.Sequential, async (root) =>
            {
                // Create parent containers with declared ExecutionMode
                var parallelTasks = root.AddChild(
                    "parallel-group",
                    "Parallel Task Group",
                    TaskType.Job,
                    ExecutionMode.Parallel,  // Declared as Parallel
                    weight: 1.0);

                var sequentialTasks = root.AddChild(
                    "sequential-group",
                    "Sequential Task Group",
                    TaskType.Job,
                    ExecutionMode.Sequential,  // Declared as Sequential
                    weight: 1.0);

                // Dynamic node creation: add children with work functions
                // Children will execute in parallel and DetectedExecutionMode should reflect this
                parallelTasks.AddChild("task1", "Task 1", workFunc: async (node, ct) =>
                {
                    await Task.Delay(500, ct);
                    node.ReportProgress(50);
                    await Task.Delay(500, ct);
                    node.ReportProgress(100);
                });

                parallelTasks.AddChild("task2", "Task 2", workFunc: async (node, ct) =>
                {
                    await Task.Delay(300, ct);
                    node.ReportProgress(50);
                    await Task.Delay(300, ct);
                    node.ReportProgress(100);
                });

                parallelTasks.AddChild("task3", "Task 3", workFunc: async (node, ct) =>
                {
                    await Task.Delay(700, ct);
                    node.ReportProgress(50);
                    await Task.Delay(300, ct);
                    node.ReportProgress(100);
                });

                // Sequential children
                sequentialTasks.AddChild("step1", "Step 1", workFunc: async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.ReportProgress(100);
                });

                sequentialTasks.AddChild("step2", "Step 2", workFunc: async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.ReportProgress(100);
                });

                sequentialTasks.AddChild("step3", "Step 3", workFunc: async (node, ct) =>
                {
                    await Task.Delay(400, ct);
                    node.ReportProgress(100);
                });

                // Execute the tree
                await root.ExecuteAsync();

                await Task.Delay(500);

                // Tree is automatically rendered by ProgressTreeManager after execution
                // Print timing analysis
                Console.WriteLine("\n=== Timing Analysis ===");
                this.PrintTimingInfo("Root", root);
                this.PrintTimingInfo("Parallel Group", parallelTasks);
                foreach (var child in parallelTasks.Children)
                {
                    this.PrintTimingInfo($"  {child.Id}", child);
                }

                this.PrintTimingInfo("Sequential Group", sequentialTasks);
                foreach (var child in sequentialTasks.Children)
                {
                    this.PrintTimingInfo($"  {child.Id}", child);
                }

                Console.WriteLine("\n=== Key Observations ===");
                Console.WriteLine($"Parallel Group:");
                Console.WriteLine($"  - Declared Mode: {parallelTasks.ExecutionMode}");
                Console.WriteLine($"  - Detected Mode: {parallelTasks.DetectedExecutionMode}");
                Console.WriteLine($"  - Effective Duration: {parallelTasks.EffectiveDuration:F1}s (longest child: ~1.0s)");
                Console.WriteLine($"  - Children overlapped in time → Detected as Parallel");

                Console.WriteLine($"\nSequential Group:");
                Console.WriteLine($"  - Declared Mode: {sequentialTasks.ExecutionMode}");
                Console.WriteLine($"  - Detected Mode: {sequentialTasks.DetectedExecutionMode}");
                Console.WriteLine($"  - Effective Duration: {sequentialTasks.EffectiveDuration:F1}s (sum of children: ~1.2s)");
                Console.WriteLine($"  - Children ran one after another → Detected as Sequential");
            });
        }

        private void PrintTimingInfo(string label, IProgressNode node)
        {
            Console.WriteLine($"{label}:");
            Console.WriteLine($"  StartTime: {node.StartTime?.ToString("HH:mm:ss.fff") ?? "N/A"}");
            Console.WriteLine($"  EndTime: {node.EndTime?.ToString("HH:mm:ss.fff") ?? "N/A"}");
            Console.WriteLine($"  EffectiveStartTime: {node.EffectiveStartTime:HH:mm:ss.fff}");
            Console.WriteLine($"  EffectiveEndTime: {node.EffectiveEndTime:HH:mm:ss.fff}");
            Console.WriteLine($"  ActualDuration: {node.ActualDuration:F3}s");
            Console.WriteLine($"  EffectiveDuration: {node.EffectiveDuration:F3}s");
            Console.WriteLine($"  DetectedMode: {node.DetectedExecutionMode?.ToString() ?? "N/A"}");
            Console.WriteLine();
        }
    }
}
