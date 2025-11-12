// -----------------------------------------------------------------------
// <copyright file="NodeExecutionContext.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Contexts;

using System.Collections.Concurrent;

/// <summary>
/// Represents the execution context for a single node instance.
/// Contains input data from previous nodes, local variables, and output data for downstream nodes.
/// </summary>
public class NodeExecutionContext
{
    /// <summary>
    /// Gets the input data passed from upstream nodes.
    /// This is populated from the OutputData of the previous node.
    /// </summary>
    public Dictionary<string, object> InputData { get; set; } = new();

    /// <summary>
    /// Gets the output data that will be passed to downstream nodes.
    /// Nodes populate this during execution.
    /// </summary>
    public Dictionary<string, object> OutputData { get; set; } = new();

    /// <summary>
    /// Gets the local variables for this node execution.
    /// These are not passed to downstream nodes, used for internal computation.
    /// </summary>
    public ConcurrentDictionary<string, object> LocalVariables { get; } = new();

    /// <summary>
    /// Gets metadata about this node execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();
}
