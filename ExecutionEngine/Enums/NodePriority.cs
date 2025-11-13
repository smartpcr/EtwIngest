// -----------------------------------------------------------------------
// <copyright file="NodePriority.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums
{
    /// <summary>
    /// Defines the priority level for node execution scheduling.
    /// Higher priority nodes are executed before lower priority nodes when
    /// workflow-level concurrency limits are reached.
    /// </summary>
    public enum NodePriority
    {
        /// <summary>
        /// Low priority - executed last when concurrency limits are reached.
        /// Suitable for background tasks, cleanup operations.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority - default priority level for most nodes.
        /// Standard execution order with fair scheduling.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - executed first when concurrency limits are reached.
        /// Suitable for critical operations, data fetching, blocking tasks.
        /// </summary>
        High = 2
    }
}
