using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Nodes.Definitions;

public class DataProcessorNode : ExecutableNodeBase
{
    public override void Initialize(NodeDefinition definition)
    {
        this.Definition = definition;
    }

    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        var instance = new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = this.NodeId,
            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Running,
            StartTime = DateTime.UtcNow,
            ExecutionContext = nodeContext
        };

        try
        {
            this.RaiseOnStart(new NodeStartEventArgs
            {
                NodeId = this.NodeId,
                NodeInstanceId = instance.NodeInstanceId,
                Timestamp = DateTime.UtcNow
            });

            var input = this.Definition?.Configuration?.GetValueOrDefault("data")?.ToString() ?? "default";
            // Processing data, output stored in context

            await Task.Delay(1000, cancellationToken); // Simulate processing

            var result = $"{input}_processed";
            nodeContext.OutputData["result"] = result;
            workflowContext.Variables[$"{this.NodeId}_result"] = result;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
        }

        return instance;
    }
}
