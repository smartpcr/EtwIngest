// -----------------------------------------------------------------------
// <copyright file="INodeMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Enums;

/// <summary>
/// Base interface for all node messages.
/// Messages are produced by nodes during execution and routed to downstream nodes.
/// </summary>
public interface INodeMessage
{
    /// <summary>
    /// Gets the ID of the node that produced this message.
    /// </summary>
    string NodeId { get; }

    /// <summary>
    /// Gets the type of message.
    /// </summary>
    MessageType MessageType { get; }

    /// <summary>
    /// Gets the timestamp when the message was created.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    Guid MessageId { get; }
}
