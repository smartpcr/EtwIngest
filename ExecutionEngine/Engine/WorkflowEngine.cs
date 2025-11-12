// -----------------------------------------------------------------------
// <copyright file="WorkflowEngine.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Engine
{
    using System.Collections.Concurrent;
    using ExecutionEngine.Contexts;
    using ExecutionEngine.Core;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Factory;
    using ExecutionEngine.Messages;
    using ExecutionEngine.Queue;
    using ExecutionEngine.Routing;
    using ExecutionEngine.Workflow;

    /// <summary>
    /// Orchestrates workflow execution by managing node lifecycle, message routing, and execution flow.
    /// Implements message-driven execution where nodes are triggered by messages from upstream nodes.
    /// </summary>
    public class WorkflowEngine
    {
        private readonly NodeFactory nodeFactory;
        private readonly ConcurrentDictionary<string, INode> nodeInstances;
        private readonly ConcurrentDictionary<string, NodeInstance> nodeExecutionTracking;
        private readonly ConcurrentDictionary<string, Task> nodeExecutionTasks;

        /// <summary>
        /// Initializes a new instance of the WorkflowEngine class.
        /// </summary>
        public WorkflowEngine()
        {
            this.nodeFactory = new NodeFactory();
            this.nodeInstances = new ConcurrentDictionary<string, INode>();
            this.nodeExecutionTracking = new ConcurrentDictionary<string, NodeInstance>();
            this.nodeExecutionTasks = new ConcurrentDictionary<string, Task>();
        }

        /// <summary>
        /// Executes a workflow definition.
        /// </summary>
        /// <param name="workflowDefinition">The workflow definition to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        public async Task<WorkflowExecutionContext> ExecuteAsync(
            WorkflowDefinition workflowDefinition,
            CancellationToken cancellationToken = default)
        {
            if (workflowDefinition == null)
            {
                throw new ArgumentNullException(nameof(workflowDefinition));
            }

            // Create execution context
            var context = new WorkflowExecutionContext
            {
                GraphId = workflowDefinition.WorkflowId,
                Status = WorkflowExecutionStatus.Running
            };

            try
            {
                // Step 1: Initialize nodes using factory
                await this.InitializeNodesAsync(workflowDefinition, context, cancellationToken);

                // Step 2: Set up message queues for each node
                this.SetupMessageQueues(workflowDefinition, context);

                // Step 3: Set up message router
                this.SetupMessageRouter(workflowDefinition, context);

                // Step 4: Find and execute entry point nodes
                var entryNodes = this.FindEntryPointNodes(workflowDefinition);

                if (entryNodes.Count == 0)
                {
                    throw new InvalidOperationException("No entry point nodes found in workflow");
                }

                // Step 5: Start execution tasks for all nodes (they will wait for messages)
                this.StartNodeExecutionTasks(workflowDefinition, context, cancellationToken);

                // Step 6: Trigger entry point nodes by sending initial messages
                await this.TriggerEntryPointNodesAsync(entryNodes, context, cancellationToken);

                // Step 7: Wait for all nodes to complete
                await this.WaitForCompletionAsync(context, cancellationToken);

                // Step 8: Set final status
                context.Status = this.DetermineWorkflowStatus(context);
                context.EndTime = DateTime.UtcNow;

                return context;
            }
            catch (Exception ex)
            {
                context.Status = WorkflowExecutionStatus.Failed;
                context.EndTime = DateTime.UtcNow;
                context.Variables["__error"] = ex.Message;
                throw;
            }
        }

        /// <summary>
        /// Initializes all nodes in the workflow using the node factory.
        /// </summary>
        private async Task InitializeNodesAsync(
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            foreach (var nodeDef in workflowDefinition.Nodes)
            {
                var node = this.nodeFactory.CreateNode(nodeDef);
                this.nodeInstances[nodeDef.NodeId] = node;

                // Track node instance
                var nodeInstance = new NodeInstance
                {
                    NodeInstanceId = Guid.NewGuid(),
                    NodeId = nodeDef.NodeId,
                    WorkflowInstanceId = context.InstanceId,
                    Status = NodeExecutionStatus.Pending
                };
                this.nodeExecutionTracking[nodeDef.NodeId] = nodeInstance;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Sets up message queues for each node.
        /// </summary>
        private void SetupMessageQueues(WorkflowDefinition workflowDefinition, WorkflowExecutionContext context)
        {
            foreach (var nodeDef in workflowDefinition.Nodes)
            {
                var queue = new NodeMessageQueue(capacity: 1000);
                context.NodeQueues[nodeDef.NodeId] = queue;
            }
        }

        /// <summary>
        /// Sets up the message router with workflow connections.
        /// </summary>
        private void SetupMessageRouter(WorkflowDefinition workflowDefinition, WorkflowExecutionContext context)
        {
            var dlq = new DeadLetterQueue();
            var router = new MessageRouter(dlq);

            // Add routes based on connections
            foreach (var connection in workflowDefinition.Connections)
            {
                if (connection.IsEnabled)
                {
                    router.AddRoute(connection.SourceNodeId, connection.TargetNodeId);
                }
            }

            context.Router = router;
            context.DeadLetterQueue = dlq;
        }

        /// <summary>
        /// Finds entry point nodes (nodes with no incoming connections).
        /// </summary>
        private List<string> FindEntryPointNodes(WorkflowDefinition workflowDefinition)
        {
            // If explicit entry point is specified, use it
            if (!string.IsNullOrEmpty(workflowDefinition.EntryPointNodeId))
            {
                return new List<string> { workflowDefinition.EntryPointNodeId };
            }

            // Otherwise, find nodes with no incoming connections
            var allNodeIds = workflowDefinition.Nodes.Select(n => n.NodeId).ToHashSet();
            var nodesWithIncoming = workflowDefinition.Connections
                .Where(c => c.IsEnabled)
                .Select(c => c.TargetNodeId)
                .ToHashSet();

            return allNodeIds.Except(nodesWithIncoming).ToList();
        }

        /// <summary>
        /// Starts execution tasks for all nodes that will process messages from their queues.
        /// </summary>
        private void StartNodeExecutionTasks(
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            foreach (var nodeDef in workflowDefinition.Nodes)
            {
                var task = Task.Run(async () =>
                {
                    await this.NodeExecutionLoopAsync(nodeDef.NodeId, context, cancellationToken);
                }, cancellationToken);

                this.nodeExecutionTasks[nodeDef.NodeId] = task;
            }
        }

        /// <summary>
        /// Main execution loop for a node - waits for a single message and executes the node once.
        /// </summary>
        private async Task NodeExecutionLoopAsync(
            string nodeId,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            var queue = (NodeMessageQueue)context.NodeQueues[nodeId];
            var node = this.nodeInstances[nodeId];
            var nodeInstance = this.nodeExecutionTracking[nodeId];
            var router = (MessageRouter)context.Router!;

            // Wait for a message with timeout
            var timeout = TimeSpan.FromSeconds(10);
            var startWait = DateTime.UtcNow;

            MessageLease? lease = null;

            // Wait for a message
            while (lease == null && !cancellationToken.IsCancellationRequested)
            {
                // Check for timeout
                if (DateTime.UtcNow - startWait > timeout)
                {
                    // No message received within timeout, mark as cancelled
                    nodeInstance.Status = NodeExecutionStatus.Cancelled;
                    return;
                }

                lease = await queue.LeaseAsync(cancellationToken);
                if (lease == null)
                {
                    await Task.Delay(50, cancellationToken);
                }
            }

            if (lease == null || cancellationToken.IsCancellationRequested)
            {
                nodeInstance.Status = NodeExecutionStatus.Cancelled;
                return;
            }

            // Execute the node with global and local context
            try
            {
                nodeInstance.Status = NodeExecutionStatus.Running;
                nodeInstance.StartTime = DateTime.UtcNow;

                var nodeContext = new NodeExecutionContext();

                // Copy input from trigger message if available
                if (lease.Message is NodeCompleteMessage completeMsg && completeMsg.NodeContext != null)
                {
                    foreach (var kvp in completeMsg.NodeContext.OutputData)
                    {
                        nodeContext.InputData[kvp.Key] = kvp.Value;
                    }
                }

                nodeInstance.ExecutionContext = nodeContext;

                // Execute to finish or failure
                var result = await node.ExecuteAsync(context, nodeContext, cancellationToken);

                // Update node instance
                nodeInstance.Status = result.Status;
                nodeInstance.EndTime = result.EndTime;
                nodeInstance.ErrorMessage = result.ErrorMessage;
                nodeInstance.Exception = result.Exception;

                // Complete the lease
                await queue.CompleteAsync(lease, cancellationToken);

                // Route completion/failure messages to downstream nodes
                if (result.Status == NodeExecutionStatus.Completed)
                {
                    var completeMessage = new NodeCompleteMessage
                    {
                        NodeId = nodeId,
                        Timestamp = DateTime.UtcNow,
                        NodeInstanceId = nodeInstance.NodeInstanceId,
                        NodeContext = nodeContext
                    };
                    await router.RouteMessageAsync(completeMessage, context, cancellationToken);
                }
                else if (result.Status == NodeExecutionStatus.Failed)
                {
                    var failMessage = new NodeFailMessage
                    {
                        NodeId = nodeId,
                        Timestamp = DateTime.UtcNow,
                        ErrorMessage = result.ErrorMessage ?? "Node execution failed",
                        Exception = result.Exception
                    };
                    await router.RouteMessageAsync(failMessage, context, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Handle execution error
                nodeInstance.Status = NodeExecutionStatus.Failed;
                nodeInstance.EndTime = DateTime.UtcNow;
                nodeInstance.ErrorMessage = ex.Message;
                nodeInstance.Exception = ex;

                await queue.AbandonAsync(lease, cancellationToken);

                var failMessage = new NodeFailMessage
                {
                    NodeId = nodeId,
                    Timestamp = DateTime.UtcNow,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };
                await router.RouteMessageAsync(failMessage, context, cancellationToken);
            }
        }

        /// <summary>
        /// Triggers entry point nodes by sending initial messages to their queues.
        /// </summary>
        private async Task TriggerEntryPointNodesAsync(
            List<string> entryNodes,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            foreach (var nodeId in entryNodes)
            {
                var queue = (NodeMessageQueue)context.NodeQueues[nodeId];
                var triggerMessage = new NodeCompleteMessage
                {
                    NodeId = "__trigger__",
                    Timestamp = DateTime.UtcNow
                };
                await queue.EnqueueAsync(triggerMessage, cancellationToken);
            }
        }

        /// <summary>
        /// Waits for all node execution tasks to complete.
        /// </summary>
        private async Task WaitForCompletionAsync(
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Wait for all tasks with a reasonable timeout
            var timeout = TimeSpan.FromSeconds(30);

            try
            {
                await Task.WhenAll(this.nodeExecutionTasks.Values).WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Log which nodes didn't complete
                var pendingNodes = this.nodeExecutionTracking
                    .Where(kvp => kvp.Value.Status == NodeExecutionStatus.Pending ||
                                  kvp.Value.Status == NodeExecutionStatus.Running)
                    .Select(kvp => kvp.Key)
                    .ToList();

                throw new TimeoutException($"Workflow execution timed out. Pending nodes: {string.Join(", ", pendingNodes)}");
            }
        }

        /// <summary>
        /// Determines the final workflow status based on node statuses.
        /// </summary>
        private WorkflowExecutionStatus DetermineWorkflowStatus(WorkflowExecutionContext context)
        {
            var statuses = this.nodeExecutionTracking.Values.Select(n => n.Status).ToList();
            var anyFailed = statuses.Any(s => s == NodeExecutionStatus.Failed);
            var anyCancelled = statuses.Any(s => s == NodeExecutionStatus.Cancelled);
            var allCompletedOrCancelled = statuses.All(s =>
                s == NodeExecutionStatus.Completed || s == NodeExecutionStatus.Cancelled);

            if (anyFailed)
            {
                // Collect error messages from failed nodes for debugging
                var failedNodes = this.nodeExecutionTracking.Values
                    .Where(n => n.Status == NodeExecutionStatus.Failed)
                    .Select(n => $"{n.NodeId}: {n.ErrorMessage ?? "Unknown error"}")
                    .ToList();

                if (failedNodes.Any())
                {
                    context.Variables["__node_errors"] = string.Join("; ", failedNodes);
                }

                return WorkflowExecutionStatus.Failed;
            }

            if (allCompletedOrCancelled)
            {
                // If some were cancelled but none failed, still consider it completed
                // Cancelled nodes are those that weren't triggered (e.g., disabled connections)
                return WorkflowExecutionStatus.Completed;
            }

            return WorkflowExecutionStatus.Running;
        }
    }
}
