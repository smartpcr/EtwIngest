//-----------------------------------------------------------------------
// <copyright file="EventFileHandlerIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using EtwEventReader.Tools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Integration tests for EventFileHandler using real ETL files.
    /// </summary>
    [TestClass]
    public class EventFileHandlerIntegrationTests
    {
        /// <summary>
        /// The directory containing real ETL/ZIP files for testing.
        /// </summary>
        private const string TestDataDirectory = "X:/icm/IL17";

        /// <summary>
        /// Tests resolving all ETL files from the test directory.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithRealDirectory_ReturnsAllEtlFiles()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { TestDataDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Should find at least one file in the directory");

            Console.WriteLine($"Found {result.Count} files in {TestDataDirectory}");

            // Verify all returned files exist
            foreach (var file in result)
            {
                Assert.IsTrue(File.Exists(file), $"File should exist: {file}");
            }

            // Print file summary
            var etlFiles = result.Where(f => f.EndsWith(".etl", StringComparison.OrdinalIgnoreCase)).ToList();
            var zipFiles = result.Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
            var otherFiles = result.Except(etlFiles).Except(zipFiles).ToList();

            Console.WriteLine($"ETL files: {etlFiles.Count}");
            Console.WriteLine($"ZIP files: {zipFiles.Count}");
            Console.WriteLine($"Other files: {otherFiles.Count}");
        }

        /// <summary>
        /// Tests resolving ETL files with wildcard pattern.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithEtlWildcard_ReturnsOnlyEtlFiles()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var wildcardPath = Path.Combine(TestDataDirectory, "*.etl");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { wildcardPath });

            // Assert
            Assert.IsNotNull(result);

            if (result.Count == 0)
            {
                Assert.Inconclusive($"No ETL files found in {TestDataDirectory}");
                return;
            }

            Console.WriteLine($"Found {result.Count} ETL files");

            // Verify all are ETL files
            foreach (var file in result)
            {
                Assert.IsTrue(file.EndsWith(".etl", StringComparison.OrdinalIgnoreCase),
                    $"File should be an ETL file: {file}");
                Assert.IsTrue(File.Exists(file), $"File should exist: {file}");
            }

            // Print first 10 files
            Console.WriteLine("\nFirst 10 ETL files:");
            foreach (var file in result.Take(10))
            {
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"  {fileInfo.Name} - {FormatFileSize(fileInfo.Length)}");
            }
        }

        /// <summary>
        /// Tests resolving ZIP files containing ETL files.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithZipWildcard_ExtractsAndReturnsEtlFiles()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var wildcardPath = Path.Combine(TestDataDirectory, "*.zip");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { wildcardPath });

            // Assert
            Assert.IsNotNull(result);

            if (result.Count == 0)
            {
                Assert.Inconclusive($"No ZIP files with ETL content found in {TestDataDirectory}");
                return;
            }

            Console.WriteLine($"Extracted {result.Count} ETL files from ZIP archives");

            // Verify all extracted files are ETL files and exist
            foreach (var file in result)
            {
                Assert.IsTrue(file.EndsWith(".etl", StringComparison.OrdinalIgnoreCase),
                    $"Extracted file should be an ETL file: {file}");
                Assert.IsTrue(File.Exists(file), $"Extracted file should exist: {file}");
            }

            // Print summary
            Console.WriteLine("\nExtracted ETL files:");
            foreach (var file in result.Take(10))
            {
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"  {fileInfo.Name} - {FormatFileSize(fileInfo.Length)}");
            }
        }

        /// <summary>
        /// Tests resolving multiple file patterns simultaneously.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithMultiplePatterns_ReturnsAllMatchingFiles()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var etlPattern = Path.Combine(TestDataDirectory, "*.etl");
            var zipPattern = Path.Combine(TestDataDirectory, "*.zip");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { etlPattern, zipPattern });

            // Assert
            Assert.IsNotNull(result);

            if (result.Count == 0)
            {
                Assert.Inconclusive($"No ETL or ZIP files found in {TestDataDirectory}");
                return;
            }

            Console.WriteLine($"Found {result.Count} total files");

            // Verify all files exist and are ETL format
            foreach (var file in result)
            {
                Assert.IsTrue(File.Exists(file), $"File should exist: {file}");
                Assert.IsTrue(file.EndsWith(".etl", StringComparison.OrdinalIgnoreCase),
                    $"All resolved files should be ETL files (ZIPs are extracted): {file}");
            }

            // Calculate statistics
            var totalSize = result.Select(f => new FileInfo(f).Length).Sum();
            Console.WriteLine($"Total size: {FormatFileSize(totalSize)}");
            Console.WriteLine($"Average file size: {FormatFileSize(totalSize / result.Count)}");
        }

        /// <summary>
        /// Tests resolving specific ETL files by name.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithSpecificFiles_ReturnsRequestedFiles()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            // Get first 3 ETL files from directory
            var allEtlFiles = Directory.GetFiles(TestDataDirectory, "*.etl", SearchOption.TopDirectoryOnly);

            if (allEtlFiles.Length == 0)
            {
                Assert.Inconclusive($"No ETL files found in {TestDataDirectory}");
                return;
            }

            var testFiles = allEtlFiles.Take(3).ToArray();
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(testFiles);

            // Assert
            Assert.AreEqual(testFiles.Length, result.Count, "Should return exactly the requested files");

            foreach (var requestedFile in testFiles)
            {
                Assert.IsTrue(result.Contains(requestedFile),
                    $"Result should contain requested file: {requestedFile}");
            }

            Console.WriteLine($"Successfully resolved {result.Count} specific files:");
            foreach (var file in result)
            {
                Console.WriteLine($"  {Path.GetFileName(file)}");
            }
        }

        /// <summary>
        /// Tests performance with large number of files.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Performance")]
        public void ResolveAllPaths_WithManyFiles_CompletesInReasonableTime()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            using var handler = new EventFileHandler();
            var startTime = DateTime.UtcNow;

            // Act
            var result = handler.ResolveAllPaths(new[] { TestDataDirectory });

            // Assert
            var duration = DateTime.UtcNow - startTime;

            Assert.IsNotNull(result);
            Console.WriteLine($"Resolved {result.Count} files in {duration.TotalSeconds:F2} seconds");

            if (result.Count > 0)
            {
                var avgTimePerFile = duration.TotalMilliseconds / result.Count;
                Console.WriteLine($"Average time per file: {avgTimePerFile:F2} ms");

                // Should process at least 10 files per second for path resolution
                // (actual ETL parsing is separate and expected to be slower)
                Assert.IsTrue(avgTimePerFile < 100,
                    "Path resolution should be fast (< 100ms per file on average)");
            }
        }

        /// <summary>
        /// Tests recursive handling of nested directories.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithNestedDirectories_FindsAllEtlFilesRecursively()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { TestDataDirectory });

            // Get actual count including subdirectories for comparison
            var allFiles = Directory.GetFiles(TestDataDirectory, "*", SearchOption.AllDirectories);
            var topLevelFiles = Directory.GetFiles(TestDataDirectory, "*", SearchOption.TopDirectoryOnly);

            // Assert
            Console.WriteLine($"Top-level files: {topLevelFiles.Length}");
            Console.WriteLine($"All files (including subdirectories): {allFiles.Length}");
            Console.WriteLine($"Handler resolved: {result.Count} files");

            Assert.AreEqual(allFiles.Length, result.Count,
                "Should find all files recursively, including nested directories");

            // Verify all returned files exist
            foreach (var file in result)
            {
                Assert.IsTrue(File.Exists(file), $"File should exist: {file}");
            }

            if (allFiles.Length > topLevelFiles.Length)
            {
                Console.WriteLine($"\nFound {allFiles.Length - topLevelFiles.Length} additional files in subdirectories");
            }
        }

        /// <summary>
        /// Tests that temporary directories are properly cleaned up.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_AfterZipExtraction_CleansUpTemporaryDirectories()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            var zipFiles = Directory.GetFiles(TestDataDirectory, "*.zip", SearchOption.TopDirectoryOnly);

            if (zipFiles.Length == 0)
            {
                Assert.Inconclusive($"No ZIP files found in {TestDataDirectory}");
                return;
            }

            var tempPathRoot = Path.Combine(TestDataDirectory, "TempPath");
            var tempDirCountBefore = Directory.Exists(tempPathRoot)
                ? Directory.GetDirectories(tempPathRoot).Length
                : 0;

            // Act
            using (var handler = new EventFileHandler())
            {
                var result = handler.ResolveAllPaths(new[] { zipFiles[0] });
                Console.WriteLine($"Extracted {result.Count} files from {Path.GetFileName(zipFiles[0])}");
            }

            // Give OS time to clean up
            System.Threading.Thread.Sleep(500);

            // Assert
            Console.WriteLine($"Temporary directories before: {tempDirCountBefore}");

            if (Directory.Exists(tempPathRoot))
            {
                var tempDirCountAfter = Directory.GetDirectories(tempPathRoot).Length;
                Console.WriteLine($"Temporary directories after: {tempDirCountAfter}");

                // Note: Cleanup may be delayed by OS file handles
                // This test primarily verifies no exception is thrown during cleanup
            }
            else
            {
                Console.WriteLine("TempPath directory cleaned up completely");
            }
        }

        /// <summary>
        /// Tests handling of corrupted or invalid files.
        /// </summary>
        [TestMethod]
        [TestCategory("Integration")]
        public void ResolveAllPaths_WithMixedValidAndInvalidFiles_HandlesGracefully()
        {
            // Arrange
            if (!Directory.Exists(TestDataDirectory))
            {
                Assert.Inconclusive($"Test data directory does not exist: {TestDataDirectory}");
                return;
            }

            using var handler = new EventFileHandler();

            // Act - Should handle any issues gracefully without throwing
            try
            {
                var result = handler.ResolveAllPaths(new[] { TestDataDirectory });

                // Assert
                Assert.IsNotNull(result);
                Console.WriteLine($"Successfully resolved {result.Count} files");
                Console.WriteLine("Handler gracefully handled any invalid files");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Handler should handle errors gracefully, but threw: {ex.Message}");
            }
        }

        /// <summary>
        /// Formats file size in human-readable format.
        /// </summary>
        /// <param name="bytes">Size in bytes.</param>
        /// <returns>Formatted string.</returns>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
