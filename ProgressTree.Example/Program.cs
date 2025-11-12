//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree.Example
{
    /// <summary>
    /// Example demonstrating the hierarchical ProgressTree library for Azure Stack deployment.
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
            var deployment = new AzureStackDeployment();
            await deployment.DeployAsync(new List<string> { "AzS-Node1", "AzS-Node2", "AzS-Node3" });
        }
    }

    /// <summary>
    /// Example Azure Stack deployment using hierarchical ProgressTree.
    /// </summary>
    public class AzureStackDeployment
    {
        private IProgressTreeManager manager = new ProgressTreeManager();

        /// <summary>
        /// Executes the deployment.
        /// </summary>
        /// <param name="nodeNames">List of node names to deploy to.</param>
        /// <returns>Task.</returns>
        public async Task DeployAsync(List<string> nodeNames)
        {
            await this.manager.RunAsync("Azure Stack Deployment", ExecutionMode.Sequential, async (root) =>
            {
                // Phase 1: Pre-deployment checks (10% weight - parallel execution)
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 1.0);

                await this.RunPreDeploymentChecksAsync(preDeployment);

                // Phase 2: Deploy to nodes (80% weight - parallel execution, most time-consuming)
                var deployment = root.AddChild(
                    "deployment",
                    "Deploy to Nodes",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 8.0);

                await this.DeployToNodesAsync(deployment, nodeNames);

                // Phase 3: Post-deployment validation (10% weight - sequential)
                var postDeployment = root.AddChild(
                    "post-deployment",
                    "Post-deployment Validation",
                    TaskType.Job,
                    ExecutionMode.Sequential,
                    weight: 1.0);

                await this.RunPostDeploymentValidationAsync(postDeployment);

                // Mark root as complete
                root.Complete();

                // Wait a moment to let final display update
                await Task.Delay(500);

                // Print final summary
                Console.WriteLine($"\n\nFinal Summary:");
                Console.WriteLine($"Root: {root.Description}");
                Console.WriteLine($"  IsCompleted: {root.IsCompleted}, Value: {root.Value}");
                Console.WriteLine($"  Duration: {root.DurationSeconds:F1}s");
                Console.WriteLine($"  Children Count: {root.Children.Count}");
                Console.WriteLine($"\nPre-deploy:");
                Console.WriteLine($"  Duration: {preDeployment.DurationSeconds:F1}s (ExecutionMode: {preDeployment.ExecutionMode})");
                Console.WriteLine($"  Children: {preDeployment.Children.Count}");
                foreach (var child in preDeployment.Children)
                {
                    Console.WriteLine($"    {child.Id}: {child.DurationSeconds:F1}s");
                }
                Console.WriteLine($"\nDeploy:");
                Console.WriteLine($"  Duration: {deployment.DurationSeconds:F1}s (ExecutionMode: {deployment.ExecutionMode})");
                Console.WriteLine($"  Children: {deployment.Children.Count}");
                foreach (var child in deployment.Children)
                {
                    Console.WriteLine($"    {child.Id}: {child.DurationSeconds:F1}s");
                }
                Console.WriteLine($"\nPost-deploy:");
                Console.WriteLine($"  Duration: {postDeployment.DurationSeconds:F1}s (ExecutionMode: {postDeployment.ExecutionMode})");
                Console.WriteLine($"  Calculated Root Duration (Sequential): {preDeployment.DurationSeconds + deployment.DurationSeconds + postDeployment.DurationSeconds:F1}s");
            });
        }

        private async Task RunPreDeploymentChecksAsync(IProgressNode parent)
        {
            // Create child tasks for pre-deployment (will run in parallel due to parent's ExecutionMode)
            var networkTask = parent.AddChild("network", "Network Connectivity", TaskType.Job);
            var storageTask = parent.AddChild("storage", "Storage Validation", TaskType.Job);
            var prereqTask = parent.AddChild("prereq", "Prerequisites Check", TaskType.Job);

            // Execute in parallel
            await Task.WhenAll(
                this.CheckNetworkAsync(networkTask),
                this.CheckStorageAsync(storageTask),
                this.CheckPrerequisitesAsync(prereqTask));
        }

        private async Task DeployToNodesAsync(IProgressNode parent, List<string> nodeNames)
        {
            var tasks = new List<Task>();

            foreach (var nodeName in nodeNames)
            {
                // Each node deployment is a job with sequential stages
                var nodeTask = parent.AddChild(
                    $"deploy_{nodeName}",
                    $"Deploy to {nodeName}",
                    TaskType.Job,
                    ExecutionMode.Sequential);

                tasks.Add(this.DeployToNodeAsync(nodeTask, nodeName));

                // Stagger the starts slightly
                await Task.Delay(200);
            }

            // Execute all node deployments in parallel
            await Task.WhenAll(tasks);
        }

        private async Task DeployToNodeAsync(IProgressNode parent, string nodeName)
        {
            // Define deployment stages
            var stages = new[]
            {
                ("Connecting to node", 10),
                ("Copying binaries", 20),
                ("Installing packages", 30),
                ("Configuring services", 20),
                ("Starting services", 15),
                ("Verifying deployment", 5)
            };

            double totalWeight = stages.Sum(s => s.Item2);
            double progressPerStage = 100.0 / stages.Length;

            // Execute stages sequentially (stages are part of the same job)
            foreach (var (stepName, stepWeight) in stages)
            {
                parent.Description = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";

                // Simulate work for this stage
                for (int i = 0; i < stepWeight; i++)
                {
                    await Task.Delay(100);
                    parent.Increment(progressPerStage / stepWeight);
                }
            }

            parent.Description = $"[green]✓ {nodeName}[/]: [grey]Deployment complete[/]";
            parent.Complete();
        }

        private async Task CheckNetworkAsync(IProgressNode task)
        {
            var endpoints = new[] { "Node1", "Node2", "Node3", "Storage" };

            for (int i = 0; i < endpoints.Length; i++)
            {
                task.Description = $"[blue]Network Connectivity[/]: [grey]Testing {endpoints[i]}[/]";
                await Task.Delay(300);
                task.Value = ((i + 1) * 100.0) / endpoints.Length;
            }

            task.Description = "[green]✓ Network Connectivity[/]: [grey]All endpoints reachable[/]";
            task.Complete();
        }

        private async Task CheckStorageAsync(IProgressNode task)
        {
            for (int i = 0; i <= 100; i += 10)
            {
                string status = i < 50 ? "Checking capacity" : "Validating IOPS";
                task.Description = $"[blue]Storage Validation[/]: [grey]{status}[/]";
                task.Value = i;
                await Task.Delay(200);
            }

            task.Description = "[green]✓ Storage Validation[/]: [grey]Storage ready[/]";
        }

        private async Task CheckPrerequisitesAsync(IProgressNode task)
        {
            var checks = new[] { "OS version", "PowerShell", "Certificates", "Permissions" };

            for (int i = 0; i < checks.Length; i++)
            {
                task.Description = $"[blue]Prerequisites Check[/]: [grey]Verifying {checks[i]}[/]";
                await Task.Delay(250);
                task.Value = ((i + 1) * 100.0) / checks.Length;
            }

            task.Description = "[green]✓ Prerequisites Check[/]: [grey]All checks passed[/]";
        }

        private async Task RunPostDeploymentValidationAsync(IProgressNode parent)
        {
            // Health check with multiple services as children
            var healthTask = parent.AddChild(
                "health",
                "Health Check",
                TaskType.Job,
                ExecutionMode.Sequential);

            await this.PerformHealthCheckAsync(healthTask);
        }

        private async Task PerformHealthCheckAsync(IProgressNode parent)
        {
            var services = new[] { "Portal", "ARM", "Storage", "Compute", "Network" };

            // Create a child task for each service
            foreach (var serviceName in services)
            {
                var serviceTask = parent.AddChild(
                    $"service_{serviceName.ToLowerInvariant()}",
                    $"{serviceName} service",
                    TaskType.Stage);

                serviceTask.Description = $"[blue]{serviceName} service[/]: [grey]Testing...[/]";
                await Task.Delay(400);
                serviceTask.Complete();
                serviceTask.Description = $"[green]✓ {serviceName} service[/]: [grey]Healthy[/]";
            }

            parent.Description = "[green]✓ Health Check[/]: [grey]All services healthy[/]";
        }
    }
}
