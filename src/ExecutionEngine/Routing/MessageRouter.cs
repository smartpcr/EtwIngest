// -----------------------------------------------------------------------
// <copyright file="MessageRouter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Routing;

using System.Collections.Concurrent;
using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Messages;
using ExecutionEngine.Queue;
using ExecutionEngine.Workflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Routes messages from source nodes to target node queues based on workflow graph edges.
/// Supports conditional routing, broadcast, and error handling.
/// </summary>
public class MessageRouter : IMessageRouter
{
    private readonly ILogger<MessageRouter> logger;
    private readonly ConcurrentDictionary<string, List<NodeConnection>> routingTable;
    private readonly IDeadLetterQueue deadLetterQueue;

    /// <summary>
    /// Initializes a new instance of the MessageRouter class.
    /// </summary>
    /// <param name="deadLetterQueue">The dead letter queue for failed messages.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public MessageRouter(IDeadLetterQueue deadLetterQueue, ILogger<MessageRouter>? logger = null)
    {
        this.logger = logger ?? NullLogger<MessageRouter>.Instance;
        this.routingTable = new ConcurrentDictionary<string, List<NodeConnection>>();
        this.deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
    }

    /// <summary>
    /// Adds a routing connection from source node to target node.
    /// </summary>
    /// <param name="connection">The node connection to add.</param>
    public void AddRoute(NodeConnection connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.SourceNodeId))
        {
            this.logger.LogError("Cannot add route: Source node ID is null or whitespace");
            throw new ArgumentException("Source node ID cannot be null or whitespace.", nameof(connection));
        }

        if (string.IsNullOrWhiteSpace(connection.TargetNodeId))
        {
            this.logger.LogError("Cannot add route: Target node ID is null or whitespace for source {SourceNodeId}", connection.SourceNodeId);
            throw new ArgumentException("Target node ID cannot be null or whitespace.", nameof(connection));
        }

        this.logger.LogDebug("Adding route from {SourceNodeId} to {TargetNodeId} (MessageType: {MessageType})",
            connection.SourceNodeId, connection.TargetNodeId, connection.TriggerMessageType);

        this.routingTable.AddOrUpdate(
            connection.SourceNodeId,
            _ => new List<NodeConnection> { connection },
            (_, list) =>
            {
                // Check if connection already exists (same source and target)
                if (!list.Any(c => c.TargetNodeId == connection.TargetNodeId))
                {
                    list.Add(connection);
                    this.logger.LogInformation("Route added: {SourceNodeId} -> {TargetNodeId}", connection.SourceNodeId, connection.TargetNodeId);
                }
                else
                {
                    this.logger.LogDebug("Route already exists: {SourceNodeId} -> {TargetNodeId}", connection.SourceNodeId, connection.TargetNodeId);
                }

                return list;
            });
    }

    /// <summary>
    /// Adds a routing edge from source node to target node (backward compatibility).
    /// Creates a NodeConnection with default settings (MessageType.Complete, no condition).
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    [Obsolete("Use AddRoute(NodeConnection) instead")]
    public void AddRoute(string sourceNodeId, string targetNodeId)
    {
        var connection = new NodeConnection
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            TriggerMessageType = MessageType.Complete,
            IsEnabled = true
        };

        this.AddRoute(connection);
    }

    /// <summary>
    /// Removes a routing edge from source node to target node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>True if the route was removed.</returns>
    public bool RemoveRoute(string sourceNodeId, string targetNodeId)
    {
        if (this.routingTable.TryGetValue(sourceNodeId, out var connections))
        {
            var toRemove = connections.FirstOrDefault(c => c.TargetNodeId == targetNodeId);
            if (toRemove != null)
            {
                return connections.Remove(toRemove);
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all target node IDs for a given source node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <returns>Array of target node IDs.</returns>
    public string[] GetTargets(string sourceNodeId)
    {
        if (this.routingTable.TryGetValue(sourceNodeId, out var connections))
        {
            return connections.Select(c => c.TargetNodeId).ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Gets all connections for a given source node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <returns>Array of NodeConnection objects.</returns>
    public NodeConnection[] GetConnections(string sourceNodeId)
    {
        if (this.routingTable.TryGetValue(sourceNodeId, out var connections))
        {
            return connections.ToArray();
        }

        return Array.Empty<NodeConnection>();
    }

    /// <summary>
    /// Routes a message from a source node to all target node queues.
    /// Evaluates conditions and filters by message type before routing.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="workflowContext">The workflow execution context containing node queues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of successful deliveries.</returns>
    public async Task<int> RouteMessageAsync(
        INodeMessage message,
        WorkflowExecutionContext workflowContext,
        CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (workflowContext == null)
        {
            throw new ArgumentNullException(nameof(workflowContext));
        }

        // Get all connections for this source node
        var connections = this.GetConnections(message.NodeId);

        if (connections.Length == 0)
        {
            // No routes defined - this is not an error, some nodes may be terminal
            this.logger.LogDebug("No routes defined for node {NodeId}, message type {MessageType}",
                message.NodeId, message.GetType().Name);
            return 0;
        }

        this.logger.LogDebug("Routing message from {NodeId}, found {ConnectionCount} connections",
            message.NodeId, connections.Length);

        // Determine message type
        var messageType = message switch
        {
            NodeCompleteMessage => MessageType.Complete,
            NodeFailMessage => MessageType.Fail,
            NodeCancelMessage => MessageType.Cancel,
            NodeNextMessage => MessageType.Next,
            _ => MessageType.Complete // Default to Complete for unknown types
        };

        // Extract node context for condition evaluation
        NodeExecutionContext? nodeContext = null;
        if (message is NodeCompleteMessage completeMsg)
        {
            nodeContext = completeMsg.NodeContext;
        }

        var successCount = 0;

        // Evaluate each connection
        foreach (var connection in connections)
        {
            try
            {
                // Skip disabled connections
                if (!connection.IsEnabled)
                {
                    continue;
                }

                // Filter by message type
                if (connection.TriggerMessageType != messageType)
                {
                    continue;
                }

                // Evaluate condition (if specified)
                if (!connection.IsConditionMet(nodeContext))
                {
                    continue;
                }

                // Filter by source port (if specified on connection)
                if (!string.IsNullOrEmpty(connection.SourcePort))
                {
                    var messageSourcePort = (message as NodeCompleteMessage)?.SourcePort ?? string.Empty;
                    if (!connection.SourcePort.Equals(messageSourcePort, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Get or create the target node's queue
                if (!workflowContext.NodeQueues.TryGetValue(connection.TargetNodeId, out var queueObj))
                {
                    // Target queue doesn't exist - skip this route
                    continue;
                }

                if (queueObj is NodeMessageQueue queue)
                {
                    var result = await queue.EnqueueAsync(message, cancellationToken);
                    if (result)
                    {
                        successCount++;
                        this.logger.LogDebug("Successfully routed message to {TargetNodeId}", connection.TargetNodeId);
                    }
                    else
                    {
                        this.logger.LogWarning("Failed to enqueue message to {TargetNodeId}", connection.TargetNodeId);
                    }
                }
            }
            catch (Exception ex)
            {
                // Failed to deliver to this target - log to dead letter queue
                this.logger.LogError(ex, "Failed to route message from {SourceNodeId} to {TargetNodeId}",
                    message.NodeId, connection.TargetNodeId);

                var envelope = new MessageEnvelope
                {
                    MessageId = message.MessageId,
                    MessageType = message.GetType().FullName ?? message.GetType().Name,
                    Payload = message
                };

                await this.deadLetterQueue.AddAsync(
                    envelope,
                    $"Failed to route message to target node '{connection.TargetNodeId}'",
                    ex);
            }
        }

        this.logger.LogInformation("Routed message from {SourceNodeId}, successful deliveries: {SuccessCount}/{TotalConnections}",
            message.NodeId, successCount, connections.Length);

        return successCount;
    }

    /// <summary>
    /// Routes a message to specific target nodes (override default routing).
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="targetNodeIds">Specific target node IDs to route to.</param>
    /// <param name="workflowContext">The workflow execution context containing node queues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of successful deliveries.</returns>
    public async Task<int> RouteToTargetsAsync(
        INodeMessage message,
        string[] targetNodeIds,
        WorkflowExecutionContext workflowContext,
        CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (targetNodeIds == null || targetNodeIds.Length == 0)
        {
            throw new ArgumentException("Target node IDs cannot be null or empty.", nameof(targetNodeIds));
        }

        if (workflowContext == null)
        {
            throw new ArgumentNullException(nameof(workflowContext));
        }

        this.logger.LogDebug("Routing message to {TargetCount} specific targets", targetNodeIds.Length);

        var successCount = 0;

        foreach (var targetNodeId in targetNodeIds)
        {
            try
            {
                if (!workflowContext.NodeQueues.TryGetValue(targetNodeId, out var queueObj))
                {
                    this.logger.LogWarning("Target queue not found for {TargetNodeId}", targetNodeId);
                    continue;
                }

                if (queueObj is NodeMessageQueue queue)
                {
                    var result = await queue.EnqueueAsync(message, cancellationToken);
                    if (result)
                    {
                        successCount++;
                        this.logger.LogDebug("Successfully routed message to {TargetNodeId}", targetNodeId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to route message to target {TargetNodeId}", targetNodeId);

                var envelope = new MessageEnvelope
                {
                    MessageId = message.MessageId,
                    MessageType = message.GetType().FullName ?? message.GetType().Name,
                    Payload = message
                };

                await this.deadLetterQueue.AddAsync(
                    envelope,
                    $"Failed to route message to target node '{targetNodeId}'",
                    ex);
            }
        }

        this.logger.LogInformation("Routed message to specific targets, successful deliveries: {SuccessCount}/{TotalTargets}",
            successCount, targetNodeIds.Length);

        return successCount;
    }

    /// <summary>
    /// Clears all routing entries.
    /// </summary>
    public void ClearRoutes()
    {
        this.routingTable.Clear();
    }

    /// <summary>
    /// Gets the total number of routes (connections) in the routing table.
    /// </summary>
    public int RouteCount => this.routingTable.Values.Sum(list => list.Count);
}
