//-----------------------------------------------------------------------
// <copyright file="TaskType.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    /// <summary>
    /// Defines the type of progress task.
    /// </summary>
    public enum TaskType
    {
        /// <summary>
        /// A job that can have child tasks or stages.
        /// </summary>
        Job,

        /// <summary>
        /// A step within a job (leaf node).
        /// </summary>
        Step
    }
}
