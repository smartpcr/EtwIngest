// -----------------------------------------------------------------------
// <copyright file="IMessageRouter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Routing;

using ExecutionEngine.Contexts;
using ExecutionEngine.Messages;
using ExecutionEngine.Workflow;

/// <summary>
/// Interface for routing messages from source nodes to target node queues based on workflow graph edges.
/// Supports conditional routing, broadcast, and error handling.
/// </summary>
public interface IMessageRouter
{
    /// <summary>
    /// Gets the total number of routes (connections) in the routing table.
    /// </summary>
    int RouteCount { get; }

    /// <summary>
    /// Adds a routing connection from source node to target node.
    /// </summary>
    /// <param name="connection">The node connection to add.</param>
    void AddRoute(NodeConnection connection);

    /// <summary>
    /// Adds a routing edge from source node to target node (backward compatibility).
    /// Creates a NodeConnection with default settings (MessageType.Complete, no condition).
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    [Obsolete("Use AddRoute(NodeConnection) instead")]
    void AddRoute(string sourceNodeId, string targetNodeId);

    /// <summary>
    /// Removes a routing edge from source node to target node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <param name="targetNodeId">The target node ID.</param>
    /// <returns>True if the route was removed.</returns>
    bool RemoveRoute(string sourceNodeId, string targetNodeId);

    /// <summary>
    /// Gets all target node IDs for a given source node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <returns>Array of target node IDs.</returns>
    string[] GetTargets(string sourceNodeId);

    /// <summary>
    /// Gets all connections for a given source node.
    /// </summary>
    /// <param name="sourceNodeId">The source node ID.</param>
    /// <returns>Array of NodeConnection objects.</returns>
    NodeConnection[] GetConnections(string sourceNodeId);

    /// <summary>
    /// Routes a message from a source node to all target node queues.
    /// Evaluates conditions and filters by message type before routing.
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="workflowContext">The workflow execution context containing node queues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of successful deliveries.</returns>
    Task<int> RouteMessageAsync(
        INodeMessage message,
        WorkflowExecutionContext workflowContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes a message to specific target nodes (override default routing).
    /// </summary>
    /// <param name="message">The message to route.</param>
    /// <param name="targetNodeIds">Specific target node IDs to route to.</param>
    /// <param name="workflowContext">The workflow execution context containing node queues.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of successful deliveries.</returns>
    Task<int> RouteToTargetsAsync(
        INodeMessage message,
        string[] targetNodeIds,
        WorkflowExecutionContext workflowContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all routing entries.
    /// </summary>
    void ClearRoutes();
}
