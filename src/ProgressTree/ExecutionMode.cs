//-----------------------------------------------------------------------
// <copyright file="ExecutionMode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    /// <summary>
    /// Defines how child tasks are executed.
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>
        /// Tasks execute one after another.
        /// </summary>
        Sequential,

        /// <summary>
        /// Tasks execute simultaneously.
        /// </summary>
        Parallel
    }
}
