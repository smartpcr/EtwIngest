// -----------------------------------------------------------------------
// <copyright file="CompensationContext.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Contexts
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides context information for compensation (undo) operations.
    /// Used in the Saga pattern to rollback completed operations when a workflow fails.
    /// </summary>
    public class CompensationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompensationContext"/> class.
        /// </summary>
        /// <param name="failedNodeId">The ID of the node that failed.</param>
        /// <param name="failureReason">The exception that caused the failure.</param>
        /// <param name="failedNodeOutput">The output data from the failed node (if any).</param>
        public CompensationContext(string failedNodeId, Exception failureReason, object? failedNodeOutput = null)
        {
            this.FailedNodeId = failedNodeId ?? throw new ArgumentNullException(nameof(failedNodeId));
            this.FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason));
            this.FailedNodeOutput = failedNodeOutput;
            this.NodesToCompensate = new List<string>();
            this.FailureTimestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the ID of the node that failed and triggered compensation.
        /// </summary>
        public string FailedNodeId { get; }

        /// <summary>
        /// Gets the exception that caused the workflow to fail.
        /// </summary>
        public Exception FailureReason { get; }

        /// <summary>
        /// Gets the output data from the failed node, if any.
        /// May be null if the node failed before producing output.
        /// </summary>
        public object? FailedNodeOutput { get; }

        /// <summary>
        /// Gets the timestamp when the failure occurred.
        /// </summary>
        public DateTime FailureTimestamp { get; }

        /// <summary>
        /// Gets the list of node IDs that need to be compensated.
        /// Populated in reverse order of completion.
        /// </summary>
        public List<string> NodesToCompensate { get; }

        /// <summary>
        /// Gets or sets a value indicating whether compensation should be partial.
        /// If true, only nodes in the failed branch are compensated.
        /// If false, all completed nodes in the workflow are compensated.
        /// </summary>
        public bool PartialCompensation { get; set; }

        /// <summary>
        /// Gets or sets optional metadata for the compensation operation.
        /// Can be used to pass additional context to compensation nodes.
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Adds a node to the compensation list.
        /// </summary>
        /// <param name="nodeId">The node ID to compensate.</param>
        public void AddNodeToCompensate(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            // Insert at the beginning to maintain reverse order
            this.NodesToCompensate.Insert(0, nodeId);
        }
    }
}
