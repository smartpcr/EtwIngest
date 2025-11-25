namespace ExecutionEngine.Example;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using ProgressTree;

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
            for (var i = 0; i < workflows.Count; i++)
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

            if (!int.TryParse(input, out var selection) || selection < 1 || selection > workflows.Count)
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

            WorkflowDefinition? foundWorkflow = null;
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

        // Display workflow info
        Console.WriteLine($"Workflow: {workflowToRun.WorkflowName}");
        Console.WriteLine($"Nodes: {workflowToRun.Nodes.Count}, Connections: {workflowToRun.Connections.Count}");
        Console.WriteLine($"Entry nodes: {string.Join(", ", workflowToRun.Nodes.Select(n => n.NodeId).Except(workflowToRun.Connections.Select(c => c.TargetNodeId)))}");
        Console.WriteLine();

        // Run the selected workflow
        await RunWorkflowWithProgressAsync(workflowToRun);

        Console.WriteLine("\n=== Execution Completed ===");
    }

    private static void CreateProgressNodeRecursive(
        NodeDefinition nodeDef,
        IProgressNode parentNode,
        Dictionary<string, IProgressNode> nodeProgressMap)
    {
        // Determine execution mode for the progress node
        var executionMode = ExecutionMode.Sequential;
        var childNodes = new List<NodeDefinition>();
        if (nodeDef is ContainerNodeDefinition containerNodeDefinition)
        {
            executionMode = containerNodeDefinition.ExecutionMode;
            childNodes = containerNodeDefinition.ChildNodes ?? new List<NodeDefinition>();
        }

        // Create progress node for this node
        var progressNode = parentNode.AddChild(
            nodeDef.NodeId,
            nodeDef.NodeName,
            executionMode == ExecutionMode.Parallel);

        nodeProgressMap[nodeDef.NodeId] = progressNode;

        // Check if this is a ContainerNode with ChildNodes
        if (childNodes.Any())
        {
            foreach (var childNode in childNodes)
            {
                CreateProgressNodeRecursive(childNode, progressNode, nodeProgressMap);
            }
        }
        else if (nodeDef is SubflowNodeDefinition subflowNodeDefinition)
        {
            var workflowPath = subflowNodeDefinition.WorkflowFilePath;
            // Try to load the child workflow
            var serializer = new WorkflowSerializer();
            var childWorkflow = serializer.LoadFromFile(workflowPath);

            // Recursively create progress nodes for child workflow nodes
            // Use hierarchical keys to avoid collisions when multiple subflows use the same workflow
            foreach (var childNode in childWorkflow.Nodes)
            {
                // Create the progress node under the subflow parent
                var childProgressNode = progressNode.AddChild(
                    childNode.NodeId,
                    childNode.NodeName,
                    false);

                // Use hierarchical key: "parentNodeId/childNodeId"
                var hierarchicalKey = $"{nodeDef.NodeId}/{childNode.NodeId}";
                nodeProgressMap[hierarchicalKey] = childProgressNode;
            }
        }
    }

    private static async Task RunWorkflowWithProgressAsync(WorkflowDefinition workflow)
    {
        var progressManager = new ProgressTreeMonitor();

        await progressManager.StartAsync(workflow.WorkflowName, (root) =>
        {
            root.AddChild(
                workflow.WorkflowId,
                workflow.WorkflowName,
                false,
                async (workflowNode, cancel) =>
                {
                    var nodeProgressMap = new Dictionary<string, IProgressNode>();
                    foreach (var node in workflow.Nodes)
                    {
                        CreateProgressNodeRecursive(node, workflowNode, nodeProgressMap);
                    }

                    // Create workflow engine and hook up events
                    var engine = new WorkflowEngine();
                    engine.NodeStarted += (nodeId, instanceId) =>
                    {
                        if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                        {
                            progressNode.Start();
                        }
                    };

                    engine.NodeCompleted += (nodeId, instanceId, duration) =>
                    {
                        if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                        {
                            progressNode.Complete();
                        }
                    };

                    engine.NodeFailed += (nodeId, instanceId, error) =>
                    {
                        if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                        {
                            progressNode.Fail(new InvalidOperationException($"Workflow '{workflow.WorkflowId}' Node '{nodeId}' Failed with error {error}"));
                        }
                    };

                    engine.NodeCancelled += (nodeId, instanceId, reason) =>
                    {
                        if (nodeProgressMap.TryGetValue(nodeId, out var progressNode))
                        {
                            progressNode.Cancel();
                        }
                    };

                    await engine.StartAsync(workflow, TimeSpan.FromSeconds(30), cancel);
                });
        });
    }
}
