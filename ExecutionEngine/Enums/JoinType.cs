// -----------------------------------------------------------------------
// <copyright file="JoinType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Enums
{
    /// <summary>
    /// Defines how a node should be triggered when it has multiple inbound connections.
    /// </summary>
    public enum JoinType
    {
        /// <summary>
        /// Trigger when ANY upstream node completes (OR logic).
        /// Node executes immediately when the first message arrives.
        /// This is the default behavior for fan-in scenarios.
        /// Example: Aggregate results as they arrive.
        /// </summary>
        Any = 0,

        /// <summary>
        /// Trigger only when ALL upstream nodes complete (AND logic).
        /// Node waits until all upstream connections have sent a message.
        /// Implements a synchronization barrier.
        /// Example: Join operation waiting for all parallel branches to complete.
        /// </summary>
        All = 1
    }
}
