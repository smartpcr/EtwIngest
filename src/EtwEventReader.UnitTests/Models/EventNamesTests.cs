//-----------------------------------------------------------------------
// <copyright file="EventNamesTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.UnitTests.Models
{
    using EtwEventReader.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for EventNames class.
    /// </summary>
    [TestClass]
    public class EventNamesTests
    {
        /// <summary>
        /// Tests that ManifestEventName constant is defined.
        /// </summary>
        [TestMethod]
        public void ManifestEventName_IsDefined()
        {
            // Assert
            Assert.AreEqual("ManifestData", EventNames.ManifestEventName);
        }
    }
}
