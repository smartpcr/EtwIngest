// -----------------------------------------------------------------------
// <copyright file="NodeCompleteMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;

/// <summary>
/// Message indicating that a node completed successfully.
/// Contains the node's execution context with output data for downstream nodes.
/// </summary>
public class NodeCompleteMessage : INodeMessage
{
    /// <inheritdoc/>
    public string NodeId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MessageType MessageType => MessageType.Complete;

    /// <inheritdoc/>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc/>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the node instance ID that completed.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the node execution context containing output data.
    /// This context will be passed as input to downstream nodes.
    /// </summary>
    public NodeExecutionContext? NodeContext { get; set; }

    /// <summary>
    /// Gets or sets the source port name that produced this message.
    /// Used for multi-port routing where a node can output messages on different named ports.
    /// If null or empty, indicates the default/primary output port.
    /// </summary>
    public string? SourcePort { get; set; }
}
