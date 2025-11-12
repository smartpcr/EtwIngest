// -----------------------------------------------------------------------
// <copyright file="MessageRouter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Routing;

using System.Collections.Concurrent;
using ExecutionEngine.Contexts;
using ExecutionEngine.Messages;
using ExecutionEngine.Queue;

/// <summary>
/// Routes messages from source nodes to target node queues based on workflow graph edges.
/// Supports conditional routing, broadcast, and error handling.
/// </summary>
public class MessageRouter
{
    private readonly ConcurrentDictionary<string, List<string>> routingTable;
    private readonly DeadLetterQueue deadLetterQueue;

    /// <summary>
    /// Initializes a new instance of the MessageRouter class.
    /// </summary>
    /// <param name="deadLetterQueue">The dead letter queue for failed messages.</param>
    public MessageRouter(DeadLetterQueue deadLetterQueue)
    {
        this.routingTable = new ConcurrentDictionary<string, List<string>>();
        this.deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
    }

    /// <summary>
    /// Adds a routing edge from source node to target node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    public void AddRoute(string sourceNodeId, string targetNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId))
        {
            throw new ArgumentException("Source node ID cannot be null or whitespace.", nameof(sourceNodeId));
        }

        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            throw new ArgumentException("Target node ID cannot be null or whitespace.", nameof(targetNodeId));
        }

        this.routingTable.AddOrUpdate(
            sourceNodeId,
            _ => new List<string> { targetNodeId },
            (_, list) =>
            {
                if (!list.Contains(targetNodeId))
                {
                    list.Add(targetNodeId);
                }

                return list;
            });
    }

    /// <summary>
    /// Removes a routing edge from source node to target node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>True if the route was removed.</returns>
    public bool RemoveRoute(string sourceNodeId, string targetNodeId)
    {
        if (this.routingTable.TryGetValue(sourceNodeId, out var targets))
        {
            return targets.Remove(targetNodeId);
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
        if (this.routingTable.TryGetValue(sourceNodeId, out var targets))
        {
            return targets.ToArray();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Routes a message from a source node to all target node queues.
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

        // Get target nodes for this source
        var targets = this.GetTargets(message.NodeId);

        if (targets.Length == 0)
        {
            // No routes defined - this is not an error, some nodes may be terminal
            return 0;
        }

        int successCount = 0;

        foreach (var targetNodeId in targets)
        {
            try
            {
                // Get or create the target node's queue
                if (!workflowContext.NodeQueues.TryGetValue(targetNodeId, out var queueObj))
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
                    }
                }
            }
            catch (Exception ex)
            {
                // Failed to deliver to this target - log to dead letter queue
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

        int successCount = 0;

        foreach (var targetNodeId in targetNodeIds)
        {
            try
            {
                if (!workflowContext.NodeQueues.TryGetValue(targetNodeId, out var queueObj))
                {
                    continue;
                }

                if (queueObj is NodeMessageQueue queue)
                {
                    var result = await queue.EnqueueAsync(message, cancellationToken);
                    if (result)
                    {
                        successCount++;
                    }
                }
            }
            catch (Exception ex)
            {
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
    /// Gets the total number of routes in the routing table.
    /// </summary>
    public int RouteCount => this.routingTable.Sum(kvp => kvp.Value.Count);
}
