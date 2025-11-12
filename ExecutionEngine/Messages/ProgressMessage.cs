// -----------------------------------------------------------------------
// <copyright file="ProgressMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Enums;

/// <summary>
/// Message indicating node progress update.
/// Used for long-running nodes to report status.
/// </summary>
public class ProgressMessage : INodeMessage
{
    /// <inheritdoc/>
    public string NodeId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MessageType MessageType => MessageType.Progress;

    /// <inheritdoc/>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc/>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the node instance ID reporting progress.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }
}
