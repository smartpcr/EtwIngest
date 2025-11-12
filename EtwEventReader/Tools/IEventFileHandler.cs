//-----------------------------------------------------------------------
// <copyright file="IEventFileHandler.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interface for handling event file operations including path resolution and zip extraction.
    /// </summary>
    public interface IEventFileHandler : IDisposable
    {
        /// <summary>
        /// Resolves all paths from the Path parameter, handling wildcards, directories, and zip files.
        /// </summary>
        /// <param name="paths">Array of file or directory paths.</param>
        /// <returns>List of resolved ETL file paths.</returns>
        List<string> ResolveAllPaths(string[] paths);
    }
}
