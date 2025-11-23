// -----------------------------------------------------------------------
// <copyright file="AzureStackHealthCheckNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;

/// <summary>
/// Azure Stack health check node.
/// Performs post-deployment health checks for various Azure Stack services.
/// </summary>
public class AzureStackHealthCheckNode : ExecutableNodeBase
{
    private string? serviceName;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        this.Definition = definition;

        if (definition.Configuration != null &&
            definition.Configuration.TryGetValue("serviceName", out var serviceNameObj))
        {
            this.serviceName = serviceNameObj?.ToString();
        }
    }

    /// <inheritdoc/>
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

            // Perform health check
            Console.WriteLine($"[Health-Check] Checking {this.serviceName} service health...");
            await Task.Delay(400, cancellationToken);
            Console.WriteLine($"[Health-Check] ✓ {this.serviceName} service is healthy");

            nodeContext.OutputData[$"{this.serviceName?.ToLowerInvariant()}Status"] = "Healthy";

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Health check was cancelled";
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
