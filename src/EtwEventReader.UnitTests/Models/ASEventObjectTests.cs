//-----------------------------------------------------------------------
// <copyright file="EtwEventObjectTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace EtwEventReader.UnitTests.Models
{
    using System;
    using EtwEventReader.Models;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for EtwEventObject class.
    /// </summary>
    [TestClass]
    public class EtwEventObjectTests
    {
        /// <summary>
        /// Tests that a new EtwEventObject has default values.
        /// </summary>
        [TestMethod]
        public void Constructor_CreatesObjectWithDefaults()
        {
            // Arrange & Act
            var evt = new EtwEventObject();

            // Assert
            Assert.IsNotNull(evt);
            Assert.IsNotNull(evt.Properties);
            Assert.IsNotNull(evt.DefaultDisplayNames);
            Assert.AreEqual(0, evt.Properties.Count);
            Assert.AreEqual(0, evt.DefaultDisplayNames.Count);
            Assert.AreEqual(0, evt.HierarchyLevel);
            Assert.IsFalse(evt.IsLastSibling);
            Assert.IsNull(evt.Parent);
        }

        /// <summary>
        /// Tests adding a property to the event object.
        /// </summary>
        [TestMethod]
        public void AddProperty_AddsPropertySuccessfully()
        {
            // Arrange
            var evt = new EtwEventObject();
            var propertyName = "TestProperty";
            var propertyValue = "TestValue";

            // Act
            evt.AddProperty(propertyName, propertyValue);

            // Assert
            Assert.AreEqual(1, evt.Properties.Count);
            Assert.IsTrue(evt.Properties.ContainsKey(propertyName));
            Assert.AreEqual(propertyValue, evt.Properties[propertyName]);
        }

        /// <summary>
        /// Tests adding multiple properties to the event object.
        /// </summary>
        [TestMethod]
        public void AddProperty_AddsMultipleProperties()
        {
            // Arrange
            var evt = new EtwEventObject();

            // Act
            evt.AddProperty("Property1", "Value1");
            evt.AddProperty("Property2", 42);
            evt.AddProperty("Property3", true);

            // Assert
            Assert.AreEqual(3, evt.Properties.Count);
            Assert.AreEqual("Value1", evt.Properties["Property1"]);
            Assert.AreEqual(42, evt.Properties["Property2"]);
            Assert.AreEqual(true, evt.Properties["Property3"]);
        }

        /// <summary>
        /// Tests that adding EventType property sets the EventType property.
        /// </summary>
        [TestMethod]
        public void AddProperty_EventType_SetsEventTypeProperty()
        {
            // Arrange
            var evt = new EtwEventObject();
            var eventType = "TestEvent";

            // Act
            evt.AddProperty(PropertyNames.EventType, eventType);

            // Assert
            Assert.AreEqual(eventType, evt.EventType);
        }

        /// <summary>
        /// Tests that adding TimeStamp property sets the TimeStamp property.
        /// </summary>
        [TestMethod]
        public void AddProperty_TimeStamp_SetsTimeStampProperty()
        {
            // Arrange
            var evt = new EtwEventObject();
            var timestamp = DateTime.Now;

            // Act
            evt.AddProperty(PropertyNames.TimeStamp, timestamp);

            // Assert
            Assert.AreEqual(timestamp, evt.TimeStamp);
        }

        /// <summary>
        /// Tests that adding DurationMs property sets the DurationMs property.
        /// </summary>
        [TestMethod]
        public void AddProperty_DurationMs_SetsDurationMsProperty()
        {
            // Arrange
            var evt = new EtwEventObject();
            long duration = 1500;

            // Act
            evt.AddProperty(PropertyNames.DurationMs, duration);

            // Assert
            Assert.AreEqual(duration, evt.DurationMs);
        }

        /// <summary>
        /// Tests that adding ErrorMessage property sets the ErrorMessage property.
        /// </summary>
        [TestMethod]
        public void AddProperty_ErrorMessage_SetsErrorMessageProperty()
        {
            // Arrange
            var evt = new EtwEventObject();
            var errorMessage = "Test error";

            // Act
            evt.AddProperty(PropertyNames.ErrorMessage, errorMessage);

            // Assert
            Assert.AreEqual(errorMessage, evt.ErrorMessage);
        }

        /// <summary>
        /// Tests that Success property returns true when no error message exists.
        /// </summary>
        [TestMethod]
        public void Success_ReturnsTrue_WhenNoErrorMessage()
        {
            // Arrange
            var evt = new EtwEventObject();

            // Act & Assert
            Assert.IsTrue(evt.Success);
        }

        /// <summary>
        /// Tests that Success property returns false when error message exists.
        /// </summary>
        [TestMethod]
        public void Success_ReturnsFalse_WhenErrorMessageExists()
        {
            // Arrange
            var evt = new EtwEventObject();

            // Act
            evt.AddProperty(PropertyNames.ErrorMessage, "Error occurred");

            // Assert
            Assert.IsFalse(evt.Success);
        }

        /// <summary>
        /// Tests that adding Success property as false sets it correctly.
        /// </summary>
        [TestMethod]
        public void AddProperty_Success_SetsSuccessProperty()
        {
            // Arrange
            var evt = new EtwEventObject();

            // Act
            evt.AddProperty(PropertyNames.Success, false);

            // Assert
            Assert.IsFalse(evt.Success);
        }

        /// <summary>
        /// Tests adding a child event to a parent event.
        /// </summary>
        [TestMethod]
        public void AddChild_SetsHierarchyCorrectly()
        {
            // Arrange
            var parent = new EtwEventObject();
            var child = new EtwEventObject();

            // Act
            parent.AddChild(child);

            // Assert
            Assert.AreEqual(1, child.HierarchyLevel);
            Assert.AreEqual(parent, child.Parent);
            Assert.IsTrue(child.IsLastSibling);
        }

        /// <summary>
        /// Tests adding multiple children updates IsLastSibling correctly.
        /// </summary>
        [TestMethod]
        public void AddChild_MultipleChildren_UpdatesIsLastSibling()
        {
            // Arrange
            var parent = new EtwEventObject();
            var child1 = new EtwEventObject();
            var child2 = new EtwEventObject();

            // Act
            parent.AddChild(child1);
            parent.AddChild(child2);

            // Assert
            Assert.IsFalse(child1.IsLastSibling);
            Assert.IsTrue(child2.IsLastSibling);
        }

        /// <summary>
        /// Tests ToString method returns formatted output.
        /// </summary>
        [TestMethod]
        public void ToString_ReturnsFormattedOutput()
        {
            // Arrange
            var evt = new EtwEventObject();
            evt.AddProperty(PropertyNames.EventType, "TestEvent");
            evt.AddProperty(PropertyNames.TimeStamp, DateTime.Now);

            // Act
            var result = evt.ToString();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("EventType"));
            Assert.IsTrue(result.Contains("TimeStamp"));
        }
    }
}
