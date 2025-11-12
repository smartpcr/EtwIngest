//-----------------------------------------------------------------------
// <copyright file="EventFileHandlerEdgeCasesTests.cs" company="Microsoft Corp.">
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
    /// Unit tests for EventFileHandler edge cases and error scenarios.
    /// </summary>
    [TestClass]
    public class EventFileHandlerEdgeCasesTests
    {
        private string testDirectory;

        /// <summary>
        /// Initializes test environment before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.testDirectory = Path.Combine(Path.GetTempPath(), "EventFileHandlerEdgeCasesTests", Guid.NewGuid().ToString());
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
        /// Tests handling of corrupted ZIP file.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithCorruptedZipFile_HandlesGracefully()
        {
            // Arrange
            var corruptedZip = Path.Combine(this.testDirectory, "corrupted.zip");
            var validFile = Path.Combine(this.testDirectory, "valid.etl");

            // Create a file with .zip extension but invalid ZIP format
            File.WriteAllText(corruptedZip, "This is not a valid ZIP file content");
            File.WriteAllText(validFile, "valid content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains(validFile), "Should include valid file");
            // Corrupted ZIP should be handled gracefully (error logged but not throw exception)
        }

        /// <summary>
        /// Tests handling of empty but valid ZIP file.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithEmptyZipFile_ReturnsNoFiles()
        {
            // Arrange
            var emptyZipPath = Path.Combine(this.testDirectory, "empty.zip");
            var validFile = Path.Combine(this.testDirectory, "valid.etl");

            // Create a valid but empty ZIP file
            using (var zipArchive = ZipFile.Open(emptyZipPath, ZipArchiveMode.Create))
            {
                // Create empty archive
            }

            File.WriteAllText(validFile, "valid content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Should only find the valid ETL file");
            Assert.IsTrue(result.Contains(validFile), "Should include valid file");
            Assert.IsFalse(result.Any(f => f.Contains("empty.zip")), "Should not include empty zip");
        }

        /// <summary>
        /// Tests handling of ZIP file containing only non-ETL files.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithZipContainingNoEtlFiles_ReturnsNoFilesFromZip()
        {
            // Arrange
            var zipPath = Path.Combine(this.testDirectory, "no-etl.zip");

            // Create a temp directory with non-ETL files
            var tempDir = Path.Combine(this.testDirectory, "temp-zip-content");
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "readme");
            File.WriteAllText(Path.Combine(tempDir, "data.log"), "log data");

            // Create ZIP file
            ZipFile.CreateFromDirectory(tempDir, zipPath);

            // Clean up temp directory
            Directory.Delete(tempDir, true);

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should not return any ETL files");
        }

        /// <summary>
        /// Tests handling of empty directory.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithEmptyDirectory_ReturnsEmptyList()
        {
            // Arrange
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should return empty list for empty directory");
        }

        /// <summary>
        /// Tests handling of zero-length ETL file.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithZeroLengthEtlFile_ExcludesFile()
        {
            // Arrange
            var zeroLengthFile = Path.Combine(this.testDirectory, "empty.etl");
            var validFile = Path.Combine(this.testDirectory, "valid.etl");

            File.Create(zeroLengthFile).Dispose(); // Create empty file
            File.WriteAllText(validFile, "valid content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Should only include valid file, excluding zero-length file");
            Assert.IsFalse(result.Contains(zeroLengthFile), "Should exclude zero-length ETL file");
            Assert.IsTrue(result.Contains(validFile), "Should include valid file");
        }

        /// <summary>
        /// Tests handling of files with special characters in names.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithSpecialCharactersInFileName_HandlesCorrectly()
        {
            // Arrange
            var specialNameFile = Path.Combine(this.testDirectory, "file with spaces & special-chars.etl");
            File.WriteAllText(specialNameFile, "content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.Contains(specialNameFile), "Should handle special characters in filename");
        }

        /// <summary>
        /// Tests handling of very long file paths.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithLongFilePath_HandlesCorrectly()
        {
            // Arrange
            var longDirName = new string('a', 100);
            var longDir = Path.Combine(this.testDirectory, longDirName);

            try
            {
                Directory.CreateDirectory(longDir);
                var longFileName = Path.Combine(longDir, "file.etl");
                File.WriteAllText(longFileName, "content");

                using var handler = new EventFileHandler();

                // Act
                var result = handler.ResolveAllPaths(new[] { this.testDirectory });

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Count);
                Assert.IsTrue(result[0].Contains(longDirName), "Should handle long paths");
            }
            catch (PathTooLongException)
            {
                Assert.Inconclusive("Path too long for this system");
            }
        }

        /// <summary>
        /// Tests handling of mixed valid and invalid paths.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMixedValidAndInvalidPaths_ProcessesValidPaths()
        {
            // Arrange
            var validFile = Path.Combine(this.testDirectory, "valid.etl");
            var nonExistentPath = Path.Combine(this.testDirectory, "nonexistent", "file.etl");
            var invalidPath = "Z:\\this\\does\\not\\exist\\file.etl";

            File.WriteAllText(validFile, "content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { validFile, nonExistentPath, invalidPath });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Should only return valid file");
            Assert.AreEqual(validFile, result[0]);
        }

        /// <summary>
        /// Tests handling of file with no extension.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithFileNoExtension_ExcludesFile()
        {
            // Arrange
            var noExtFile = Path.Combine(this.testDirectory, "noextension");
            var etlFile = Path.Combine(this.testDirectory, "with.etl");

            File.WriteAllText(noExtFile, "content");
            File.WriteAllText(etlFile, "etl content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count, "Should only include ETL file, excluding file without extension");
            Assert.IsFalse(result.Contains(noExtFile), "Should exclude file without extension");
            Assert.IsTrue(result.Contains(etlFile), "Should include ETL file");
        }

        /// <summary>
        /// Tests handling of multiple extensions.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMultipleExtensions_IncludesFile()
        {
            // Arrange
            var multiExtFile = Path.Combine(this.testDirectory, "file.backup.etl");
            File.WriteAllText(multiExtFile, "content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].EndsWith(".etl"), "Should include file with multiple extensions");
        }

        /// <summary>
        /// Tests handling of case sensitivity in file extensions.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithMixedCaseExtensions_IncludesAllFiles()
        {
            // Arrange
            var lowerCase = Path.Combine(this.testDirectory, "lower.etl");
            var upperCase = Path.Combine(this.testDirectory, "UPPER.ETL");
            var mixedCase = Path.Combine(this.testDirectory, "Mixed.Etl");

            File.WriteAllText(lowerCase, "content1");
            File.WriteAllText(upperCase, "content2");
            File.WriteAllText(mixedCase, "content3");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count, "Should include files regardless of extension case");
        }

        /// <summary>
        /// Tests handling of read-only files.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithReadOnlyFile_IncludesFile()
        {
            // Arrange
            var readOnlyFile = Path.Combine(this.testDirectory, "readonly.etl");
            File.WriteAllText(readOnlyFile, "content");

            // Make file read-only
            var fileInfo = new FileInfo(readOnlyFile);
            fileInfo.IsReadOnly = true;

            using var handler = new EventFileHandler();

            try
            {
                // Act
                var result = handler.ResolveAllPaths(new[] { this.testDirectory });

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Count);
                Assert.IsTrue(result.Contains(readOnlyFile), "Should include read-only file");
            }
            finally
            {
                // Cleanup - remove read-only attribute
                if (File.Exists(readOnlyFile))
                {
                    fileInfo.IsReadOnly = false;
                }
            }
        }

        /// <summary>
        /// Tests handling of symbolic links (if supported).
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithSymbolicLink_FollowsLink()
        {
            // Arrange
            var targetFile = Path.Combine(this.testDirectory, "target.etl");
            var linkFile = Path.Combine(this.testDirectory, "link.etl");

            File.WriteAllText(targetFile, "target content");

            try
            {
                // Try to create symbolic link (may not work on all systems)
                File.CreateSymbolicLink(linkFile, targetFile);

                using var handler = new EventFileHandler();

                // Act
                var result = handler.ResolveAllPaths(new[] { this.testDirectory });

                // Assert
                Assert.IsNotNull(result);
                // Should include at least the target file
                Assert.IsTrue(result.Count >= 1, "Should include at least one file");
                Assert.IsTrue(result.Any(f => f.Contains("target.etl")), "Should find target file");
            }
            catch (NotSupportedException)
            {
                Assert.Inconclusive("Symbolic links not supported on this system");
            }
            catch (UnauthorizedAccessException)
            {
                Assert.Inconclusive("Insufficient permissions to create symbolic links");
            }
            catch (IOException ex) when (ex.Message.Contains("privilege", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Insufficient privileges to create symbolic links (requires administrator on Windows)");
            }
        }

        /// <summary>
        /// Tests handling of hidden files.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithHiddenFile_IncludesFile()
        {
            // Arrange
            var hiddenFile = Path.Combine(this.testDirectory, ".hidden.etl");
            File.WriteAllText(hiddenFile, "content");

            try
            {
                // Set hidden attribute (Windows-specific)
                File.SetAttributes(hiddenFile, FileAttributes.Hidden);

                using var handler = new EventFileHandler();

                // Act
                var result = handler.ResolveAllPaths(new[] { this.testDirectory });

                // Assert
                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Count, "Should include hidden file");
            }
            catch (PlatformNotSupportedException)
            {
                Assert.Inconclusive("Hidden attribute not supported on this platform");
            }
            finally
            {
                // Cleanup - remove hidden attribute
                if (File.Exists(hiddenFile))
                {
                    try
                    {
                        File.SetAttributes(hiddenFile, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        /// <summary>
        /// Tests handling of empty array input.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithEmptyArray_ReturnsEmptyList()
        {
            // Arrange
            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new string[0]);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Should return empty list for empty array");
        }

        /// <summary>
        /// Tests handling of null path in array.
        /// </summary>
        [TestMethod]
        public void ResolveAllPaths_WithNullPathInArray_HandlesGracefully()
        {
            // Arrange
            var validFile = Path.Combine(this.testDirectory, "valid.etl");
            File.WriteAllText(validFile, "content");

            using var handler = new EventFileHandler();

            // Act
            var result = handler.ResolveAllPaths(new[] { validFile, null, this.testDirectory });

            // Assert
            Assert.IsNotNull(result);
            // Should handle null gracefully and continue processing
        }
    }
}
