using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;

namespace ExecutionEngine.Example.Workflows;

public static class ParallelWorkflow
{
    public static WorkflowDefinition Create()
    {
        var assemblyPath = typeof(ParallelWorkflow).Assembly.Location;

        return new WorkflowDefinition
        {
            WorkflowId = "parallel-workflow",
            WorkflowName = "Simple Parallel Processing",
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
                        ["message"] = "Starting parallel tasks"
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
                    NodeId = "finish",
                    NodeName = "Finish",
                    Type = "Task",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Parallel tasks completed"
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                // Fan-out from start
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process1" },
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process2" },

                // Both converge to finish
                new NodeConnection { SourceNodeId = "process1", TargetNodeId = "finish" },
                new NodeConnection { SourceNodeId = "process2", TargetNodeId = "finish" }
            }
        };
    }
}
