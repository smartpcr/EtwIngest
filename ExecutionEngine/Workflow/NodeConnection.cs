// -----------------------------------------------------------------------
// <copyright file="NodeConnection.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using ExecutionEngine.Enums;

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
}
