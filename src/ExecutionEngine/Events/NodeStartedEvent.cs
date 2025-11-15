// -----------------------------------------------------------------------
// <copyright file="NodeStartedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a node execution starts.
/// </summary>
public class NodeStartedEvent : NodeEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific node execution instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }
}
