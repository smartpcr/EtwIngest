using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Engine;
using ExecutionEngine.Factory;
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

        // Scan for available workflows - try multiple possible locations
        var workflowsDir = "Workflows";
        if (!Directory.Exists(workflowsDir))
        {
            workflowsDir = "ExecutionEngine.Example/Workflows";
        }
        if (!Directory.Exists(workflowsDir))
        {
            workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
        }

        if (!Directory.Exists(workflowsDir))
        {
            Console.WriteLine($"Workflows directory not found. Tried:");
            Console.WriteLine($"  - Workflows");
            Console.WriteLine($"  - ExecutionEngine.Example/Workflows");
            Console.WriteLine($"  - {Path.Combine(AppContext.BaseDirectory, "Workflows")}");
            return;
        }

        var workflowFiles = Directory.GetFiles(workflowsDir, "*.yaml");

        if (workflowFiles.Length == 0)
        {
            Console.WriteLine($"No workflow files found in {workflowsDir}");
            return;
        }

        WorkflowDefinition workflowToRun;

        if (args.Length == 0)
        {
            // Interactive mode: list workflows and ask user to select
            Console.WriteLine("Available Workflows:");
            Console.WriteLine("-------------------");

            var workflows = new List<(string FilePath, WorkflowDefinition Workflow)>();
            var errors = new List<string>();

            foreach (var file in workflowFiles)
            {
                try
                {
                    var workflow = serializer.LoadFromFile(file);
                    workflows.Add((file, workflow));
                }
                catch (Exception ex)
                {
                    errors.Add($"[ERROR] {Path.GetFileName(file)} - Failed to load: {ex.Message}");
                }
            }

            // Display valid workflows
            for (int i = 0; i < workflows.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {workflows[i].Workflow.WorkflowId} - {workflows[i].Workflow.WorkflowName}");
                Console.WriteLine($"   File: {Path.GetFileName(workflows[i].FilePath)}");
            }

            // Display errors at the end
            if (errors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Failed to load:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"   {error}");
                }
            }

            if (workflows.Count == 0)
            {
                Console.WriteLine("\nNo valid workflows found. Exiting.");
                return;
            }

            Console.WriteLine();
            Console.Write("Select a workflow to run (enter number): ");
            var input = Console.ReadLine();

            if (!int.TryParse(input, out int selection) || selection < 1 || selection > workflows.Count)
            {
                Console.WriteLine("Invalid selection. Exiting.");
                return;
            }

            workflowToRun = workflows[selection - 1].Workflow;
            Console.WriteLine($"\nSelected: {workflowToRun.WorkflowId} - {workflowToRun.WorkflowName}\n");
        }
        else
        {
            // Load workflow by WorkflowId from command line
            var requestedWorkflowId = args[0];
            Console.WriteLine($"Searching for workflow with ID: {requestedWorkflowId}");

            WorkflowDefinition foundWorkflow = null;
            foreach (var file in workflowFiles)
            {
                try
                {
                    var workflow = serializer.LoadFromFile(file);
                    if (workflow.WorkflowId.Equals(requestedWorkflowId, StringComparison.OrdinalIgnoreCase))
                    {
                        foundWorkflow = workflow;
                        Console.WriteLine($"Found workflow in: {Path.GetFileName(file)}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            if (foundWorkflow == null)
            {
                Console.WriteLine($"Workflow '{requestedWorkflowId}' not found in {workflowsDir}");
                return;
            }

            workflowToRun = foundWorkflow;
            Console.WriteLine($"Loaded: {workflowToRun.WorkflowId} - {workflowToRun.WorkflowName}\n");
        }

        // Fix assembly paths to be absolute
        FixAssemblyPaths(workflowToRun);

        // Display workflow info
        Console.WriteLine($"Workflow: {workflowToRun.WorkflowName}");
        Console.WriteLine($"Nodes: {workflowToRun.Nodes.Count}, Connections: {workflowToRun.Connections.Count}");
        Console.WriteLine($"Entry nodes: {string.Join(", ", workflowToRun.Nodes.Select(n => n.NodeId).Except(workflowToRun.Connections.Select(c => c.TargetNodeId)))}");
        Console.WriteLine();

        // Run the selected workflow
        await RunWorkflowWithProgressAsync(workflowToRun);

        Console.WriteLine("\n=== Execution Completed ===");
    }

    private static void FixAssemblyPaths(WorkflowDefinition workflow)
    {
        var assemblyPath = typeof(Program).Assembly.Location;
        foreach (var node in workflow.Nodes)
        {
            FixNodeAssemblyPath(node, assemblyPath);
        }
    }

    private static void FixNodeAssemblyPath(NodeDefinition node, string assemblyPath)
    {
        // Fix assembly path for this node
        if (!string.IsNullOrEmpty(node.AssemblyPath))
        {
            node.AssemblyPath = assemblyPath;
        }

        // Recursively fix child nodes if this is a container
        if (node.Configuration != null && node.Configuration.TryGetValue("ChildNodes", out var childNodesObj))
        {
            if (childNodesObj is List<NodeDefinition> childNodes)
            {
                foreach (var childNode in childNodes)
                {
                    FixNodeAssemblyPath(childNode, assemblyPath);
                }
            }
        }
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
