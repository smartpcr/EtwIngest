// -----------------------------------------------------------------------
// <copyright file="NodeProgressEvent.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Events;

/// <summary>
/// Event published for node progress updates during execution.
/// </summary>
public class NodeProgressEvent : NodeEvent
{
    /// <summary>
    /// Gets or sets the unique identifier for this specific node execution instance.
    /// </summary>
    public Guid NodeInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the current status message.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
