//-----------------------------------------------------------------------
// <copyright file="EventProcessorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.UnitTests.Tools
{
    using System;
    using System.IO;
    using EtwEventReader.Tools;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for EventProcessor class.
    /// </summary>
    [TestClass]
    public class EventProcessorTests
    {
        /// <summary>
        /// Tests that EventProcessor can be instantiated.
        /// </summary>
        [TestMethod]
        public void Constructor_CreatesInstance()
        {
            // Arrange & Act
            var processor = new EventProcessor();

            // Assert
            Assert.IsNotNull(processor);
        }

        /// <summary>
        /// Tests that GetEvents throws appropriate exception for invalid path.
        /// </summary>
        [TestMethod]
        public void GetEvents_InvalidPath_ReturnsEmptyList()
        {
            // Arrange
            var processor = new EventProcessor();
            string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".etl");

            // Act
            var result = processor.GetEvents(new[] { nonExistentPath });

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests that GetEvents with empty array returns empty list.
        /// </summary>
        [TestMethod]
        public void GetEvents_EmptyArray_ReturnsEmptyList()
        {
            // Arrange
            var processor = new EventProcessor();

            // Act
            var result = processor.GetEvents(Array.Empty<string>());

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>
        /// Tests that GetEvents handles null provider name filter.
        /// </summary>
        [TestMethod]
        public void GetEvents_NullProviderName_DoesNotThrow()
        {
            // Arrange
            var processor = new EventProcessor();
            string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".etl");

            // Act & Assert - should not throw
            var result = processor.GetEvents(
                new[] { nonExistentPath },
                Guid.Empty,
                null,
                null);

            Assert.IsNotNull(result);
        }
    }
}
