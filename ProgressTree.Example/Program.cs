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
    /// Static tree construction: Build the entire tree structure before execution.
    /// All nodes are created upfront, then work is executed on the pre-built tree.
    /// </summary>
    public class StaticTreeDeployment
    {
        private IProgressTreeManager manager = new ProgressTreeManager();

        /// <summary>
        /// Executes the deployment with static tree construction.
        /// </summary>
        /// <param name="nodeNames">List of node names to deploy to.</param>
        /// <returns>Task.</returns>
        public async Task DeployAsync(List<string> nodeNames)
        {
            await this.manager.RunAsync("Azure Stack Deployment", ExecutionMode.Sequential, async (root) =>
            {
                // STATIC TREE CONSTRUCTION: Build entire tree structure BEFORE execution
                Console.WriteLine("Building tree structure...");

                // Phase 1: Pre-deployment checks
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 1.0);

                var networkTask = preDeployment.AddChild("network", "Network Connectivity", TaskType.Job);
                var storageTask = preDeployment.AddChild("storage", "Storage Validation", TaskType.Job);
                var prereqTask = preDeployment.AddChild("prereq", "Prerequisites Check", TaskType.Job);

                // Phase 2: Deploy to nodes
                var deployment = root.AddChild(
                    "deployment",
                    "Deploy to Nodes",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 8.0);

                var nodeTasks = new List<IProgressNode>();
                foreach (var nodeName in nodeNames)
                {
                    var nodeTask = deployment.AddChild(
                        $"deploy_{nodeName}",
                        $"Deploy to {nodeName}",
                        TaskType.Job,
                        ExecutionMode.Sequential);
                    nodeTasks.Add(nodeTask);
                }

                // Phase 3: Post-deployment validation
                var postDeployment = root.AddChild(
                    "post-deployment",
                    "Post-deployment Validation",
                    TaskType.Job,
                    ExecutionMode.Sequential,
                    weight: 1.0);

                var healthTask = postDeployment.AddChild(
                    "health",
                    "Health Check",
                    TaskType.Job,
                    ExecutionMode.Sequential);

                var serviceNames = new[] { "Portal", "ARM", "Storage", "Compute", "Network" };
                var serviceTasks = new List<IProgressNode>();
                foreach (var serviceName in serviceNames)
                {
                    var serviceTask = healthTask.AddChild(
                        $"service_{serviceName.ToLowerInvariant()}",
                        $"{serviceName} service",
                        TaskType.Stage);
                    serviceTasks.Add(serviceTask);
                }

                Console.WriteLine($"Tree structure complete: {root.Children.Count} phases, {preDeployment.Children.Count + deployment.Children.Count + postDeployment.Children.Count} total jobs\n");

                // NOW EXECUTE: Run work on the pre-built tree
                await Task.WhenAll(
                    this.CheckNetworkAsync(networkTask),
                    this.CheckStorageAsync(storageTask),
                    this.CheckPrerequisitesAsync(prereqTask));

                var deployTasks = new List<Task>();
                for (int i = 0; i < nodeNames.Count; i++)
                {
                    deployTasks.Add(this.DeployToNodeAsync(nodeTasks[i], nodeNames[i]));
                    await Task.Delay(200); // Stagger starts
                }
                await Task.WhenAll(deployTasks);

                for (int i = 0; i < serviceNames.Length; i++)
                {
                    await this.PerformServiceCheckAsync(serviceTasks[i], serviceNames[i]);
                }
                healthTask.Complete();

                root.Complete();
                await Task.Delay(500);
            });
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

        private async Task DeployToNodeAsync(IProgressNode parent, string nodeName)
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

            double totalWeight = stages.Sum(s => s.Item2);
            double progressPerStage = 100.0 / stages.Length;

            foreach (var (stepName, stepWeight) in stages)
            {
                parent.Description = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";

                for (int i = 0; i < stepWeight; i++)
                {
                    await Task.Delay(100);
                    parent.Increment(progressPerStage / stepWeight);
                }
            }

            parent.Description = $"[green]✓ {nodeName}[/]: [grey]Deployment complete[/]";
            parent.Complete();
        }

        private async Task PerformServiceCheckAsync(IProgressNode serviceTask, string serviceName)
        {
            serviceTask.Description = $"[blue]{serviceName} service[/]: [grey]Testing...[/]";
            await Task.Delay(400);
            serviceTask.Complete();
            serviceTask.Description = $"[green]✓ {serviceName} service[/]: [grey]Healthy[/]";
        }
    }

    /// <summary>
    /// Dynamic tree construction: Build nodes during execution as needed.
    /// This is the original approach where children are added during the execution flow.
    /// </summary>
    public class DynamicTreeDeployment
    {
        private IProgressTreeManager manager = new ProgressTreeManager();

        /// <summary>
        /// Executes the deployment with dynamic tree construction.
        /// </summary>
        /// <param name="nodeNames">List of node names to deploy to.</param>
        /// <returns>Task.</returns>
        public async Task DeployAsync(List<string> nodeNames)
        {
            await this.manager.RunAsync("Azure Stack Deployment", ExecutionMode.Sequential, async (root) =>
            {
                // DYNAMIC TREE CONSTRUCTION: Add children as we go, interleaved with execution

                // Phase 1: Create pre-deployment node, then EXECUTE and add its children dynamically
                Console.WriteLine("Adding Pre-deployment phase...");
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 1.0);

                await this.RunPreDeploymentChecksAsync(preDeployment);  // Adds children during execution

                // Phase 2: Create deployment node, then EXECUTE and add node children dynamically
                Console.WriteLine("Adding Deploy to Nodes phase...");
                var deployment = root.AddChild(
                    "deployment",
                    "Deploy to Nodes",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 8.0);

                await this.DeployToNodesAsync(deployment, nodeNames);  // Adds children during execution

                // Phase 3: Create post-deployment node, then EXECUTE and add its children dynamically
                Console.WriteLine("Adding Post-deployment phase...");
                var postDeployment = root.AddChild(
                    "post-deployment",
                    "Post-deployment Validation",
                    TaskType.Job,
                    ExecutionMode.Sequential,
                    weight: 1.0);

                await this.RunPostDeploymentValidationAsync(postDeployment);  // Adds children during execution

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

                // Phase 1: Pre-deployment checks with work functions
                var preDeployment = root.AddChild(
                    "pre-deployment",
                    "Pre-deployment Checks",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 1.0);

                preDeployment.AddChild("network", "Network Connectivity", workFunc: this.CheckNetworkWorkFunc);
                preDeployment.AddChild("storage", "Storage Validation", workFunc: this.CheckStorageWorkFunc);
                preDeployment.AddChild("prereq", "Prerequisites Check", workFunc: this.CheckPrerequisitesWorkFunc);

                // Phase 2: Deploy to nodes
                var deployment = root.AddChild(
                    "deployment",
                    "Deploy to Nodes",
                    TaskType.Job,
                    ExecutionMode.Parallel,
                    weight: 8.0);

                foreach (var nodeName in nodeNames)
                {
                    var nodeTask = deployment.AddChild(
                        $"deploy_{nodeName}",
                        $"Deploy to {nodeName}",
                        TaskType.Job,
                        ExecutionMode.Sequential,
                        workFunc: async (node, ct) => await this.DeployToNodeWorkFunc(node, nodeName, ct));
                }

                // Phase 3: Post-deployment validation
                var postDeployment = root.AddChild(
                    "post-deployment",
                    "Post-deployment Validation",
                    TaskType.Job,
                    ExecutionMode.Sequential,
                    weight: 1.0);

                var healthTask = postDeployment.AddChild(
                    "health",
                    "Health Check",
                    TaskType.Job,
                    ExecutionMode.Sequential);

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
                await root.ExecuteAsync();

                await Task.Delay(500);
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
                task.Value = ((i + 1) * 100.0) / endpoints.Length;
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
                task.Value = i;
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
                task.Value = ((i + 1) * 100.0) / checks.Length;
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

            double progressPerStage = 100.0 / stages.Length;

            foreach (var (stepName, stepWeight) in stages)
            {
                ct.ThrowIfCancellationRequested();
                parent.Description = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";

                for (int i = 0; i < stepWeight; i++)
                {
                    await Task.Delay(100, ct);
                    parent.Increment(progressPerStage / stepWeight);
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
