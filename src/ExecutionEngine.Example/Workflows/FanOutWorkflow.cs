using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;

namespace ExecutionEngine.Example.Workflows;

public static class FanOutWorkflow
{
    public static WorkflowDefinition Create()
    {
        var assemblyPath = typeof(FanOutWorkflow).Assembly.Location;

        return new WorkflowDefinition
        {
            WorkflowId = "fanout-workflow",
            WorkflowName = "Fan-Out Parallel Processing",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "start",
                    NodeName = "Start",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Starting parallel processing"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "process1",
                    NodeName = "Processor 1",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "dataset_1"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "process2",
                    NodeName = "Processor 2",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "dataset_2"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "process3",
                    NodeName = "Processor 3",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "dataset_3"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "aggregate",
                    NodeName = "Aggregator",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.AggregatorNode"
                    // NOTE: Temporarily removed JoinType.All due to possible engine bug
                    // JoinType = JoinType.All // Wait for all upstreams
                }
            },
            Connections = new List<NodeConnection>
            {
                // Fan-out from start
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process1" },
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process2" },
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process3" },

                // Fan-in to aggregate
                new NodeConnection { SourceNodeId = "process1", TargetNodeId = "aggregate" },
                new NodeConnection { SourceNodeId = "process2", TargetNodeId = "aggregate" },
                new NodeConnection { SourceNodeId = "process3", TargetNodeId = "aggregate" }
            }
        };
    }
}
