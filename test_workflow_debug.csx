using ExecutionEngine.Engine;
using ExecutionEngine.Workflow;
using ExecutionEngine.Factory;
using ExecutionEngine.Enums;

// Simple sequential workflow
var workflow = new WorkflowDefinition
{
    WorkflowId = "debug-test",
    WorkflowName = "Debug Test",
    Nodes = new List<NodeDefinition>
    {
        new NodeDefinition
        {
            NodeId = "node1",
            NodeName = "Node 1",
            Type = "Task",
            RuntimeType = RuntimeType.CSharp,
            AssemblyPath = typeof(ExecutionEngine.Example.Nodes.LogNode).Assembly.Location,
            TypeName = "ExecutionEngine.Example.Nodes.LogNode",
            Configuration = new Dictionary<string, object>
            {
                ["message"] = "Node 1"
            }
        },
        new NodeDefinition
        {
            NodeId = "node2",
            NodeName = "Node 2",
            Type = "Task",
            RuntimeType = RuntimeType.CSharp,
            AssemblyPath = typeof(ExecutionEngine.Example.Nodes.LogNode).Assembly.Location,
            TypeName = "ExecutionEngine.Example.Nodes.LogNode",
            Configuration = new Dictionary<string, object>
            {
                ["message"] = "Node 2"
            }
        }
    },
    Connections = new List<NodeConnection>
    {
        new NodeConnection { SourceNodeId = "node1", TargetNodeId = "node2" }
    }
};

var engine = new WorkflowEngine();

engine.NodeStarted += (nodeId, instanceId) =>
{
    Console.WriteLine($"[STARTED] {nodeId}");
};

engine.NodeCompleted += (nodeId, instanceId, duration) =>
{
    Console.WriteLine($"[COMPLETED] {nodeId} in {duration.TotalSeconds:F2}s");
};

var result = await engine.StartAsync(workflow);

Console.WriteLine($"\nWorkflow Status: {result.Status}");
Console.WriteLine($"Duration: {result.Duration?.TotalSeconds:F2}s");
