// -----------------------------------------------------------------------
// <copyright file="ExecuteAsyncDeployment.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ProgressTree.Example
{
    /// <summary>
    /// ExecuteAsync pattern: Build tree with work functions attached, then call root.ExecuteAsync().
    /// Parent nodes automatically drive children execution based on ExecutionMode.
    /// </summary>
    public class ExecuteAsyncDeployment
    {
        private readonly IProgressTreeMonitor monitor = new ProgressTreeMonitor();

        /// <summary>
        /// Executes the deployment using ExecuteAsync pattern.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task DeployAsync()
        {
            await this.monitor.StartAsync("Azure Stack Deployment", this.BuildDeployWorkflow);
        }

        private void BuildDeployWorkflow(IProgressNode root)
        {
            // Phase 1: Pre-deployment checks with work functions
            var preDeployment = root.AddChild(
                "pre-deployment",
                "Pre-deployment Checks",
                false);
            preDeployment.AddChild("network", "Network Connectivity", false, this.CheckNetworkWorkFunc);
            preDeployment.AddChild("storage", "Storage Validation", false, this.CheckStorageWorkFunc);
            preDeployment.AddChild("prereq", "Prerequisites Check", false, this.CheckPrerequisitesWorkFunc);

            // Phase 2: Deploy to nodes
            var deployment = root.AddChild(
                "deployment",
                "Deploy to Nodes",
                true);

            List<string> nodeNames = new()
            {
                "Node1",
                "Node2",
                "Node3",
                "Node4"
            };
            foreach (var nodeName in nodeNames)
            {
                deployment.AddChild(
                    $"deploy_{nodeName}",
                    $"Deploy to {nodeName}",
                    false,
                    async (node, ct) => await this.DeployToNodeWorkFunc(node, nodeName, ct));
            }

            // Phase 3: Post-deployment validation
            var postDeployment = root.AddChild(
                "post-deployment",
                "Post-deployment Validation",
                false);

            var healthTask = postDeployment.AddChild(
                "health",
                "Health Check",
                false);

            var serviceNames = new[] { "Portal", "ARM", "Storage", "Compute", "Network" };
            foreach (var serviceName in serviceNames)
            {
                healthTask.AddChild(
                    $"service_{serviceName.ToLowerInvariant()}",
                    $"{serviceName} service",
                    false,
                    async (node, ct) => await this.PerformServiceCheckWorkFunc(node, serviceName, ct));
            }
        }

        private async Task CheckNetworkWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var endpoints = new[] { "Node1", "Node2", "Node3", "Storage" };

            for (var i = 0; i < endpoints.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                task.Name = $"[blue]Network Connectivity[/]: [grey]Testing {endpoints[i]}[/]";
                await Task.Delay(300, ct);
                task.UpdateProgress($"testing endpoint {endpoints[i]}", (i + 1.0) / endpoints.Length);
            }

            task.Name = "[green]✓ Network Connectivity[/]: [grey]All endpoints reachable[/]";
        }

        private async Task CheckStorageWorkFunc(IProgressNode task, CancellationToken ct)
        {
            for (var i = 0; i <= 100; i += 10)
            {
                ct.ThrowIfCancellationRequested();
                var status = i < 50 ? "Checking capacity" : "Validating IOPS";
                task.Name = $"[blue]Storage Validation[/]: [grey]{status}[/]";
                task.UpdateProgress(status, i * 0.01);
                await Task.Delay(200, ct);
            }

            task.Name = "[green]✓ Storage Validation[/]: [grey]Storage ready[/]";
        }

        private async Task CheckPrerequisitesWorkFunc(IProgressNode task, CancellationToken ct)
        {
            var checks = new[] { "OS version", "PowerShell", "Certificates", "Permissions" };

            for (var i = 0; i < checks.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                task.Name = $"[blue]Prerequisites Check[/]: [grey]Verifying {checks[i]}[/]";
                await Task.Delay(250, ct);
                task.UpdateProgress($"Verifying {checks[i]}", (i + 1.0) / checks.Length);
            }

            task.Name = "[green]✓ Prerequisites Check[/]: [grey]All checks passed[/]";
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
            var totalWeight = stages.Sum(s => s.Item2);

            foreach (var (stepName, stepWeight) in stages)
            {
                ct.ThrowIfCancellationRequested();
                parent.Name = $"[yellow]{nodeName}[/]: [grey]{stepName}...[/]";
                await Task.Delay(100, ct);
                currentProgress += stepWeight;
                parent.UpdateProgress(stepName, currentProgress * 1.0 / totalWeight);
            }

            parent.Name = $"[green]✓ {nodeName}[/]: [grey]Deployment complete[/]";
        }

        private async Task PerformServiceCheckWorkFunc(IProgressNode serviceTask, string serviceName, CancellationToken ct)
        {
            serviceTask.Name = $"[blue]{serviceName} service[/]: [grey]Testing...[/]";
            await Task.Delay(400, ct);
            serviceTask.Name = $"[green]✓ {serviceName} service[/]: [grey]Healthy[/]";
        }
    }
}