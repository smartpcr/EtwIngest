using ExecutionEngine.Contexts;
using ExecutionEngine.Engine;
using ExecutionEngine.Workflow;
using ProgressTree;

namespace ExecutionEngine.Example;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== ExecutionEngine Examples with ProgressTree ===\n");

        // Load workflow serializer
        var serializer = new WorkflowSerializer();

        // Example 1: Customer Order Processing (Sequential)
        Console.WriteLine("Example 1: Customer Order Processing (Sequential)");
        Console.WriteLine("--------------------------------------------------");
        var sequentialPath = Path.Combine("Workflows", "Sequential.yaml");
        var workflow1 = serializer.LoadFromFile(sequentialPath);

        // Fix assembly paths to be absolute
        var assemblyPath = typeof(Program).Assembly.Location;
        foreach (var node in workflow1.Nodes)
        {
            if (!string.IsNullOrEmpty(node.AssemblyPath))
            {
                node.AssemblyPath = assemblyPath;
            }
        }

        Console.WriteLine($"Loaded workflow: {workflow1.WorkflowName}");
        Console.WriteLine($"Nodes: {workflow1.Nodes.Count}, Connections: {workflow1.Connections.Count}");
        Console.WriteLine($"Entry nodes (no incoming): {string.Join(", ", workflow1.Nodes.Select(n => n.NodeId).Except(workflow1.Connections.Select(c => c.TargetNodeId)))}");
        await RunWorkflowWithProgressAsync(workflow1);

        // Example 2: Data Analytics Pipeline (Parallel)
        Console.WriteLine("\nExample 2: Data Analytics Pipeline (Parallel)");
        Console.WriteLine("----------------------------------------------");
        var parallelPath = Path.Combine("Workflows", "Parallel.yaml");
        var workflow2 = serializer.LoadFromFile(parallelPath);

        // Fix assembly paths to be absolute
        foreach (var node in workflow2.Nodes)
        {
            if (!string.IsNullOrEmpty(node.AssemblyPath))
            {
                node.AssemblyPath = assemblyPath;
            }
        }

        await RunWorkflowWithProgressAsync(workflow2);

        Console.WriteLine("\n=== All Examples Completed ===");
    }

    private static async Task RunWorkflowWithProgressAsync(WorkflowDefinition workflow)
    {
        var progressManager = new ProgressTreeManager();

        await progressManager.RunAsync($"Workflow Execution", ExecutionMode.Sequential, async (root) =>
        {
            // Create event log to collect messages during execution
            var eventLog = new List<string>();

            // Create a child node for the workflow itself
            var workflowNode = root.AddChild(
                workflow.WorkflowId,
                $"Workflow: {workflow.WorkflowId}",
                TaskType.Job,
                ExecutionMode.Sequential,
                weight: 1.0);

            // Create a progress node for each workflow node as grandchildren
            var nodeProgressMap = new Dictionary<string, IProgressNode>();

            foreach (var nodeDef in workflow.Nodes)
            {
                var progressNode = workflowNode.AddChild(
                    nodeDef.NodeId,
                    nodeDef.NodeId,
                    TaskType.Stage,
                    ExecutionMode.Sequential,
                    maxValue: 100,
                    weight: 1.0);

                nodeProgressMap[nodeDef.NodeId] = progressNode;
            }

            // Create workflow engine and hook up events
            var engine = new WorkflowEngine();

            engine.NodeStarted += (nodeId, instanceId) =>
            {
                if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                {
                    progressNode.Description = $"[yellow]{nodeId}[/]: [grey]Running...[/]";
                    progressNode.ReportProgress(10);
                    eventLog.Add($"[{workflow.WorkflowId}] Node '{nodeId}' started");
                }
            };

            engine.NodeCompleted += (nodeId, instanceId, duration) =>
            {
                if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                {
                    progressNode.Description = $"[green]✓ {nodeId}[/]: [grey]Completed in {duration.TotalSeconds:F1}s[/]";
                    progressNode.ReportProgress(100);
                    eventLog.Add($"[{workflow.WorkflowId}] Node '{nodeId}' completed in {duration.TotalSeconds:F1}s");
                }
            };

            engine.NodeFailed += (nodeId, instanceId, error) =>
            {
                if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                {
                    progressNode.Description = $"[red]✗ {nodeId}[/]: [grey]{error}[/]";
                    progressNode.ReportProgress(100);
                    eventLog.Add($"[{workflow.WorkflowId}] Node '{nodeId}' failed: {error}");
                }
            };

            engine.NodeCancelled += (nodeId, instanceId, reason) =>
            {
                if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                {
                    progressNode.Description = $"[grey]⊘ {nodeId}[/]: [grey]{reason}[/]";
                    progressNode.ReportProgress(100);
                    eventLog.Add($"[{workflow.WorkflowId}] Node '{nodeId}' cancelled: {reason}");
                }
            };

            // Start workflow execution
            Console.WriteLine($"Starting workflow engine...");
            WorkflowExecutionContext result;
            try
            {
                result = await engine.StartAsync(workflow);
                Console.WriteLine($"Engine completed with status: {result.Status}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during execution: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

            // Ensure all nodes show 100% completion in the progress tree
            foreach (var (nodeId, progressNode) in nodeProgressMap)
            {
                progressNode.ReportProgress(100);
            }

            // Wait for progress display to update and complete
            await Task.Delay(1000);

            // Print event log after progress tree completes
            Console.WriteLine($"\n=== Event Log ===");
            foreach (var logEntry in eventLog)
            {
                Console.WriteLine($"  • {logEntry}");
            }

            // Print summary
            var completedCount = eventLog.Count(e => e.Contains("completed"));
            var failedCount = eventLog.Count(e => e.Contains("failed"));
            var totalNodes = workflow.Nodes.Count;
            var completionPercentage = totalNodes > 0 ? (completedCount * 100.0 / totalNodes) : 0;

            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Workflow: {workflow.WorkflowName}");
            Console.WriteLine($"Status: {result.Status}");
            Console.WriteLine($"Duration: {result.Duration?.TotalSeconds:F2}s");
            Console.WriteLine($"Nodes: {completedCount + failedCount}/{totalNodes} executed ({completionPercentage:F0}% complete)");
            if (failedCount > 0)
            {
                Console.WriteLine($"Failed: {failedCount} node(s)");
            }
        });
    }
}
