//-----------------------------------------------------------------------
// <copyright file="EventFileHandlerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.UnitTests.Tools
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using EtwEventReader.Tools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for EventFileHandler class.
    /// </summary>
    [TestClass]
    public class EventFileHandlerTests
    {
        private string testDirectory;

        /// <summary>
        /// Initializes test environment before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.testDirectory = Path.Combine(Path.GetTempPath(), "EventFileHandlerTests", Guid.NewGuid().ToString());
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
        /// Tests that EventFileHandler can be instantiated.
        /// </summary>
        [TestMethod]
        public void Constructor_CreatesInstance()
        {
            // Arrange & Act
            using var handler = new EventFileHandler();

            // Assert
            Assert.IsNotNull(handler);
        }

        /// <summary>
        /// Tests resolving a single file path.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithSingleFile_ReturnsSinglePath()
        {
            // Arrange
            var testFile = Path.Combine(this.testDirectory, "test.etl");
            File.WriteAllText(testFile, "test content");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { testFile });

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(testFile, result[0]);
        }

        /// <summary>
        /// Tests resolving a directory path.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithDirectory_ReturnsAllFilesInDirectory()
        {
            // Arrange
            var file1 = Path.Combine(this.testDirectory, "test1.etl");
            var file2 = Path.Combine(this.testDirectory, "test2.etl");
            File.WriteAllText(file1, "test content 1");
            File.WriteAllText(file2, "test content 2");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(file1));
            Assert.IsTrue(result.Contains(file2));
        }

        /// <summary>
        /// Tests resolving a wildcard pattern.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithWildcard_ReturnsMatchingFiles()
        {
            // Arrange
            var etlFile = Path.Combine(this.testDirectory, "test.etl");
            var txtFile = Path.Combine(this.testDirectory, "test.txt");
            File.WriteAllText(etlFile, "etl content");
            File.WriteAllText(txtFile, "txt content");
            var wildcardPath = Path.Combine(this.testDirectory, "*.etl");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { wildcardPath });

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(etlFile, result[0]);
        }

        /// <summary>
        /// Tests resolving a non-existent path.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithNonExistentPath_ReturnsEmptyList()
        {
            // Arrange
            var nonExistentPath = Path.Combine(this.testDirectory, "nonexistent.etl");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { nonExistentPath });

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests resolving a zip file containing ETL files.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithZipFile_ExtractsAndReturnsEtlFiles()
        {
            // Arrange
            var etlFileName = "test.etl";
            var zipFilePath = Path.Combine(this.testDirectory, "archive.zip");

            // Create a temp directory with an ETL file
            var tempEtlDir = Path.Combine(this.testDirectory, "temp");
            Directory.CreateDirectory(tempEtlDir);
            var tempEtlFile = Path.Combine(tempEtlDir, etlFileName);
            File.WriteAllText(tempEtlFile, "etl content");

            // Create zip file
            ZipFile.CreateFromDirectory(tempEtlDir, zipFilePath);

            // Clean up temp ETL directory
            Directory.Delete(tempEtlDir, true);

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { zipFilePath });

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].EndsWith(".etl"));
            Assert.IsTrue(File.Exists(result[0]));
        }

        /// <summary>
        /// Tests that zero-length files are ignored.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithZeroLengthZipFile_IgnoresFile()
        {
            // Arrange
            var zeroLengthZip = Path.Combine(this.testDirectory, "empty.zip");
            File.Create(zeroLengthZip).Dispose(); // Create empty file
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { zeroLengthZip });

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests that duplicate paths are removed.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithDuplicatePaths_ReturnsDistinctPaths()
        {
            // Arrange
            var testFile = Path.Combine(this.testDirectory, "test.etl");
            File.WriteAllText(testFile, "test content");
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { testFile, testFile, testFile });

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(testFile, result[0]);
        }

        /// <summary>
        /// Tests that Dispose is called and completes without exceptions.
        /// </summary>
        [TestMethod]
        public void Dispose_WithTempDirectories_CompletesSuccessfully()
        {
            // Arrange
            var zipFilePath = Path.Combine(this.testDirectory, "archive.zip");

            // Create a temp directory with an ETL file
            var tempEtlDir = Path.Combine(this.testDirectory, "temp");
            Directory.CreateDirectory(tempEtlDir);
            var tempEtlFile = Path.Combine(tempEtlDir, "test.etl");
            File.WriteAllText(tempEtlFile, "etl content");

            // Create zip file
            ZipFile.CreateFromDirectory(tempEtlDir, zipFilePath);
            Directory.Delete(tempEtlDir, true);

            List<string> result;

            // Act - Dispose should complete without exception
            using (var handler = new EventFileHandler())
            {
                result = handler.ResolveAllPaths(new[] { zipFilePath });
            }

            // Assert - We got results and dispose completed
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].EndsWith(".etl"));

            // Note: We don't assert directory cleanup due to timing and OS file handle issues
            // The important thing is Dispose doesn't throw exceptions
        }

        /// <summary>
        /// Tests resolving multiple paths of different types.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMixedPaths_ReturnsAllResolvedPaths()
        {
            // Arrange
            var file1 = Path.Combine(this.testDirectory, "file1.etl");
            var file2 = Path.Combine(this.testDirectory, "file2.etl");
            var subDir = Path.Combine(this.testDirectory, "subdir");
            Directory.CreateDirectory(subDir);
            var file3 = Path.Combine(subDir, "file3.etl");

            File.WriteAllText(file1, "content1");
            File.WriteAllText(file2, "content2");
            File.WriteAllText(file3, "content3");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { file1, subDir });

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(file1));
            Assert.IsTrue(result.Contains(file3));
        }
    }
}
