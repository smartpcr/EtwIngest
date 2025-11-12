using ExecutionEngine.Engine;
using ExecutionEngine.Example.Workflows;
using ExecutionEngine.Workflow;
using ProgressTree;

namespace ExecutionEngine.Example;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== ExecutionEngine Examples with ProgressTree ===\n");

        // Example 1: Simple Sequential Workflow with Progress Tracking
        Console.WriteLine("Example 1: Simple Sequential Workflow");
        Console.WriteLine("-------------------------------------");
        var workflow1 = SimpleSequentialWorkflow.Create();
        await RunWorkflowWithProgressAsync(workflow1);

        // Example 2: Parallel Processing with Progress Tracking
        Console.WriteLine("\nExample 2: Parallel Processing");
        Console.WriteLine("-------------------------------");
        var workflow2 = ParallelWorkflow.Create();
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
            var result = await engine.StartAsync(workflow);

            // Wait for progress display to complete
            await Task.Delay(500);

            // Print event log after progress tree completes
            Console.WriteLine($"\n=== Event Log ===");
            foreach (var logEntry in eventLog)
            {
                Console.WriteLine($"  • {logEntry}");
            }

            // Print summary
            Console.WriteLine($"\n=== Summary ===");
            Console.WriteLine($"Workflow ID: {workflow.WorkflowId}");
            Console.WriteLine($"Status: {result.Status}");
            Console.WriteLine($"Duration: {result.Duration?.TotalSeconds:F2}s");
            Console.WriteLine($"Nodes Executed: {eventLog.Count(e => e.Contains("completed")) + eventLog.Count(e => e.Contains("failed"))}");
        });
    }
}
