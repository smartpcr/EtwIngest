//-----------------------------------------------------------------------
// <copyright file="IProgressTreeManager.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages a hierarchical progress tree with automatic parent progress calculation.
    /// </summary>
    public interface IProgressTreeMonitor
    {
        /// <summary>
        /// Starts the progress tracking session.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAsync(string name, Action<IProgressNode> buildAction);
    }
}
