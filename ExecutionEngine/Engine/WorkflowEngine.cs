// -----------------------------------------------------------------------
// <copyright file="WorkflowEngine.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Engine
{
    using System.Collections.Concurrent;
    using System.Text.Json;
    using ExecutionEngine.Concurrency;
    using ExecutionEngine.Contexts;
    using ExecutionEngine.Core;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Events;
    using ExecutionEngine.Factory;
    using ExecutionEngine.Messages;
    using ExecutionEngine.Persistence;
    using ExecutionEngine.Policies;
    using ExecutionEngine.Queue;
    using ExecutionEngine.Resilience;
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
        private readonly ConcurrentDictionary<Guid, WorkflowDefinition> workflowDefinitions;
        private readonly ICheckpointStorage? checkpointStorage;
        private readonly ConcurrentDictionary<Guid, ConcurrencyLimiter> workflowConcurrencyLimiters;
        private readonly NodeThrottler nodeThrottler;
        private readonly CircuitBreakerManager circuitBreakerManager;

        // Track upstream completion for ALL join logic
        // Key: NodeId, Value: Set of upstream NodeIds that have completed
        private ConcurrentDictionary<string, HashSet<string>> upstreamCompletionTracking;

        // Track expected upstream count for ALL join nodes
        // Key: NodeId, Value: Expected number of upstream completions
        private ConcurrentDictionary<string, int> expectedUpstreamCount;

        // Track completed nodes for compensation (Saga pattern)
        // Key: WorkflowInstanceId, Value: List of (NodeId, CompensationNodeId) pairs
        private ConcurrentDictionary<Guid, List<(string NodeId, string? CompensationNodeId)>> completedNodesForCompensation;

        /// <summary>
        /// Initializes a new instance of the WorkflowEngine class.
        /// </summary>
        /// <param name="checkpointStorage">Optional checkpoint storage for persistence and recovery.</param>
        public WorkflowEngine(ICheckpointStorage? checkpointStorage = null)
        {
            this.nodeFactory = new NodeFactory();
            this.nodeInstances = new ConcurrentDictionary<string, INode>();
            this.nodeExecutionTracking = new ConcurrentDictionary<string, NodeInstance>();
            this.nodeExecutionTasks = new ConcurrentBag<Task>();
            this.activeWorkflows = new ConcurrentDictionary<Guid, WorkflowExecutionContext>();
            this.workflowCancellationSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();
            this.workflowDefinitions = new ConcurrentDictionary<Guid, WorkflowDefinition>();
            this.checkpointStorage = checkpointStorage;
            this.workflowConcurrencyLimiters = new ConcurrentDictionary<Guid, ConcurrencyLimiter>();
            this.nodeThrottler = new NodeThrottler();
            this.circuitBreakerManager = new CircuitBreakerManager();
            this.upstreamCompletionTracking = new ConcurrentDictionary<string, HashSet<string>>();
            this.expectedUpstreamCount = new ConcurrentDictionary<string, int>();
            this.completedNodesForCompensation = new ConcurrentDictionary<Guid, List<(string NodeId, string? CompensationNodeId)>>();
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

            // Validate workflow definition before execution
            var validator = new Workflow.WorkflowValidator();
            var validationResult = validator.Validate(workflowDefinition);
            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException($"Workflow validation failed:{Environment.NewLine}{errorMessage}");
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
                this.workflowDefinitions[context.InstanceId] = workflowDefinition;

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
        /// Resumes a paused workflow execution from checkpoint.
        /// Restores workflow state and continues execution from where it left off.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to resume.</param>
        /// <param name="timeout">Optional timeout for the remaining workflow execution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workflow execution context with results.</returns>
        /// <exception cref="InvalidOperationException">Thrown if checkpoint storage not configured or checkpoint not found.</exception>
        public async Task<WorkflowExecutionContext> ResumeAsync(
            Guid workflowInstanceId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (this.checkpointStorage == null)
            {
                throw new InvalidOperationException("Cannot resume workflow: checkpoint storage not configured");
            }

            // Load checkpoint
            var checkpoint = await this.checkpointStorage.LoadCheckpointAsync(workflowInstanceId, cancellationToken);
            if (checkpoint == null)
            {
                throw new InvalidOperationException($"Checkpoint not found for workflow {workflowInstanceId}");
            }

            if (checkpoint.Status != WorkflowExecutionStatus.Paused)
            {
                throw new InvalidOperationException($"Cannot resume workflow in status {checkpoint.Status}");
            }

            // Deserialize workflow definition
            var workflowDef = JsonSerializer.Deserialize<WorkflowDefinition>(checkpoint.WorkflowDefinitionJson);
            if (workflowDef == null)
            {
                throw new InvalidOperationException("Failed to deserialize workflow definition from checkpoint");
            }

            // Create a new execution context with the saved instance ID
            var context = new WorkflowExecutionContext
            {
                GraphId = checkpoint.WorkflowId,
                Status = WorkflowExecutionStatus.Running
            };

            // Use reflection to set the private instanceId field to match the checkpointed ID
            var instanceIdField = typeof(WorkflowExecutionContext).GetField("<InstanceId>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            instanceIdField?.SetValue(context, checkpoint.WorkflowInstanceId);

            // Restore variables
            foreach (var kvp in checkpoint.Variables)
            {
                context.Variables[kvp.Key] = kvp.Value;
            }

            // Set up message queues for each node
            this.SetupMessageQueues(workflowDef, context);

            // Set up message router
            this.SetupMessageRouter(workflowDef, context);

            // Restore node execution tracking
            foreach (var kvp in checkpoint.NodeStates)
            {
                var nodeState = kvp.Value;
                var nodeInstance = new NodeInstance
                {
                    NodeInstanceId = Guid.NewGuid(), // Generate new instance ID for resumed execution
                    NodeId = nodeState.NodeId,
                    WorkflowInstanceId = checkpoint.WorkflowInstanceId,
                    Status = nodeState.Status,
                    StartTime = nodeState.StartTime,
                    EndTime = nodeState.EndTime,
                    ErrorMessage = nodeState.ErrorMessage,
                    ExecutionContext = new NodeExecutionContext
                    {
                        InputData = new Dictionary<string, object>(nodeState.InputData),
                        OutputData = new Dictionary<string, object>(nodeState.OutputData)
                    }
                };
                this.nodeExecutionTracking[kvp.Key] = nodeInstance;
            }

            // Restore message queues
            foreach (var kvp in checkpoint.MessageQueues)
            {
                var nodeId = kvp.Key;
                if (context.NodeQueues.TryGetValue(nodeId, out var queueObj))
                {
                    var queue = (NodeMessageQueue)queueObj;
                    foreach (var serializedMsg in kvp.Value)
                    {
                        // Deserialize the message payload
                        var payload = JsonSerializer.Deserialize<object>(serializedMsg.PayloadJson);

                        // Reconstruct the message envelope
                        var envelope = new MessageEnvelope
                        {
                            MessageId = serializedMsg.MessageId,
                            MessageType = serializedMsg.MessageType,
                            Payload = payload,
                            Status = MessageStatus.Ready,
                            RetryCount = serializedMsg.RetryCount,
                            NotBefore = serializedMsg.NotBefore
                        };

                        // Restore the message to the queue
                        await queue.RestoreFromCheckpointAsync(envelope, cancellationToken);
                    }
                }
            }

            // Track active workflow
            this.activeWorkflows[context.InstanceId] = context;
            this.workflowDefinitions[context.InstanceId] = workflowDef;

            // Create cancellation token source with timeout
            var workflowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                workflowCts.CancelAfter(timeout.Value);
            }
            this.workflowCancellationSources[context.InstanceId] = workflowCts;

            try
            {
                // Resume execution (simplified - continue from current state)
                // Note: For a full implementation, we'd need to continue the execution loop
                // For now, we'll re-execute from the current state
                await this.ExecuteAsync(workflowDef, workflowCts.Token);

                return context;
            }
            finally
            {
                // Cleanup
                this.workflowCancellationSources.TryRemove(context.InstanceId, out _);

                // Delete checkpoint after successful completion
                if (context.Status == WorkflowExecutionStatus.Completed ||
                    context.Status == WorkflowExecutionStatus.Failed)
                {
                    await this.checkpointStorage.DeleteCheckpointAsync(context.InstanceId, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Pauses a running workflow execution.
        /// Creates a checkpoint of the current state and cancels execution.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to pause.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the pause operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if workflow not found or cannot be paused.</exception>
        public async Task PauseAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default)
        {
            if (this.checkpointStorage == null)
            {
                throw new InvalidOperationException("Cannot pause workflow: checkpoint storage not configured");
            }

            if (!this.activeWorkflows.TryGetValue(workflowInstanceId, out var context))
            {
                throw new InvalidOperationException($"Workflow {workflowInstanceId} not found");
            }

            if (context.Status != WorkflowExecutionStatus.Running)
            {
                throw new InvalidOperationException($"Cannot pause workflow in status {context.Status}");
            }

            // Create checkpoint before pausing
            var checkpoint = await this.CreateCheckpointAsync(workflowInstanceId, cancellationToken);
            if (checkpoint == null)
            {
                throw new InvalidOperationException($"Failed to create checkpoint for workflow {workflowInstanceId}");
            }

            // Update checkpoint status to Paused
            checkpoint.Status = WorkflowExecutionStatus.Paused;
            await this.checkpointStorage.SaveCheckpointAsync(checkpoint, cancellationToken);

            // Update context status
            context.Status = WorkflowExecutionStatus.Paused;

            // Cancel the workflow execution (this will stop the execution loop)
            if (this.workflowCancellationSources.TryGetValue(workflowInstanceId, out var cts))
            {
                cts.Cancel();
            }
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

        /// <summary>
        /// Recovers incomplete workflows from persistent storage and resumes their execution.
        /// Used for failure recovery and resuming workflows after engine restart.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A collection of recovered workflow instance IDs.</returns>
        public async Task<IReadOnlyCollection<Guid>> RecoverIncompleteWorkflowsAsync(
            CancellationToken cancellationToken = default)
        {
            if (this.checkpointStorage == null)
            {
                return Array.Empty<Guid>();
            }

            // Load all checkpoints from storage
            var checkpoints = await this.checkpointStorage.ListCheckpointsAsync(cancellationToken);

            var recoveredWorkflowIds = new List<Guid>();

            foreach (var checkpoint in checkpoints)
            {
                // Only recover workflows that are in Running or Paused status
                // Skip Completed, Failed, and Cancelled workflows
                if (checkpoint.Status != WorkflowExecutionStatus.Running &&
                    checkpoint.Status != WorkflowExecutionStatus.Paused)
                {
                    continue;
                }

                try
                {
                    // Update checkpoint status to Paused before resuming (ensures consistent state)
                    checkpoint.Status = WorkflowExecutionStatus.Paused;
                    await this.checkpointStorage.SaveCheckpointAsync(checkpoint, cancellationToken);

                    // Resume the workflow
                    await this.ResumeAsync(checkpoint.WorkflowInstanceId, timeout: null, cancellationToken);

                    recoveredWorkflowIds.Add(checkpoint.WorkflowInstanceId);
                }
                catch (Exception)
                {
                    // Log error and continue with next checkpoint
                    // In production, you'd want proper error logging here
                    continue;
                }
            }

            return recoveredWorkflowIds;
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
                // Publish WorkflowStartedEvent
                context.PublishEvent(new WorkflowStartedEvent
                {
                    WorkflowInstanceId = context.InstanceId,
                    WorkflowId = workflowDefinition.WorkflowId,
                    WorkflowName = workflowDefinition.WorkflowName,
                    TotalNodes = workflowDefinition.Nodes.Count,
                    Timestamp = DateTime.UtcNow
                });

                // Step 1: Set up message queues for each node
                this.SetupMessageQueues(workflowDefinition, context);

                // Step 2: Set up message router
                this.SetupMessageRouter(workflowDefinition, context);

                // Step 3: Setup upstream tracking for ALL join nodes
                this.SetupUpstreamTracking(workflowDefinition);

                // Step 3.5: Setup concurrency control
                this.SetupConcurrencyControl(context.InstanceId, workflowDefinition);

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

                        // Try to read a signal from this queue's channel (non-blocking)
                        // Note: Signals can be dropped due to channel coalescing, so we also check queue count
                        bool hasSignal = nodeQueue.MessageSignals.TryRead(out var signal);
                        bool hasMessages = nodeQueue.Count > 0;

                        if (hasSignal || hasMessages)
                        {
                            // If we have a signal, use it. Otherwise, we'll try to lease directly from the queue.
                            // ExecuteNodeAsync will handle the case where there's no message available.
                            messageProcessed = true;

                            // Use signal if available, otherwise create a dummy signal
                            // ProcessMessageAsync will trigger ExecuteNodeAsync which will lease the actual message
                            var messageToProcess = hasSignal ? signal : new NodeCompleteMessage
                            {
                                NodeId = "__dummy__",
                                Timestamp = DateTime.UtcNow
                            };

                            // Handle the message based on type
                            await this.ProcessMessageAsync(nodeDef, messageToProcess, workflowDefinition, context, cancellationToken);
                        }
                    }

                    // Check if workflow is complete
                    // Complete when: all queues are empty AND all tracked nodes FOR THIS WORKFLOW are terminal
                    bool allQueuesEmpty = context.NodeQueues.Values
                        .Cast<NodeMessageQueue>()
                        .All(q => q.Count == 0);

                    // Filter tracked nodes by this workflow's instance ID
                    var thisWorkflowNodes = this.nodeExecutionTracking.Values
                        .Where(n => n.WorkflowInstanceId == context.InstanceId)
                        .ToList();

                    // Don't exit early if no nodes have been tracked yet (workflow just started)
                    // Only exit when we have tracked nodes AND they're all terminal
                    bool allTrackedNodesTerminal = thisWorkflowNodes.Count > 0 &&
                        thisWorkflowNodes.All(nodeInstance =>
                            nodeInstance.Status == NodeExecutionStatus.Completed ||
                            nodeInstance.Status == NodeExecutionStatus.Failed ||
                            nodeInstance.Status == NodeExecutionStatus.Cancelled);

                    // Exit when all queues are empty AND all tracked nodes are terminal
                    // This ensures we don't exit before all nodes have had a chance to execute
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

                // Publish workflow completion event based on final status
                var duration = context.Duration ?? TimeSpan.Zero;

                if (context.Status == WorkflowExecutionStatus.Completed)
                {
                    context.PublishEvent(new WorkflowCompletedEvent
                    {
                        WorkflowInstanceId = context.InstanceId,
                        WorkflowId = workflowDefinition.WorkflowId,
                        Duration = duration,
                        Status = context.Status,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else if (context.Status == WorkflowExecutionStatus.Failed)
                {
                    var errorMessage = context.Variables.TryGetValue("__error", out var error)
                        ? error?.ToString() ?? "Workflow execution failed"
                        : "Workflow execution failed";

                    context.PublishEvent(new WorkflowFailedEvent
                    {
                        WorkflowInstanceId = context.InstanceId,
                        WorkflowId = workflowDefinition.WorkflowId,
                        Duration = duration,
                        ErrorMessage = errorMessage,
                        Timestamp = DateTime.UtcNow
                    });
                }
                else if (context.Status == WorkflowExecutionStatus.Cancelled)
                {
                    var reason = context.Variables.TryGetValue("__error", out var error)
                        ? error?.ToString() ?? "Workflow execution cancelled"
                        : "Workflow execution cancelled";

                    context.PublishEvent(new WorkflowCancelledEvent
                    {
                        WorkflowInstanceId = context.InstanceId,
                        WorkflowId = workflowDefinition.WorkflowId,
                        Duration = duration,
                        Reason = reason,
                        Timestamp = DateTime.UtcNow
                    });
                }

                return context;
            }
            catch (Exception ex)
            {
                context.Status = WorkflowExecutionStatus.Failed;
                context.EndTime = DateTime.UtcNow;
                context.Variables["__error"] = ex.Message;

                // Publish WorkflowFailedEvent on exception
                var duration = context.Duration ?? TimeSpan.Zero;
                context.PublishEvent(new WorkflowFailedEvent
                {
                    WorkflowInstanceId = context.InstanceId,
                    WorkflowId = workflowDefinition.WorkflowId,
                    Duration = duration,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });

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
        /// Passes complete NodeConnection objects to support conditional routing.
        /// </summary>
        private void SetupMessageRouter(WorkflowDefinition workflowDefinition, WorkflowExecutionContext context)
        {
            var dlq = new DeadLetterQueue();
            var router = new MessageRouter(dlq);

            // Add routes based on connections (pass complete connection objects)
            foreach (var connection in workflowDefinition.Connections)
            {
                if (connection.IsEnabled)
                {
                    router.AddRoute(connection);
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
        /// Sets up concurrency control for the workflow.
        /// Creates a workflow-level concurrency limiter and registers nodes with the node throttler.
        /// Also registers circuit breakers and initializes compensation tracking.
        /// </summary>
        private void SetupConcurrencyControl(Guid workflowInstanceId, WorkflowDefinition workflowDefinition)
        {
            // Create workflow-level concurrency limiter if max concurrency is set
            var concurrencyLimiter = new ConcurrencyLimiter(workflowDefinition.MaxConcurrency);
            this.workflowConcurrencyLimiters[workflowInstanceId] = concurrencyLimiter;

            // Initialize compensation tracking for this workflow
            this.completedNodesForCompensation[workflowInstanceId] = new List<(string NodeId, string? CompensationNodeId)>();

            // Register all nodes with the node throttler and circuit breaker if they have policies
            foreach (var nodeDef in workflowDefinition.Nodes)
            {
                if (nodeDef.MaxConcurrentExecutions > 0)
                {
                    this.nodeThrottler.RegisterNode(nodeDef.NodeId, nodeDef.MaxConcurrentExecutions);
                }

                // Register circuit breaker policy if present
                if (nodeDef.CircuitBreakerPolicy != null)
                {
                    this.circuitBreakerManager.RegisterNode(nodeDef.NodeId, nodeDef.CircuitBreakerPolicy);
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

                            // Publish NodeCancelledEvent
                            context.PublishEvent(new NodeCancelledEvent
                            {
                                WorkflowInstanceId = context.InstanceId,
                                NodeId = nodeDef.NodeId,
                                NodeName = nodeDef.NodeName,
                                NodeInstanceId = nodeInstance.NodeInstanceId,
                                Reason = cancelMsg.Reason ?? "Cancelled by upstream",
                                Timestamp = DateTime.UtcNow
                            });

                            // Publish progress update
                            var progress = this.CalculateProgress(context, workflowDefinition.Nodes.Count);
                            context.PublishProgress(progress);
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

            // If node has already reached a terminal status, complete the lease and skip execution
            if (nodeInstance.Status == NodeExecutionStatus.Completed ||
                nodeInstance.Status == NodeExecutionStatus.Failed ||
                nodeInstance.Status == NodeExecutionStatus.Cancelled)
            {
                await queue.CompleteAsync(lease, cancellationToken);
                return;
            }

            // Get node definition for concurrency control
            var nodeDef = workflowDefinition.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (nodeDef == null)
            {
                await queue.AbandonAsync(lease, cancellationToken);
                throw new InvalidOperationException($"Node definition not found for NodeId: {nodeId}");
            }

            // Acquire concurrency slots
            ConcurrencySlot? workflowSlot = null;
            NodeThrottleSlot? nodeSlot = null;
            bool leaseCompleted = false;

            try
            {
                // Acquire workflow-level concurrency slot
                if (this.workflowConcurrencyLimiters.TryGetValue(context.InstanceId, out var concurrencyLimiter))
                {
                    workflowSlot = await concurrencyLimiter.AcquireAsync(nodeDef.Priority, cancellationToken);
                }

                // Acquire node-level throttle slot
                nodeSlot = await this.nodeThrottler.AcquireAsync(nodeId, cancellationToken);

                // Execute the node
                nodeInstance.Status = NodeExecutionStatus.Running;
                nodeInstance.StartTime = DateTime.UtcNow;

                // Raise NodeStarted event
                this.NodeStarted?.Invoke(nodeId, nodeInstance.NodeInstanceId);

                // Publish NodeStartedEvent
                context.PublishEvent(new NodeStartedEvent
                {
                    WorkflowInstanceId = context.InstanceId,
                    NodeId = nodeId,
                    NodeName = nodeDef?.NodeName ?? nodeId,
                    NodeInstanceId = nodeInstance.NodeInstanceId,
                    Timestamp = DateTime.UtcNow
                });

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

                // Check circuit breaker before execution
                if (!this.circuitBreakerManager.AllowRequest(nodeId))
                {
                    // Circuit is open - check if fallback node exists
                    if (!string.IsNullOrEmpty(nodeDef.FallbackNodeId))
                    {
                        // Route to fallback node
                        var fallbackQueue = (NodeMessageQueue)context.NodeQueues[nodeDef.FallbackNodeId];
                        var fallbackMessage = new NodeCompleteMessage
                        {
                            NodeId = nodeId,
                            Timestamp = DateTime.UtcNow,
                            NodeContext = nodeContext
                        };
                        await fallbackQueue.EnqueueAsync(fallbackMessage, cancellationToken);

                        // Mark as completed with fallback
                        nodeInstance.Status = NodeExecutionStatus.Completed;
                        nodeInstance.EndTime = DateTime.UtcNow;
                        nodeInstance.ErrorMessage = "Circuit breaker open - routed to fallback";
                    }
                    else
                    {
                        // No fallback - fail immediately
                        throw new InvalidOperationException($"Circuit breaker is open for node {nodeId}");
                    }
                }
                else
                {
                    // Execute node with retry policy
                    int retryAttempt = 0;
                    NodeInstance? result = null;

                    while (true)
                    {
                        try
                        {
                            // Execute to finish or failure
                            result = await node.ExecuteAsync(context, nodeContext, cancellationToken);

                            if (result.Status == NodeExecutionStatus.Completed)
                            {
                                // Success - record in circuit breaker
                                this.circuitBreakerManager.RecordSuccess(nodeId);
                                break;
                            }
                            else if (result.Status == NodeExecutionStatus.Failed)
                            {
                                // Failed - record in circuit breaker
                                this.circuitBreakerManager.RecordFailure(nodeId);

                                // Check if retry is configured and should retry
                                if (nodeDef.RetryPolicy != null && result.Exception != null)
                                {
                                    if (retryAttempt < nodeDef.RetryPolicy.MaxAttempts - 1 &&
                                        nodeDef.RetryPolicy.ShouldRetry(result.Exception))
                                    {
                                        // Calculate delay and retry
                                        var delay = nodeDef.RetryPolicy.CalculateDelay(retryAttempt);
                                        retryAttempt++;
                                        await Task.Delay(delay, cancellationToken);
                                        continue; // Retry
                                    }
                                }

                                // No retry or max attempts reached - fail
                                break;
                            }
                            else
                            {
                                // Other status (Cancelled, etc.)
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Record failure in circuit breaker
                            this.circuitBreakerManager.RecordFailure(nodeId);

                            // Check if retry is configured and should retry
                            if (nodeDef.RetryPolicy != null)
                            {
                                if (retryAttempt < nodeDef.RetryPolicy.MaxAttempts - 1 &&
                                    nodeDef.RetryPolicy.ShouldRetry(ex))
                                {
                                    // Calculate delay and retry
                                    var delay = nodeDef.RetryPolicy.CalculateDelay(retryAttempt);
                                    retryAttempt++;
                                    await Task.Delay(delay, cancellationToken);
                                    continue; // Retry
                                }
                            }

                            // No retry or max attempts reached - rethrow
                            throw;
                        }
                    }

                    // Update node instance with result
                    if (result != null)
                    {
                        nodeInstance.Status = result.Status;
                        nodeInstance.EndTime = result.EndTime;
                        nodeInstance.ErrorMessage = result.ErrorMessage;
                        nodeInstance.Exception = result.Exception;
                        nodeInstance.SourcePort = result.SourcePort;
                    }
                }

                // Complete the lease
                await queue.CompleteAsync(lease, cancellationToken);
                leaseCompleted = true;

                // Route completion/failure messages to downstream queues
                // MessageRouter enqueues → triggers channel signals → downstream workers wake up
                if (nodeInstance.Status == NodeExecutionStatus.Completed)
                {
                    // Track completed node for compensation (Saga pattern)
                    if (!string.IsNullOrEmpty(nodeDef.CompensationNodeId))
                    {
                        if (this.completedNodesForCompensation.TryGetValue(context.InstanceId, out var completedList))
                        {
                            completedList.Add((nodeId, nodeDef.CompensationNodeId));
                        }
                    }

                    var completeMessage = new NodeCompleteMessage
                    {
                        NodeId = nodeId,
                        Timestamp = DateTime.UtcNow,
                        NodeInstanceId = nodeInstance.NodeInstanceId,
                        NodeContext = nodeContext,
                        SourcePort = nodeInstance.SourcePort
                    };

                    await router.RouteMessageAsync(completeMessage, context, cancellationToken);

                    // Raise NodeCompleted event
                    var duration = nodeInstance.Duration ?? TimeSpan.Zero;
                    this.NodeCompleted?.Invoke(nodeId, nodeInstance.NodeInstanceId, duration);

                    // Publish NodeCompletedEvent
                    context.PublishEvent(new NodeCompletedEvent
                    {
                        WorkflowInstanceId = context.InstanceId,
                        NodeId = nodeId,
                        NodeName = nodeDef?.NodeName ?? nodeId,
                        NodeInstanceId = nodeInstance.NodeInstanceId,
                        Duration = duration,
                        Timestamp = DateTime.UtcNow
                    });

                    // Publish progress update
                    var progress = this.CalculateProgress(context, workflowDefinition.Nodes.Count);
                    context.PublishProgress(progress);
                }
                else if (nodeInstance.Status == NodeExecutionStatus.Failed)
                {
                    var failMessage = new NodeFailMessage
                    {
                        NodeId = nodeId,
                        Timestamp = DateTime.UtcNow,
                        ErrorMessage = nodeInstance.ErrorMessage ?? "Node execution failed",
                        Exception = nodeInstance.Exception
                    };

                    await router.RouteMessageAsync(failMessage, context, cancellationToken);

                    // Raise NodeFailed event
                    this.NodeFailed?.Invoke(nodeId, nodeInstance.NodeInstanceId, nodeInstance.ErrorMessage ?? "Node execution failed");

                    // Publish NodeFailedEvent
                    context.PublishEvent(new NodeFailedEvent
                    {
                        WorkflowInstanceId = context.InstanceId,
                        NodeId = nodeId,
                        NodeName = nodeDef?.NodeName ?? nodeId,
                        NodeInstanceId = nodeInstance.NodeInstanceId,
                        ErrorMessage = nodeInstance.ErrorMessage ?? "Node execution failed",
                        Exception = nodeInstance.Exception,
                        Timestamp = DateTime.UtcNow
                    });

                    // Publish progress update
                    var progressFailed = this.CalculateProgress(context, workflowDefinition.Nodes.Count);
                    context.PublishProgress(progressFailed);

                    // Execute compensation logic (Saga pattern)
                    await this.ExecuteCompensationAsync(
                        nodeId,
                        nodeInstance.Exception ?? new Exception(nodeInstance.ErrorMessage ?? "Node execution failed"),
                        null,
                        workflowDefinition,
                        context,
                        cancellationToken);

                    // Propagate cancellation to downstream nodes
                    await this.CancelNodeAndPropagate(nodeId, nodeInstance.ErrorMessage ?? "Node execution failed", workflowDefinition, context, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Handle execution error
                nodeInstance.Status = NodeExecutionStatus.Failed;
                nodeInstance.EndTime = DateTime.UtcNow;
                nodeInstance.ErrorMessage = ex.Message;
                nodeInstance.Exception = ex;

                // Only abandon the lease if it hasn't been completed yet
                if (!leaseCompleted)
                {
                    await queue.AbandonAsync(lease, cancellationToken);
                }

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

                // Publish NodeFailedEvent
                var nodeDefForException = workflowDefinition.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
                context.PublishEvent(new NodeFailedEvent
                {
                    WorkflowInstanceId = context.InstanceId,
                    NodeId = nodeId,
                    NodeName = nodeDefForException?.NodeName ?? nodeId,
                    NodeInstanceId = nodeInstance.NodeInstanceId,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });

                // Publish progress update
                var progressException = this.CalculateProgress(context, workflowDefinition.Nodes.Count);
                context.PublishProgress(progressException);

                // Execute compensation logic (Saga pattern)
                await this.ExecuteCompensationAsync(
                    nodeId,
                    ex,
                    null,
                    workflowDefinition,
                    context,
                    cancellationToken);

                // Propagate cancellation to downstream nodes
                await this.CancelNodeAndPropagate(nodeId, ex.Message, workflowDefinition, context, cancellationToken);
            }
            finally
            {
                // Release concurrency slots
                workflowSlot?.Dispose();
                nodeSlot?.Dispose();
            }
        }

        /// <summary>
        /// Executes compensation nodes in reverse order (Saga pattern).
        /// Called when a workflow fails to rollback completed operations.
        /// </summary>
        /// <param name="failedNodeId">The ID of the node that failed.</param>
        /// <param name="failureException">The exception that caused the failure.</param>
        /// <param name="failedNodeOutput">The output from the failed node (if any).</param>
        /// <param name="workflowDefinition">The workflow definition.</param>
        /// <param name="context">The workflow execution context.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ExecuteCompensationAsync(
            string failedNodeId,
            Exception failureException,
            object? failedNodeOutput,
            WorkflowDefinition workflowDefinition,
            WorkflowExecutionContext context,
            CancellationToken cancellationToken)
        {
            // Get completed nodes for this workflow
            if (!this.completedNodesForCompensation.TryGetValue(context.InstanceId, out var completedNodes))
            {
                // No completed nodes to compensate
                return;
            }

            if (completedNodes.Count == 0)
            {
                // No nodes with compensation configured
                return;
            }

            // Build compensation context
            var compensationContext = new CompensationContext(failedNodeId, failureException, failedNodeOutput);

            // Add nodes to compensation list (in reverse order - already inserted at beginning)
            foreach (var (nodeId, compensationNodeId) in completedNodes)
            {
                if (!string.IsNullOrEmpty(compensationNodeId))
                {
                    compensationContext.AddNodeToCompensate(compensationNodeId);
                }
            }

            // Execute compensation nodes in order (they're already in reverse order)
            foreach (var compensationNodeId in compensationContext.NodesToCompensate)
            {
                try
                {
                    // Get compensation node definition
                    var nodeDef = workflowDefinition.Nodes.FirstOrDefault(n => n.NodeId == compensationNodeId);
                    if (nodeDef == null)
                    {
                        continue; // Skip if compensation node not found
                    }

                    // Get or create compensation node instance
                    var node = this.nodeInstances.GetOrAdd(compensationNodeId, _ => this.nodeFactory.CreateNode(nodeDef));

                    // Create node execution context with compensation context
                    var nodeContext = new NodeExecutionContext();
                    nodeContext.InputData["CompensationContext"] = compensationContext;

                    // Execute compensation node
                    var result = await node.ExecuteAsync(context, nodeContext, cancellationToken);

                    if (result.Status == NodeExecutionStatus.Failed)
                    {
                        // Log compensation failure but continue with other compensations
                        // In production, you might want to implement compensation failure policies
                        Console.WriteLine($"Compensation node {compensationNodeId} failed: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    // Log exception but continue with other compensations
                    Console.WriteLine($"Exception executing compensation node {compensationNodeId}: {ex.Message}");
                }
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
        /// Calculates the current progress of workflow execution.
        /// </summary>
        /// <param name="context">The workflow execution context.</param>
        /// <param name="totalNodes">Total number of nodes in the workflow.</param>
        /// <returns>A ProgressUpdate object with current execution metrics.</returns>
        private ProgressUpdate CalculateProgress(WorkflowExecutionContext context, int totalNodes)
        {
            var nodeInstances = this.nodeExecutionTracking.Values.ToList();

            var nodesCompleted = nodeInstances.Count(n => n.Status == NodeExecutionStatus.Completed);
            var nodesRunning = nodeInstances.Count(n => n.Status == NodeExecutionStatus.Running);
            var nodesPending = nodeInstances.Count(n => n.Status == NodeExecutionStatus.Pending);
            var nodesFailed = nodeInstances.Count(n => n.Status == NodeExecutionStatus.Failed);
            var nodesCancelled = nodeInstances.Count(n => n.Status == NodeExecutionStatus.Cancelled);

            // Calculate percent complete: (completed + failed + cancelled) / totalNodes * 100
            var terminalNodes = nodesCompleted + nodesFailed + nodesCancelled;
            var percentComplete = totalNodes > 0 ? (double)terminalNodes / totalNodes * 100.0 : 0.0;

            // Estimate time remaining based on average node duration
            TimeSpan? estimatedTimeRemaining = null;
            if (nodesCompleted > 0)
            {
                var completedNodes = nodeInstances.Where(n => n.Status == NodeExecutionStatus.Completed && n.Duration.HasValue).ToList();
                if (completedNodes.Any())
                {
                    var averageDuration = TimeSpan.FromTicks((long)completedNodes.Average(n => n.Duration!.Value.Ticks));
                    var remainingNodes = totalNodes - terminalNodes;
                    if (remainingNodes > 0)
                    {
                        estimatedTimeRemaining = TimeSpan.FromTicks(averageDuration.Ticks * remainingNodes);
                    }
                }
            }

            return new ProgressUpdate
            {
                WorkflowInstanceId = context.InstanceId,
                PercentComplete = percentComplete,
                NodesCompleted = nodesCompleted,
                NodesRunning = nodesRunning,
                NodesPending = nodesPending,
                NodesFailed = nodesFailed,
                NodesCancelled = nodesCancelled,
                TotalNodes = totalNodes,
                EstimatedTimeRemaining = estimatedTimeRemaining,
                Timestamp = DateTime.UtcNow
            };
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

        /// <summary>
        /// Creates a checkpoint of the current workflow state.
        /// </summary>
        /// <param name="workflowInstanceId">The workflow instance ID to checkpoint.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created checkpoint, or null if workflow not found.</returns>
        private async Task<WorkflowCheckpoint?> CreateCheckpointAsync(
            Guid workflowInstanceId,
            CancellationToken cancellationToken = default)
        {
            if (!this.activeWorkflows.TryGetValue(workflowInstanceId, out var context))
            {
                return null;
            }

            if (!this.workflowDefinitions.TryGetValue(workflowInstanceId, out var workflowDef))
            {
                return null;
            }

            // Capture node states
            var nodeStates = new Dictionary<string, NodeInstanceState>();
            foreach (var kvp in this.nodeExecutionTracking)
            {
                var nodeInstance = kvp.Value;
                var nodeState = new NodeInstanceState
                {
                    NodeId = nodeInstance.NodeId,
                    Status = nodeInstance.Status,
                    StartTime = nodeInstance.StartTime,
                    EndTime = nodeInstance.EndTime,
                    ErrorMessage = nodeInstance.ErrorMessage,
                    InputData = nodeInstance.ExecutionContext?.InputData ?? new Dictionary<string, object>(),
                    OutputData = nodeInstance.ExecutionContext?.OutputData ?? new Dictionary<string, object>()
                };
                nodeStates[kvp.Key] = nodeState;
            }

            // Capture message queues
            var messageQueues = new Dictionary<string, List<SerializedMessage>>();
            foreach (var kvp in context.NodeQueues)
            {
                var nodeQueue = (NodeMessageQueue)kvp.Value;
                var messages = await nodeQueue.GetAllMessagesAsync(cancellationToken);

                var serializedMessages = new List<SerializedMessage>();
                foreach (var envelope in messages)
                {
                    var serializedMsg = new SerializedMessage
                    {
                        MessageId = envelope.MessageId,
                        MessageType = envelope.MessageType,
                        PayloadJson = JsonSerializer.Serialize(envelope.Payload),
                        RetryCount = envelope.RetryCount,
                        NotBefore = envelope.NotBefore
                    };
                    serializedMessages.Add(serializedMsg);
                }

                if (serializedMessages.Count > 0)
                {
                    messageQueues[kvp.Key] = serializedMessages;
                }
            }

            // Create checkpoint
            var checkpoint = new WorkflowCheckpoint
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowId = workflowDef.WorkflowId,
                WorkflowDefinitionJson = JsonSerializer.Serialize(workflowDef),
                Status = context.Status,
                StartTime = context.StartTime,
                CheckpointTime = DateTime.UtcNow,
                Variables = new Dictionary<string, object>(context.Variables),
                NodeStates = nodeStates,
                MessageQueues = messageQueues,
                ErrorMessages = new List<string>()
            };

            // Collect error messages from context if available
            if (context.Variables.TryGetValue("__error", out var errorObj))
            {
                checkpoint.ErrorMessages.Add(errorObj?.ToString() ?? string.Empty);
            }

            if (context.Variables.TryGetValue("__node_errors", out var nodeErrorsObj))
            {
                checkpoint.ErrorMessages.Add(nodeErrorsObj?.ToString() ?? string.Empty);
            }

            // Save checkpoint if storage is configured
            if (this.checkpointStorage != null)
            {
                await this.checkpointStorage.SaveCheckpointAsync(checkpoint, cancellationToken);
            }

            return checkpoint;
        }
    }
}
