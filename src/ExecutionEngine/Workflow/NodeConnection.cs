// -----------------------------------------------------------------------
// <copyright file="NodeConnection.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Routing;

/// <summary>
/// Represents a directed connection (edge) between two nodes in a workflow graph.
/// </summary>
public class NodeConnection
{
    /// <summary>
    /// Gets or sets the source node ID.
    /// </summary>
    public string SourceNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target node ID.
    /// </summary>
    public string TargetNodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message type that triggers this connection.
    /// When the source node produces a message of this type, the target node will be triggered.
    /// </summary>
    public MessageType TriggerMessageType { get; set; } = MessageType.Complete;

    /// <summary>
    /// Gets or sets an optional condition expression that must be true for the connection to trigger.
    /// If null or empty, the connection always triggers when the message type matches.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this connection is enabled.
    /// Disabled connections are not followed during workflow execution.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets optional metadata for the connection.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the priority of this connection (higher priority connections are evaluated first).
    /// Useful when multiple connections from the same source node compete.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets the source port name for multi-port routing.
    /// If null or empty, uses the default/primary output port.
    /// Allows nodes to have multiple named outputs (e.g., "success", "failure", "timeout").
    /// </summary>
    public string? SourcePort { get; set; }

    /// <summary>
    /// Gets or sets the target port name for multi-port routing.
    /// If null or empty, uses the default/primary input port.
    /// Allows nodes to have multiple named inputs.
    /// </summary>
    public string? TargetPort { get; set; }

    /// <summary>
    /// Evaluates whether the connection's condition is met based on the node execution context.
    /// If no condition is specified, returns true (connection is always active).
    /// </summary>
    /// <param name="context">The node execution context containing output data.</param>
    /// <returns>True if the condition is met or no condition exists, false otherwise.</returns>
    public bool IsConditionMet(NodeExecutionContext? context)
    {
        // No condition means always active
        if (string.IsNullOrWhiteSpace(this.Condition))
        {
            return true;
        }

        // No context means we can't evaluate - return false for safety
        if (context == null)
        {
            return false;
        }

        try
        {
            return ConditionEvaluator.Evaluate(this.Condition, context);
        }
        catch
        {
            // Invalid condition expression - return false for safety
            return false;
        }
    }
}
