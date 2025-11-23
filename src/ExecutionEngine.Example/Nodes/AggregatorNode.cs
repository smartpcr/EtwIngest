using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Nodes.Definitions;

public class AggregatorNode : ExecutableNodeBase
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

            // Aggregating results from workflow context

            // Collect all processed results from global context
            var results = new List<string>();
            foreach (var kvp in workflowContext.Variables)
            {
                if (kvp.Key.EndsWith("_result"))
                {
                    results.Add(kvp.Value?.ToString() ?? "");
                }
            }

            await Task.Delay(500, cancellationToken);

            var aggregated = string.Join(", ", results);
            // Aggregated result stored in output context
            nodeContext.OutputData["aggregated"] = aggregated;
            workflowContext.Variables["aggregate_result"] = aggregated;

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
