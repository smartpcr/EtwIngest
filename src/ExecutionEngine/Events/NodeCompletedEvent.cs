// -----------------------------------------------------------------------
// <copyright file="NodeCompletedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a node execution completes successfully.
/// </summary>
public class NodeCompletedEvent : NodeEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific node execution instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the execution duration for this node.
    /// </summary>
    public TimeSpan Duration { get; set; }
}
