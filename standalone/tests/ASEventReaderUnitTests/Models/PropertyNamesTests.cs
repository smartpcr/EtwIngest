//-----------------------------------------------------------------------
// <copyright file="PropertyNamesTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ASEventReaderUnitTests.Models
{
    using ASEventReader.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for PropertyNames class.
    /// </summary>
    [TestClass]
    public class PropertyNamesTests
    {
        /// <summary>
        /// Tests that all constant property names are defined.
        /// </summary>
        [TestMethod]
        public void Constants_AreDefined()
        {
            // Assert
            Assert.AreEqual("durationMs", PropertyNames.DurationMs);
            Assert.AreEqual("errorMessage", PropertyNames.ErrorMessage);
            Assert.AreEqual("callStack", PropertyNames.CallStack);
            Assert.AreEqual("success", PropertyNames.Success);
            Assert.AreEqual("ActivityId", PropertyNames.ActivityId);
            Assert.AreEqual("RelatedActivityId", PropertyNames.RelatedActivityId);
            Assert.AreEqual("EventType", PropertyNames.EventType);
            Assert.AreEqual("ProviderName", PropertyNames.ProviderName);
            Assert.AreEqual("Path", PropertyNames.Path);
            Assert.AreEqual("TimeStamp", PropertyNames.TimeStamp);
            Assert.AreEqual("ProcessID", PropertyNames.ProcessID);
            Assert.AreEqual("ThreadID", PropertyNames.ThreadID);
            Assert.AreEqual("FormattedMessage", PropertyNames.FormattedMessage);
            Assert.AreEqual("AS_HierarchyLevel", PropertyNames.HierarchyLevel);
            Assert.AreEqual("AS_TreeEventType", PropertyNames.TreeEventType);
        }

        /// <summary>
        /// Tests that AsProvidedProperties list is populated.
        /// </summary>
        [TestMethod]
        public void AsProvidedProperties_IsPopulated()
        {
            // Assert
            Assert.IsNotNull(PropertyNames.AsProvidedProperties);
            Assert.IsTrue(PropertyNames.AsProvidedProperties.Count > 0);
        }

        /// <summary>
        /// Tests that AsProvidedProperties contains expected properties.
        /// </summary>
        [TestMethod]
        public void AsProvidedProperties_ContainsExpectedProperties()
        {
            // Assert
            Assert.IsTrue(PropertyNames.AsProvidedProperties.Contains(PropertyNames.ActivityId));
            Assert.IsTrue(PropertyNames.AsProvidedProperties.Contains(PropertyNames.EventType));
            Assert.IsTrue(PropertyNames.AsProvidedProperties.Contains(PropertyNames.TimeStamp));
            Assert.IsTrue(PropertyNames.AsProvidedProperties.Contains(PropertyNames.ProviderName));
        }
    }
}
