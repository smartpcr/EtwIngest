// -----------------------------------------------------------------------
// <copyright file="NodeEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Base class for all node-related events.
/// Inherits from WorkflowEvent to maintain workflow context.
/// </summary>
public abstract class NodeEvent : WorkflowEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the node definition.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the node.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;
}
