// -----------------------------------------------------------------------
// <copyright file="NodeFailedEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published when a node execution fails.
/// </summary>
public class NodeFailedEvent : NodeEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific node execution instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the error message describing why the node failed.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception that caused the node failure.
    /// </summary>
    public Exception? Exception { get; set; }
}
