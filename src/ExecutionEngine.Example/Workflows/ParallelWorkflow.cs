using ExecutionEngine.Enums;
using ExecutionEngine.Workflow;

namespace ExecutionEngine.Example.Workflows;

using ExecutionEngine.Nodes.Definitions;

public static class ParallelWorkflow
{
    public static WorkflowDefinition Create()
    {
        var assemblyPath = typeof(ParallelWorkflow).Assembly.Location;

        return new WorkflowDefinition
        {
            WorkflowId = "data-analytics-pipeline",
            WorkflowName = "Parallel Data Analytics Pipeline",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "fetch-data",
                    NodeName = "Fetch Raw Data",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Fetching raw data from multiple sources"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "analyze-sales",
                    NodeName = "Analyze Sales Data",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "sales_metrics"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "analyze-inventory",
                    NodeName = "Analyze Inventory Levels",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "inventory_status"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "generate-report",
                    NodeName = "Generate Dashboard Report",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Generating executive dashboard with analytics"
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                // Fan-out: Fetch data, then analyze both sales and inventory in parallel
                new NodeConnection { SourceNodeId = "fetch-data", TargetNodeId = "analyze-sales" },
                new NodeConnection { SourceNodeId = "fetch-data", TargetNodeId = "analyze-inventory" },

                // Fan-in: Both analyses must complete before generating report
                new NodeConnection { SourceNodeId = "analyze-sales", TargetNodeId = "generate-report" },
                new NodeConnection { SourceNodeId = "analyze-inventory", TargetNodeId = "generate-report" }
            }
        };
    }
}
