// -----------------------------------------------------------------------
// <copyright file="MessageType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums;

/// <summary>
/// Represents the type of message produced by a node.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Message indicating node completed successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// Message indicating node failed with an error.
    /// </summary>
    Fail,

    /// <summary>
    /// Message indicating node progress update.
    /// </summary>
    Progress,

    /// <summary>
    /// Message indicating loop node produced next iteration output.
    /// Used by ForEach/While nodes to stream results per iteration.
    /// Loop nodes emit both Next (per iteration) and Complete (at end).
    /// Connections with TriggerMessageType=Next process each item as it arrives.
    /// Connections with TriggerMessageType=Complete aggregate after all iterations.
    /// </summary>
    Next,

    /// <summary>
    /// Message indicating node was cancelled.
    /// Triggers cascade cancellation: downstream nodes receive OnCancel and cancel recursively.
    /// When a node fails or is cancelled, all pending outbound messages are cleared (moved to DLQ)
    /// and OnCancel is sent to all downstream nodes.
    /// </summary>
    Cancel
}
