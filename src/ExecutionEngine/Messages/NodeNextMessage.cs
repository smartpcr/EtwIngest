// -----------------------------------------------------------------------
// <copyright file="NodeNextMessage.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Messages;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;

/// <summary>
/// Message indicating that a loop node produced next iteration output.
/// Used by ForEach/While nodes to stream results per iteration to downstream nodes.
/// Allows downstream nodes to process each iteration as it happens, not just at loop completion.
///
/// Loop nodes emit TWO message types:
/// - OnNext (this message): Emitted for EACH iteration to stream data item-by-item
/// - OnComplete (NodeCompleteMessage): Emitted ONCE when all iterations finish
///
/// Example: ForEach with 10 items emits 10 Next messages (one per item) + 1 Complete message (at end)
/// Downstream nodes can connect via Next (process each item) or Complete (aggregate after all items)
/// </summary>
public class NodeNextMessage : INodeMessage
{
    /// <inheritdoc/>
    public string NodeId { get; set; } = string.Empty;

    /// <inheritdoc/>
    public MessageType MessageType => MessageType.Next;

    /// <inheritdoc/>
    public DateTime Timestamp { get; set; }

    /// <inheritdoc/>
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the node instance ID producing this iteration.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the iteration index (0-based).
    /// For ForEach: index in the collection.
    /// For While: iteration count.
    /// </summary>
    public int IterationIndex { get; set; }

    /// <summary>
    /// Gets or sets the iteration context containing outputs from this iteration.
    /// </summary>
    public NodeExecutionContext? IterationContext { get; set; }

    /// <summary>
    /// Gets or sets optional metadata about this iteration.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
