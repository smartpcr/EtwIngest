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
    public class WorkflowEngine : IWorkflowEngine
    {
        private readonly NodeFactory nodeFactory;
        private readonly ConcurrentDictionary<string, INode> nodeInstances;
        private readonly ConcurrentDictionary<string, NodeInstance> nodeExecutionTracking;
        private readonly ConcurrentBag<Task> nodeExecutionTasks;
        private readonly ConcurrentDictionary<Guid, WorkflowExecutionContext> activeWorkflows;
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> workflowCancellationSources;

        // Track upstream completion for ALL join logic
        // Key: NodeId, Value: Set of upstream NodeIds that have completed
        private ConcurrentDictionary<string, HashSet<string>> upstreamCompletionTracking;

        // Track expected upstream count for ALL join nodes
        // Key: NodeId, Value: Expected number of upstream completions
        private ConcurrentDictionary<string, int> expectedUpstreamCount;

        /// <summary>
        /// Initializes a new instance of the WorkflowEngine class.
        /// </summary>
        public WorkflowEngine()
        {
            this.nodeFactory = new NodeFactory();
            this.nodeInstances = new ConcurrentDictionary<string, INode>();
            this.nodeExecutionTracking = new ConcurrentDictionary<string, NodeInstance>();
            this.nodeExecutionTasks = new ConcurrentBag<Task>();
            this.activeWorkflows = new ConcurrentDictionary<Guid, WorkflowExecutionContext>();
            this.workflowCancellationSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();
            this.upstreamCompletionTracking = new ConcurrentDictionary<string, HashSet<string>>();
            this.expectedUpstreamCount = new ConcurrentDictionary<string, int>();
        }

        /// <summary>
        /// Event raised when a node starts execution.
        /// </summary>
        public event Action<string, Guid>? NodeStarted;

        /// <summary>
        /// Event raised when a node completes successfully.
        /// </summary>
        public event Action<string, Guid, TimeSpan>? NodeCompleted;

        /// <summary>
        /// Event raised when a node fails during execution.
        /// </summary>
        public event Action<string, Guid, string>? NodeFailed;

        /// <summary>
        /// Event raised when a node is cancelled.
        /// </summary>
        public event Action<string, Guid, string>? NodeCancelled;

        /// <summary>
        /// Starts a new workflow execution with optional timeout.
        /// </summary>
        /// <param name="workflowDefinition">The workflow definition to execute.</param>
        /// <param name="timeout">Optional timeout for the entire workflow execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        public async Task<WorkflowExecutionContext> StartAsync(
            WorkflowDefinition workflowDefinition,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (workflowDefinition == null)
            {
                throw new ArgumentNullException(nameof(workflowDefinition));
            }

            // Create a linked cancellation token source for this workflow
            var workflowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Guid workflowInstanceId = Guid.Empty;

            try
            {
                // Apply timeout if specified
                if (timeout.HasValue)
                {
                    workflowCts.CancelAfter(timeout.Value);
                }

                // Execute the workflow
                var context = await this.ExecuteAsync(workflowDefinition, workflowCts.Token);
                workflowInstanceId = context.InstanceId;

                // Track active workflow (InstanceId is auto-generated in WorkflowExecutionContext constructor)
                this.activeWorkflows[context.InstanceId] = context;
                this.workflowCancellationSources[context.InstanceId] = workflowCts;

                return context;
            }
            catch (OperationCanceledException) when (workflowCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                var context = new WorkflowExecutionContext
                {
                    GraphId = workflowDefinition.WorkflowId,
                    Status = WorkflowExecutionStatus.Cancelled,
                    EndTime = DateTime.UtcNow
                };
                workflowInstanceId = context.InstanceId;
                context.Variables["__error"] = "Workflow execution timed out";
                this.activeWorkflows[context.InstanceId] = context;
                throw new TimeoutException($"Workflow execution timed out after {timeout}", new OperationCanceledException());
            }
            finally
            {
                // Cleanup - remove cancellation token source
                if (workflowInstanceId != Guid.Empty)
                {
                    this.workflowCancellationSources.TryRemove(workflowInstanceId, out _);
                }
            }
        }

        /// <summary>
        /// Resumes a paused workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to resume.</param>
        /// <param name="timeout">Optional timeout for the remaining workflow execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        /// <exception cref="NotImplementedException">This feature will be implemented in Phase 2.5.</exception>
        public Task<WorkflowExecutionContext> ResumeAsync(
            Guid workflowInstanceId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            // TODO: Implement in Phase 2.5 (State Persistence & Recovery)
            throw new NotImplementedException("ResumeAsync will be implemented in Phase 2.5");
        }

        /// <summary>
        /// Pauses a running workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to pause.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the pause operation.</returns>
        /// <exception cref="NotImplementedException">This feature will be implemented in Phase 2.5.</exception>
        public Task PauseAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement in Phase 2.5 (State Persistence & Recovery)
            throw new NotImplementedException("PauseAsync will be implemented in Phase 2.5");
        }

        /// <summary>
        /// Cancels a running or paused workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the cancel operation.</returns>
        public Task CancelAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
        {
            if (this.workflowCancellationSources.TryGetValue(workflowInstanceId, out var cts))
            {
                cts.Cancel();

                // Update workflow status
                if (this.activeWorkflows.TryGetValue(workflowInstanceId, out var context))
                {
                    context.Status = WorkflowExecutionStatus.Cancelled;
                    context.EndTime = DateTime.UtcNow;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current status of a workflow execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context, or null if not found.</returns>
        public Task<WorkflowExecutionContext?> GetWorkflowStatusAsync(
            Guid workflowInstanceId,
            CancellationToken cancellationToken = default)
        {
            this.activeWorkflows.TryGetValue(workflowInstanceId, out var context);
            return Task.FromResult<WorkflowExecutionContext?>(context);
        }

        // ==================== Private Methods ====================

        /// <summary>
        /// Executes a workflow definition.
        /// </summary>
        /// <param name="workflowDefinition">The workflow definition to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        private async Task<WorkflowExecutionContext> ExecuteAsync(
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
                // Step 1: Set up message queues for each node
                this.SetupMessageQueues(workflowDefinition, context);

                // Step 2: Set up message router
                this.SetupMessageRouter(workflowDefinition, context);

                // Step 3: Setup upstream tracking for ALL join nodes
                this.SetupUpstreamTracking(workflowDefinition);

                // Step 4: Find and trigger entry point nodes
                var entryNodes = this.FindEntryPointNodes(workflowDefinition);

                if (entryNodes.Count == 0)
                {
                    throw new InvalidOperationException("No entry point nodes found in workflow");
                }

                // Step 5: Trigger entry point nodes by enqueueing initial messages
                await this.TriggerEntryPointNodesAsync(entryNodes, context, cancellationToken);

                // Step 6: Centralized execution loop - checks all queues for messages
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool messageProcessed = false;

                    // Check all node queues for available messages
                    foreach (var nodeQueueEntry in context.NodeQueues)
                    {
                        var nodeId = nodeQueueEntry.Key;
                        var nodeQueue = (NodeMessageQueue)nodeQueueEntry.Value;
                        var nodeDef = workflowDefinition.Nodes.FirstOrDefault(n => n.NodeId == nodeId);

                        if (nodeDef == null) continue;

                        // Try to read a message from this queue's channel (non-blocking)
                        if (nodeQueue.MessageSignals.TryRead(out var signal))
                        {
                            messageProcessed = true;

                            // Handle the message based on type
                            await this.ProcessMessageAsync(nodeDef, signal, workflowDefinition, context, cancellationToken);
                        }
                    }

                    // Check if workflow is complete
                    // Complete when: all queues are empty AND all tracked nodes are terminal
                    bool allQueuesEmpty = context.NodeQueues.Values
                        .Cast<NodeMessageQueue>()
                        .All(q => q.Count == 0);

                    bool allTrackedNodesTerminal = this.nodeExecutionTracking.Count == 0 ||
                        this.nodeExecutionTracking.Values.All(nodeInstance =>
                            nodeInstance.Status == NodeExecutionStatus.Completed ||
                            nodeInstance.Status == NodeExecutionStatus.Failed ||
                            nodeInstance.Status == NodeExecutionStatus.Cancelled);

                    if (allQueuesEmpty && allTrackedNodesTerminal)
                    {
                        break;
                    }

                    // If no messages were processed, wait briefly to avoid busy-wait
                    if (!messageProcessed)
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }

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
        /// Gets or creates a node instance on-demand using the node factory.
        /// Nodes are only created when they are first executed, not pre-initialized.
        /// </summary>
        private async Task<INode> GetOrCreateNodeAsync(
            string nodeId,
            WorkflowDefinition workflowDefinition,
            CancellationToken cancellationToken)
        {
            // Check cache first
            if (this.nodeInstances.TryGetValue(nodeId, out var existingNode))
            {
                return existingNode;
            }

            // Find node definition
            var nodeDef = workflowDefinition.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (nodeDef == null)
            {
                throw new InvalidOperationException($"Node definition not found: {nodeId}");
            }

            // Create node using factory
            var node = this.nodeFactory.CreateNode(nodeDef);
            this.nodeInstances[nodeId] = node;

            await Task.CompletedTask;
            return node;
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
        /// Sets up upstream tracking for nodes with ALL join type.
        /// Tracks which upstream nodes have sent messages to determine when to execute.
        /// </summary>
        private void SetupUpstreamTracking(WorkflowDefinition workflowDefinition)
        {
            // Count upstream connections for each node
            var upstreamCounts = new Dictionary<string, int>();
            foreach (var connection in workflowDefinition.Connections)
            {
                if (connection.IsEnabled)
                {
                    if (!upstreamCounts.ContainsKey(connection.TargetNodeId))
                    {
                        upstreamCounts[connection.TargetNodeId] = 0;
                    }

                    upstreamCounts[connection.TargetNodeId]++;
                }
            }

            // Initialize tracking for ALL join nodes
            foreach (var nodeDef in workflowDefinition.Nodes)
            {
                if (nodeDef.JoinType == JoinType.All && upstreamCounts.ContainsKey(nodeDef.NodeId))
                {
                    this.expectedUpstreamCount[nodeDef.NodeId] = upstreamCounts[nodeDef.NodeId];
                    this.upstreamCompletionTracking[nodeDef.NodeId] = new HashSet<string>();
                }
            }
        }


        /// <summary>
        /// Cancels a node and propagates cancellation to all downstream nodes.
        /// Creates cancellation cascade to cleanly shut down dependent branches.
        /// Routes Cancel messages through both OnCancel and OnComplete connections.
        /// Note: Does not change the status of the source node - only propagates to downstream.
        /// </summary>
        private async Task CancelNodeAndPropagate(
            string nodeId,
            string reason,
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Do NOT change the status of the source node (it's already Failed)
            // We only propagate cancellation to downstream nodes
            if (this.nodeExecutionTracking.TryGetValue(nodeId, out var nodeInstance))
            {
                // Keep the original status (Failed) - don't overwrite it
                // Just ensure error message is set if not already
                if (string.IsNullOrEmpty(nodeInstance.ErrorMessage))
                {
                    nodeInstance.ErrorMessage = reason;
                }
            }

            // Create OnCancel message
            var cancelMsg = new NodeCancelMessage
            {
                NodeId = nodeId,
                Timestamp = DateTime.UtcNow,
                NodeInstanceId = nodeInstance?.NodeInstanceId ?? Guid.Empty,
                Reason = reason,
                CascadeFromFailure = true
            };

            // Find all downstream connections with TriggerMessageType = Cancel OR Complete
            // Cancel messages should propagate through both types of connections to ensure
            // cancellation cascade works even when explicit Cancel connections aren't defined
            var downstreamConnections = workflowDefinition.Connections
                .Where(c => c.SourceNodeId == nodeId &&
                           c.IsEnabled &&
                           (c.TriggerMessageType == MessageType.Cancel ||
                            c.TriggerMessageType == MessageType.Complete))
                .ToList();

            // Manually enqueue Cancel message to all eligible downstream nodes
            foreach (var connection in downstreamConnections)
            {
                if (context.NodeQueues.TryGetValue(connection.TargetNodeId, out var targetQueue))
                {
                    var queue = (NodeMessageQueue)targetQueue;
                    await queue.EnqueueAsync(cancelMsg, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Processes a single message and handles execution logic based on message type.
        /// Replaces HandleMessageAsync with cancellation support.
        /// </summary>
        private async Task ProcessMessageAsync(
            NodeDefinition nodeDef,
            INodeMessage signal,
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                // Handle Cancel message - mark this node as cancelled and propagate further
                if (signal is NodeCancelMessage cancelMsg)
                {
                    // Mark THIS node (the recipient) as cancelled, but only if not already failed/completed
                    if (this.nodeExecutionTracking.TryGetValue(nodeDef.NodeId, out var nodeInstance))
                    {
                        // Only mark as cancelled if node hasn't executed yet (is still Pending)
                        if (nodeInstance.Status == NodeExecutionStatus.Pending ||
                            nodeInstance.Status == NodeExecutionStatus.Running)
                        {
                            nodeInstance.Status = NodeExecutionStatus.Cancelled;
                            nodeInstance.ErrorMessage = cancelMsg.Reason ?? "Cancelled by upstream";

                            // Raise NodeCancelled event
                            this.NodeCancelled?.Invoke(nodeDef.NodeId, nodeInstance.NodeInstanceId, cancelMsg.Reason ?? "Cancelled by upstream");
                        }
                    }

                    // Propagate cancellation to downstream nodes (don't overwrite their status)
                    var router = (MessageRouter)context.Router!;
                    var downstreamCancelMsg = new NodeCancelMessage
                    {
                        NodeId = nodeDef.NodeId,
                        Timestamp = DateTime.UtcNow,
                        NodeInstanceId = nodeInstance?.NodeInstanceId ?? Guid.Empty,
                        Reason = cancelMsg.Reason ?? "Cancelled by upstream",
                        CascadeFromFailure = cancelMsg.CascadeFromFailure
                    };
                    await router!.RouteMessageAsync(downstreamCancelMsg, context, cancellationToken);
                    return;
                }

                // Check join type for other messages
                if (nodeDef.JoinType == JoinType.Any)
                {
                    // ANY: Execute immediately when any message arrives
                    await this.ExecuteNodeAsync(nodeDef.NodeId, workflowDefinition, context, cancellationToken);
                }
                else // JoinType.All
                {
                    // ALL: Check if all upstreams have sent a message
                    string? sourceNodeId = null;

                    if (signal is NodeCompleteMessage completeMsg)
                        sourceNodeId = completeMsg.NodeId;
                    else if (signal is NodeFailMessage failMsg)
                        sourceNodeId = failMsg.NodeId;
                    else if (signal is NodeNextMessage nextMsg)
                        sourceNodeId = nextMsg.NodeId;

                    if (!string.IsNullOrEmpty(sourceNodeId))
                    {
                        bool shouldExecute = false;

                        if (this.upstreamCompletionTracking.TryGetValue(nodeDef.NodeId, out var completedUpstreams))
                        {
                            lock (completedUpstreams)
                            {
                                completedUpstreams.Add(sourceNodeId);

                                if (this.expectedUpstreamCount.TryGetValue(nodeDef.NodeId, out var expected) &&
                                    completedUpstreams.Count >= expected)
                                {
                                    shouldExecute = true;
                                    completedUpstreams.Clear();
                                }
                            }
                        }

                        if (shouldExecute)
                        {
                            await this.ExecuteNodeAsync(nodeDef.NodeId, workflowDefinition, context, cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Only set error if node is tracked (it might not be if join logic prevented execution)
                if (this.nodeExecutionTracking.TryGetValue(nodeDef.NodeId, out var nodeInstance))
                {
                    nodeInstance.Status = NodeExecutionStatus.Failed;
                    nodeInstance.ErrorMessage = $"Message processing error: {ex.Message}";
                    nodeInstance.Exception = ex;
                }
            }
        }

        /// <summary>
        /// Executes a node once by leasing a message from its queue.
        /// After execution, routes completion/failure messages to downstream queues.
        /// Downstream workers are triggered automatically via channel signals.
        /// </summary>
        private async Task ExecuteNodeAsync(
            string nodeId,
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Get or create node instance tracking
            if (!this.nodeExecutionTracking.TryGetValue(nodeId, out var nodeInstance))
            {
                // First time executing this node - create tracking entry
                nodeInstance = new NodeInstance
                {
                    NodeInstanceId = Guid.NewGuid(),
                    NodeId = nodeId,
                    WorkflowInstanceId = context.InstanceId,
                    Status = NodeExecutionStatus.Pending,
                    StartTime = DateTime.UtcNow
                };
                this.nodeExecutionTracking[nodeId] = nodeInstance;
            }

            // If node is already running, skip (prevent duplicate execution)
            if (nodeInstance.Status == NodeExecutionStatus.Running)
            {
                return;
            }

            var queue = (NodeMessageQueue)context.NodeQueues[nodeId];
            var node = await this.GetOrCreateNodeAsync(nodeId, workflowDefinition, cancellationToken);
            var router = (MessageRouter)context.Router!;

            // Lease a message from the queue
            var lease = await queue.LeaseAsync(cancellationToken);

            if (lease == null)
            {
                // No message available - worker was woken but message already processed
                return;
            }

            // Execute the node
            try
            {
                nodeInstance.Status = NodeExecutionStatus.Running;
                nodeInstance.StartTime = DateTime.UtcNow;

                // Raise NodeStarted event
                this.NodeStarted?.Invoke(nodeId, nodeInstance.NodeInstanceId);

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

                // Route completion/failure messages to downstream queues
                // MessageRouter enqueues → triggers channel signals → downstream workers wake up
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

                    // Raise NodeCompleted event
                    var duration = nodeInstance.Duration ?? TimeSpan.Zero;
                    this.NodeCompleted?.Invoke(nodeId, nodeInstance.NodeInstanceId, duration);
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

                    // Raise NodeFailed event
                    this.NodeFailed?.Invoke(nodeId, nodeInstance.NodeInstanceId, result.ErrorMessage ?? "Node execution failed");

                    // Propagate cancellation to downstream nodes
                    await this.CancelNodeAndPropagate(nodeId, result.ErrorMessage ?? "Node execution failed", workflowDefinition, context, cancellationToken);
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

                // Raise NodeFailed event
                this.NodeFailed?.Invoke(nodeId, nodeInstance.NodeInstanceId, ex.Message);

                // Propagate cancellation to downstream nodes
                await this.CancelNodeAndPropagate(nodeId, ex.Message, workflowDefinition, context, cancellationToken);
            }
        }

        /// <summary>
        /// Triggers entry point nodes by enqueueing initial messages.
        /// Channel signals automatically wake up subscribed workers.
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

                // Enqueue triggers channel signal → worker wakes up and executes
                await queue.EnqueueAsync(triggerMessage, cancellationToken);
            }
        }

        /// <summary>
        /// Waits for all node execution tasks to complete.
        /// In message-driven architecture, new tasks are spawned dynamically as messages arrive,
        /// so we poll node statuses instead of waiting for a static list of tasks.
        /// </summary>
        private async Task WaitForCompletionAsync(
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromMilliseconds(100);

            while (DateTime.UtcNow - startTime < timeout)
            {
                // Check if all nodes have reached a terminal status
                var allNodesComplete = this.nodeExecutionTracking.Values.All(nodeInstance =>
                    nodeInstance.Status == NodeExecutionStatus.Completed ||
                    nodeInstance.Status == NodeExecutionStatus.Failed ||
                    nodeInstance.Status == NodeExecutionStatus.Cancelled);

                if (allNodesComplete)
                {
                    // All nodes have reached terminal status - return immediately
                    // Worker tasks continue running in background but are idle (waiting on channels)
                    // They will be cancelled when the workflow CancellationToken is triggered
                    return;
                }

                // Wait a bit before checking again
                await Task.Delay(pollInterval, cancellationToken);
            }

            // Timeout occurred
            var pendingNodes = this.nodeExecutionTracking
                .Where(kvp => kvp.Value.Status == NodeExecutionStatus.Pending ||
                              kvp.Value.Status == NodeExecutionStatus.Running)
                .Select(kvp => kvp.Key)
                .ToList();

            throw new TimeoutException($"Workflow execution timed out. Pending nodes: {string.Join(", ", pendingNodes)}");
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
