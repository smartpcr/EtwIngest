namespace ExecutionEngine.Example.Workflows;

using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;


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
                new CSharpNodeDefinition
                {
                    NodeId = "validate-order",
                    NodeName = "Validate Customer Order",
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.LogNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["message"] = "Validating order details and customer information"
                    }
                },
                new CSharpNodeDefinition
                {
                    NodeId = "process-payment",
                    NodeName = "Process Payment",
                    AssemblyPath = assemblyPath,
                    TypeName = "ExecutionEngine.Example.Nodes.DataProcessorNode",
                    Configuration = new Dictionary<string, object>
                    {
                        ["data"] = "payment_transaction"
                    }
                },
                new CSharpNodeDefinition
                {
                    NodeId = "send-confirmation",
                    NodeName = "Send Order Confirmation",
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
