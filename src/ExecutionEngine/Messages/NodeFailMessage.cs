// -----------------------------------------------------------------------
// <copyright file="NodeFailMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;

/// <summary>
/// Message indicating that a node failed with an error.
/// Contains error details and partial execution context.
/// </summary>
public class NodeFailMessage : INodeMessage
{
    /// <inheritdoc/>
    public string NodeId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MessageType MessageType => MessageType.Fail;

    /// <inheritdoc/>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc/>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the node instance ID that failed.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the node execution context (may contain partial output).
    /// </summary>
    public NodeExecutionContext? NodeContext { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused the failure.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
