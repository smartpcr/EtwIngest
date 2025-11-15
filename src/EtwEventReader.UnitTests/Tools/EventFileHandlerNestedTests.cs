//-----------------------------------------------------------------------
// <copyright file="EventFileHandlerNestedTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.UnitTests.Tools
{
    using System;
    using System.IO;
    using System.Linq;
    using EtwEventReader.Tools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for EventFileHandler nested directory handling.
    /// </summary>
    [TestClass]
    public class EventFileHandlerNestedTests
    {
        private string testDirectory = null!;

        /// <summary>
        /// Initializes test environment before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.testDirectory = Path.Combine(Path.GetTempPath(), "EventFileHandlerNestedTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.testDirectory);
        }

        /// <summary>
        /// Cleans up test environment after each test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.testDirectory))
            {
                try
                {
                    Directory.Delete(this.testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Tests resolving nested directories recursively.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithNestedDirectories_ReturnsAllNestedFiles()
        {
            // Arrange
            var file1 = Path.Combine(this.testDirectory, "file1.etl");
            var subDir1 = Path.Combine(this.testDirectory, "sub1");
            var subDir2 = Path.Combine(subDir1, "sub2");

            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);

            var file2 = Path.Combine(subDir1, "file2.etl");
            var file3 = Path.Combine(subDir2, "file3.etl");
            var file4 = Path.Combine(subDir2, "file4.txt"); // Non-ETL file - will be excluded

            File.WriteAllText(file1, "root level");
            File.WriteAllText(file2, "level 1");
            File.WriteAllText(file3, "level 2");
            File.WriteAllText(file4, "text file");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.AreEqual(3, result.Count, "Should find 3 ETL files recursively, excluding non-ETL files");
            Assert.IsTrue(result.Contains(file1), "Should contain root level file");
            Assert.IsTrue(result.Contains(file2), "Should contain level 1 file");
            Assert.IsTrue(result.Contains(file3), "Should contain level 2 file");
            Assert.IsFalse(result.Contains(file4), "Should not contain text file");
        }

        /// <summary>
        /// Tests wildcard pattern with nested directories.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithWildcardInNestedDirs_ReturnsMatchingFilesRecursively()
        {
            // Arrange
            var file1 = Path.Combine(this.testDirectory, "file1.etl");
            var subDir1 = Path.Combine(this.testDirectory, "sub1");
            var subDir2 = Path.Combine(subDir1, "sub2");

            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);

            var file2 = Path.Combine(subDir1, "file2.etl");
            var file3 = Path.Combine(subDir2, "file3.etl");
            var file4 = Path.Combine(subDir2, "file4.txt"); // Should not match

            File.WriteAllText(file1, "root level");
            File.WriteAllText(file2, "level 1");
            File.WriteAllText(file3, "level 2");
            File.WriteAllText(file4, "text file");

            var wildcardPath = Path.Combine(this.testDirectory, "*.etl");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { wildcardPath });

            // Assert
            Assert.AreEqual(3, result.Count, "Should find 3 ETL files recursively");
            Assert.IsTrue(result.Contains(file1), "Should contain root level ETL");
            Assert.IsTrue(result.Contains(file2), "Should contain level 1 ETL");
            Assert.IsTrue(result.Contains(file3), "Should contain level 2 ETL");
            Assert.IsFalse(result.Contains(file4), "Should not contain text file");
        }

        /// <summary>
        /// Tests deeply nested directory structure.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithDeeplyNestedStructure_FindsAllFiles()
        {
            // Arrange - Create 5 levels deep
            var currentDir = this.testDirectory;
            for (var i = 1; i <= 5; i++)
            {
                currentDir = Path.Combine(currentDir, $"level{i}");
                Directory.CreateDirectory(currentDir);
                var file = Path.Combine(currentDir, $"file{i}.etl");
                File.WriteAllText(file, $"content at level {i}");
            }

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.AreEqual(5, result.Count, "Should find all 5 files in nested structure");

            // Verify each level's file is found
            for (var i = 1; i <= 5; i++)
            {
                var expectedPath = Path.Combine(this.testDirectory, string.Join(Path.DirectorySeparatorChar.ToString(),
                    Enumerable.Range(1, i).Select(n => $"level{n}")), $"file{i}.etl");
                Assert.IsTrue(result.Any(r => r.EndsWith($"file{i}.etl")),
                    $"Should find file at level {i}");
            }
        }

        /// <summary>
        /// Tests nested directories with mixed content.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMixedNestedContent_ReturnsAllFiles()
        {
            // Arrange
            var rootFile = Path.Combine(this.testDirectory, "root.etl");
            var subDir = Path.Combine(this.testDirectory, "logs");
            var subSubDir1 = Path.Combine(subDir, "2024");
            var subSubDir2 = Path.Combine(subDir, "2025");

            Directory.CreateDirectory(subDir);
            Directory.CreateDirectory(subSubDir1);
            Directory.CreateDirectory(subSubDir2);

            File.WriteAllText(rootFile, "root");
            File.WriteAllText(Path.Combine(subDir, "log1.etl"), "log1");
            File.WriteAllText(Path.Combine(subDir, "readme.txt"), "readme"); // Will be excluded
            File.WriteAllText(Path.Combine(subSubDir1, "old.etl"), "old");
            File.WriteAllText(Path.Combine(subSubDir2, "new.etl"), "new");
            File.WriteAllText(Path.Combine(subSubDir2, "data.zip"), "zip"); // Will trigger extraction but is invalid

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            // Should find 4 ETL files (root.etl, log1.etl, old.etl, new.etl)
            // readme.txt is excluded (not allowed extension)
            // data.zip is excluded (corrupted/invalid ZIP, no ETL files extracted)
            Assert.AreEqual(4, result.Count, "Should find 4 ETL files, excluding txt and invalid zip");

            var etlFiles = result.Where(f => f.EndsWith(".etl")).ToList();
            Assert.AreEqual(4, etlFiles.Count, "Should find 4 ETL files");
        }

        /// <summary>
        /// Tests that empty nested directories don't cause issues.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithEmptyNestedDirectories_HandlesGracefully()
        {
            // Arrange
            var file1 = Path.Combine(this.testDirectory, "file1.etl");
            var emptyDir1 = Path.Combine(this.testDirectory, "empty1");
            var emptyDir2 = Path.Combine(emptyDir1, "empty2");
            var subDirWithFile = Path.Combine(this.testDirectory, "withfile");

            Directory.CreateDirectory(emptyDir1);
            Directory.CreateDirectory(emptyDir2);
            Directory.CreateDirectory(subDirWithFile);

            File.WriteAllText(file1, "content");
            File.WriteAllText(Path.Combine(subDirWithFile, "file2.etl"), "content2");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.AreEqual(2, result.Count, "Should find 2 files, ignoring empty directories");
        }

        /// <summary>
        /// Tests recursive wildcard with specific extension.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithExtensionWildcardRecursive_ReturnsOnlyMatchingExtension()
        {
            // Arrange
            var subDir = Path.Combine(this.testDirectory, "data");
            Directory.CreateDirectory(subDir);

            File.WriteAllText(Path.Combine(this.testDirectory, "file1.etl"), "1");
            File.WriteAllText(Path.Combine(this.testDirectory, "file2.log"), "2");
            File.WriteAllText(Path.Combine(subDir, "file3.etl"), "3");
            File.WriteAllText(Path.Combine(subDir, "file4.txt"), "4");

            var wildcardPath = Path.Combine(this.testDirectory, "*.etl");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { wildcardPath });

            // Assert
            Assert.AreEqual(2, result.Count, "Should find 2 ETL files recursively");
            Assert.IsTrue(result.All(f => f.EndsWith(".etl")), "All files should be ETL files");
        }

        /// <summary>
        /// Tests multiple directory paths with nested structures.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMultipleNestedDirectories_CombinesAllResults()
        {
            // Arrange
            var dir1 = Path.Combine(this.testDirectory, "dir1");
            var dir2 = Path.Combine(this.testDirectory, "dir2");
            var subDir1 = Path.Combine(dir1, "sub");
            var subDir2 = Path.Combine(dir2, "sub");

            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);

            File.WriteAllText(Path.Combine(dir1, "file1.etl"), "1");
            File.WriteAllText(Path.Combine(subDir1, "file2.etl"), "2");
            File.WriteAllText(Path.Combine(dir2, "file3.etl"), "3");
            File.WriteAllText(Path.Combine(subDir2, "file4.etl"), "4");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { dir1, dir2 });

            // Assert
            Assert.AreEqual(4, result.Count, "Should find all 4 files from both directory trees");
        }
    }
}
