// -----------------------------------------------------------------------
// <copyright file="NodeCancelMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Enums;

/// <summary>
/// Message indicating that a node was cancelled.
/// Triggers cascade cancellation: when received, downstream nodes cancel recursively.
///
/// Cancellation propagation:
/// - When a node fails or is explicitly cancelled, all pending outbound messages are cleared (moved to DLQ)
/// - OnCancel message is sent to all downstream nodes
/// - Downstream nodes receive OnCancel, cancel their execution, and propagate OnCancel further downstream
/// - This creates a cancellation cascade that cleanly shuts down dependent branches
/// </summary>
public class NodeCancelMessage : INodeMessage
{
    /// <inheritdoc/>
    public string NodeId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MessageType MessageType => MessageType.Cancel;

    /// <inheritdoc/>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc/>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the node instance ID that was cancelled.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets whether this cancellation was triggered by an upstream failure.
    /// </summary>
    public bool CascadeFromFailure { get; set; }
}
