using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;

namespace ExecutionEngine.Example.Workflows;

public static class SimpleSequentialWorkflow
{
    public static WorkflowDefinition Create()
    {
        var assemblyPath = typeof(SimpleSequentialWorkflow).Assembly.Location;

        return new WorkflowDefinition
        {
            WorkflowId = "simple-sequential",
            WorkflowName = "Simple Sequential Workflow",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "start",
                    NodeName = "Start Logger",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Workflow started"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "process",
                    NodeName = "Data Processor",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "sample_data"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "finish",
                    NodeName = "Finish Logger",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Workflow completed"
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process" },
                new NodeConnection { SourceNodeId = "process", TargetNodeId = "finish" }
            }
        };
    }
}
