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
                int completedPhases = 0;
                List<string> eventLog = new List<string>();

                // Hook up event handlers - log to memory instead of console during rendering
                root.OnStart += (node) => eventLog.Add($"Root started: {node.Id}");
                root.OnFinish += (node) => eventLog.Add($"Root finished! Total duration: {node.DurationSeconds:F1}s");
                root.OnFail += (node, error) => eventLog.Add($"Root failed: {error.Message}");

                // Phase 1: Pre-deployment checks with work functions
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 1.0);

                // Event handlers collect metrics without console output
                preDeployment.OnFinish += (node) =>
                {
                    completedPhases++;
                    eventLog.Add($"Pre-deployment completed in {node.DurationSeconds:F1}s");
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
                    eventLog.Add($"Deployment to all nodes completed in {node.DurationSeconds:F1}s");
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
                    nodeTask.OnFinish += (node) => eventLog.Add($"{nodeName} deployment finished in {node.DurationSeconds:F1}s");
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
                    eventLog.Add($"Post-deployment validation completed in {node.DurationSeconds:F1}s");
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
                Console.WriteLine($"Total Duration: {root.DurationSeconds:F1}s");
                Console.WriteLine($"Pre-deployment: {preDeployment.DurationSeconds:F1}s (Parallel)");
                Console.WriteLine($"Deployment: {deployment.DurationSeconds:F1}s (Parallel)");
                Console.WriteLine($"Post-deployment: {postDeployment.DurationSeconds:F1}s (Sequential)");
                Console.WriteLine($"Completed Phases: {completedPhases}/3");
            });
        }

        private async Task CheckNetworkWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var endpoints = new[] { "Node1", "Node2", "Node3", "Storage" };

            for (int i = 0; i < endpoints.Length; i++)
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
            for (int i = 0; i <= 100; i += 10)
            {
                ct.ThrowIfCancellationRequested();
                string status = i < 50 ? "Checking capacity" : "Validating IOPS";
                task.Description = $"[blue]Storage Validation[/]: [grey]{status}[/]";
                task.ReportProgress(i);
                await Task.Delay(200, ct);
            }

            task.Description = "[green]✓ Storage Validation[/]: [grey]Storage ready[/]";
        }

        private async Task CheckPrerequisitesWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var checks = new[] { "OS version", "PowerShell", "Certificates", "Permissions" };

            for (int i = 0; i < checks.Length; i++)
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
            double progressPerStage = 100.0 / stages.Length;

            foreach (var (stepName, stepWeight) in stages)
            {
                ct.ThrowIfCancellationRequested();
                parent.Description = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";

                for (int i = 0; i < stepWeight; i++)
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
}
