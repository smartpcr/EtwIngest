// -----------------------------------------------------------------------
// <copyright file="NodeCancelledEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a node execution is cancelled.
/// </summary>
public class NodeCancelledEvent : NodeEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific node execution instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the reason for cancellation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
