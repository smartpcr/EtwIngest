//-----------------------------------------------------------------------
// <copyright file="EventFileHandler.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;

    /// <summary>
    /// Handles event file operations including path resolution and zip extraction.
    /// </summary>
    public class EventFileHandler : IEventFileHandler
    {
        /// <summary>
        /// Default allowed file extensions for event files.
        /// </summary>
        private static readonly string[] DefaultAllowedExtensions = new[] { ".zip", ".etl", ".evtx" };

        /// <summary>
        /// Allowed file extensions for event files.
        /// </summary>
        private readonly string[] allowedExtensions;

        /// <summary>
        /// List of temporary paths created during processing.
        /// </summary>
        private readonly List<string> tempPaths = new List<string>();

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventFileHandler"/> class with default allowed extensions.
        /// </summary>
        public EventFileHandler()
            : this(DefaultAllowedExtensions)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventFileHandler"/> class with custom allowed extensions.
        /// </summary>
        /// <param name="allowedExtensions">Array of allowed file extensions (e.g., ".etl", ".evtx"). Must include ".zip" if ZIP extraction is desired.</param>
        public EventFileHandler(string[] allowedExtensions)
        {
            if (allowedExtensions == null || allowedExtensions.Length == 0)
            {
                throw new ArgumentException("At least one allowed extension must be specified", nameof(allowedExtensions));
            }

            this.allowedExtensions = allowedExtensions;
        }

        /// <summary>
        /// Resolves all paths from the Path parameter, handling wildcards, directories, and zip files.
        /// Filters files by allowed extensions (.zip, .etl, .evtx) and excludes zero-length files.
        /// </summary>
        /// <param name="paths">Array of file or directory paths.</param>
        /// <returns>List of resolved event file paths.</returns>
        public List<string> ResolveAllPaths(string[] paths)
        {
            var resolvedPaths = new List<string>();
            var zeroLengthFiles = new List<string>();

            for (int i = 0; i < paths.Length; i++)
            {
                string tempZipExtractionDirName = string.Empty;
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

                try
                {
                    // Handle wildcards and zip files
                    if (paths[i].Contains("*") || paths[i].EndsWith(".zip"))
                    {
                        var directory = Path.GetDirectoryName(paths[i]) ?? Directory.GetCurrentDirectory();
                        var searchPattern = Path.GetFileName(paths[i]);
                        tempZipExtractionDirName = Path.Combine(directory, "TempPath", timestamp);

                        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            resolvedPaths.Add(file);
                        }
                    }
                    else if (Directory.Exists(paths[i]))
                    {
                        tempZipExtractionDirName = Path.Combine(paths[i], "TempPath", timestamp);
                        resolvedPaths.AddRange(Directory.GetFiles(paths[i], "*", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(paths[i]))
                    {
                        resolvedPaths.Add(paths[i]);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Path not found: {paths[i]}");
                        continue;
                    }

                    // Process zip files
                    var zipFiles = resolvedPaths.Where(f => f.EndsWith(".zip")).ToList();
                    foreach (var zipFile in zipFiles)
                    {
                        FileInfo fileInfo = new FileInfo(zipFile);

                        if (fileInfo.Length > 0)
                        {
                            if (!Directory.Exists(tempZipExtractionDirName))
                            {
                                Directory.CreateDirectory(tempZipExtractionDirName);
                                this.tempPaths.Add(tempZipExtractionDirName);
                            }

                            this.ExtractFileToDirectory(zipFile, tempZipExtractionDirName);
                        }
                        else
                        {
                            zeroLengthFiles.Add(zipFile);
                            Console.WriteLine($"Warning: Ignoring zero length file: {zipFile}");
                        }
                    }

                    if (!string.IsNullOrEmpty(tempZipExtractionDirName) && Directory.Exists(tempZipExtractionDirName))
                    {
                        // Extract both .etl and .evtx files from ZIP
                        var etlFiles = Directory.GetFiles(tempZipExtractionDirName, "*.etl", SearchOption.AllDirectories);
                        var evtxFiles = Directory.GetFiles(tempZipExtractionDirName, "*.evtx", SearchOption.AllDirectories);
                        resolvedPaths.AddRange(etlFiles);
                        resolvedPaths.AddRange(evtxFiles);
                    }

                    resolvedPaths.RemoveAll(x => zeroLengthFiles.Contains(x));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing path {paths[i]}: {ex.Message}");
                }
            }

            // Remove ZIP files from results (they're only for extraction, not final output)
            // This is done after the loop to ensure all ZIPs are removed even if extraction failed
            resolvedPaths.RemoveAll(x => x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            // Filter by allowed extensions and remove zero-length files
            var filteredPaths = resolvedPaths
                .Where(path => this.HasAllowedExtension(path))
                .Where(path => !this.IsZeroLengthFile(path))
                .Distinct()
                .ToList();

            return filteredPaths;
        }

        /// <summary>
        /// Checks if a file has an allowed extension.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>True if the file has an allowed extension, false otherwise.</returns>
        private bool HasAllowedExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return this.allowedExtensions.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a file is zero-length (empty).
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>True if the file is zero-length, false otherwise.</returns>
        private bool IsZeroLengthFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == 0)
                    {
                        Console.WriteLine($"Warning: Excluding zero-length file: {filePath}");
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                // If we can't check the file, assume it's not zero-length
                return false;
            }
        }

        /// <summary>
        /// Disposes resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources used by this instance.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.RemoveTempPaths();
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Extract Zip file to a directory.
        /// </summary>
        /// <param name="zipName">Zip File Name.</param>
        /// <param name="extractDirectory">Directory for extracting zip file.</param>
        private void ExtractFileToDirectory(string zipName, string extractDirectory)
        {
            using var zip = ZipFile.OpenRead(zipName);
            zip.ExtractToDirectory(extractDirectory);
        }

        /// <summary>
        /// Remove the temporary directories created during processing.
        /// </summary>
        private void RemoveTempPaths()
        {
            try
            {
                if (this.tempPaths != null && this.tempPaths.Count != 0)
                {
                    this.tempPaths.ForEach(tempDir =>
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Failed to delete temp directory {tempDir}: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing temp directories: {ex.Message}");
            }
        }
    }
}
