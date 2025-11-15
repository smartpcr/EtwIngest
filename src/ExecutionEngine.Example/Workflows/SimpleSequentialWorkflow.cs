using ExecutionEngine.Enums;
using ExecutionEngine.Workflow;

namespace ExecutionEngine.Example.Workflows;

using ExecutionEngine.Nodes.Definitions;

public static class SimpleSequentialWorkflow
{
    public static WorkflowDefinition Create()
    {
        var assemblyPath = typeof(SimpleSequentialWorkflow).Assembly.Location;

        return new WorkflowDefinition
        {
            WorkflowId = "customer-order-processing",
            WorkflowName = "Customer Order Processing Pipeline",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "validate-order",
                    NodeName = "Validate Customer Order",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Validating order details and customer information"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "process-payment",
                    NodeName = "Process Payment",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "payment_transaction"
                    }
                },
                new NodeDefinition
                {
                    NodeId = "send-confirmation",
                    NodeName = "Send Order Confirmation",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Sending confirmation email to customer"
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "validate-order", TargetNodeId = "process-payment" },
                new NodeConnection { SourceNodeId = "process-payment", TargetNodeId = "send-confirmation" }
            }
        };
    }
}
