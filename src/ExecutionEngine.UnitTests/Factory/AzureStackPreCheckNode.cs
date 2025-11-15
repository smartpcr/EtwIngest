// -----------------------------------------------------------------------
// <copyright file="AzureStackPreCheckNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Factory
{
    using System;
    using System.Threading.Tasks;
    using ExecutionEngine.Contexts;
    using ExecutionEngine.Core;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Factory;
    using ExecutionEngine.Nodes;


    /// <summary>
    /// Azure Stack pre-deployment check node.
    /// Performs various pre-deployment validations (network, storage, prerequisites).
    /// </summary>
    public class AzureStackPreCheckNode : ExecutableNodeBase
    {
        private string? checkType;

        /// <inheritdoc/>
        public override void Initialize(NodeDefinition definition)
        {
            base.Initialize(definition);

            if (definition.Configuration != null &&
                definition.Configuration.TryGetValue("checkType", out var checkTypeObj))
            {
                this.checkType = checkTypeObj?.ToString();
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

                // Perform check based on type
                switch (this.checkType?.ToLowerInvariant())
                {
                    case "network":
                        Console.WriteLine("[Pre-Check] Checking network connectivity...");
                        await Task.Delay(500, cancellationToken);
                        Console.WriteLine("[Pre-Check] ✓ Network connectivity validated");
                        nodeContext.OutputData["networkStatus"] = "OK";
                        break;

                    case "storage":
                        Console.WriteLine("[Pre-Check] Validating storage configuration...");
                        await Task.Delay(500, cancellationToken);
                        Console.WriteLine("[Pre-Check] ✓ Storage configuration validated");
                        nodeContext.OutputData["storageStatus"] = "OK";
                        break;

                    case "prerequisites":
                        Console.WriteLine("[Pre-Check] Checking prerequisites...");
                        await Task.Delay(500, cancellationToken);
                        Console.WriteLine("[Pre-Check] ✓ All prerequisites met");
                        nodeContext.OutputData["prerequisitesStatus"] = "OK";
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown check type: {this.checkType}");
                }

                instance.Status = NodeExecutionStatus.Completed;
                instance.EndTime = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                instance.Status = NodeExecutionStatus.Cancelled;
                instance.EndTime = DateTime.UtcNow;
                instance.ErrorMessage = "Pre-check was cancelled";
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
}
