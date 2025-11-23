using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Nodes.Definitions;

/// <summary>
/// Node for simulating Azure Stack deployment to a specific node.
/// </summary>
public class AzureStackDeployNode : ExecutableNodeBase
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

            var nodeName = this.Definition?.Configuration?.GetValueOrDefault("nodeName")?.ToString() ?? "Unknown-Node";

            // Simulate the deployment stages
            var stages = new[]
            {
                ("Connecting to node", 10),
                ("Copying binaries", 20),
                ("Installing packages", 30),
                ("Configuring services", 20),
                ("Starting services", 15),
                ("Verifying deployment", 5)
            };

            foreach (var (stepName, stepWeight) in stages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 0; i < stepWeight; i++)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            nodeContext.OutputData["nodeName"] = nodeName;
            nodeContext.OutputData["deployed"] = true;

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
