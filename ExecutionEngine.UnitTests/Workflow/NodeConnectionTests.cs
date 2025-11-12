// -----------------------------------------------------------------------
// <copyright file="NodeConnectionTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Enums;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class NodeConnectionTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var connection = new NodeConnection();

        // Assert
        connection.SourceNodeId.Should().BeEmpty();
        connection.TargetNodeId.Should().BeEmpty();
        connection.TriggerMessageType.Should().Be(MessageType.Complete);
        connection.Condition.Should().BeNull();
        connection.IsEnabled.Should().BeTrue();
        connection.Metadata.Should().BeNull();
        connection.Priority.Should().Be(0);
    }

    [TestMethod]
    public void Properties_ShouldBeSettable()
    {
        // Arrange & Act
        var connection = new NodeConnection
        {
            SourceNodeId = "source-1",
            TargetNodeId = "target-1",
            TriggerMessageType = MessageType.Fail,
            Condition = "result > 0",
            IsEnabled = false,
            Priority = 10
        };

        // Assert
        connection.SourceNodeId.Should().Be("source-1");
        connection.TargetNodeId.Should().Be("target-1");
        connection.TriggerMessageType.Should().Be(MessageType.Fail);
        connection.Condition.Should().Be("result > 0");
        connection.IsEnabled.Should().BeFalse();
        connection.Priority.Should().Be(10);
    }

    [TestMethod]
    public void TriggerMessageType_ShouldSupportAllMessageTypes()
    {
        // Arrange
        var connection = new NodeConnection();

        // Act & Assert - Complete
        connection.TriggerMessageType = MessageType.Complete;
        connection.TriggerMessageType.Should().Be(MessageType.Complete);

        // Act & Assert - Fail
        connection.TriggerMessageType = MessageType.Fail;
        connection.TriggerMessageType.Should().Be(MessageType.Fail);

        // Act & Assert - Progress
        connection.TriggerMessageType = MessageType.Progress;
        connection.TriggerMessageType.Should().Be(MessageType.Progress);
    }

    [TestMethod]
    public void Metadata_ShouldBeSettableAndModifiable()
    {
        // Arrange
        var connection = new NodeConnection
        {
            Metadata = new Dictionary<string, object>
            {
                { "description", "Error handler connection" },
                { "retryCount", 3 }
            }
        };

        // Act
        connection.Metadata!["timeout"] = 30;

        // Assert
        connection.Metadata.Should().HaveCount(3);
        connection.Metadata["description"].Should().Be("Error handler connection");
        connection.Metadata["retryCount"].Should().Be(3);
        connection.Metadata["timeout"].Should().Be(30);
    }

    [TestMethod]
    public void Connection_WithCondition_ShouldStoreConditionExpression()
    {
        // Arrange & Act
        var connection = new NodeConnection
        {
            SourceNodeId = "validator",
            TargetNodeId = "processor",
            Condition = "validation.IsValid && data.Count > 0"
        };

        // Assert
        connection.Condition.Should().Be("validation.IsValid && data.Count > 0");
    }

    [TestMethod]
    public void Connection_WithPriority_ShouldStorePriorityValue()
    {
        // Arrange & Act
        var highPriority = new NodeConnection { Priority = 100 };
        var lowPriority = new NodeConnection { Priority = 1 };
        var defaultPriority = new NodeConnection();

        // Assert
        highPriority.Priority.Should().Be(100);
        lowPriority.Priority.Should().Be(1);
        defaultPriority.Priority.Should().Be(0);
    }

    [TestMethod]
    public void Connection_Disabled_ShouldHaveIsEnabledFalse()
    {
        // Arrange & Act
        var connection = new NodeConnection
        {
            SourceNodeId = "node-1",
            TargetNodeId = "node-2",
            IsEnabled = false
        };

        // Assert
        connection.IsEnabled.Should().BeFalse();
    }

    [TestMethod]
    public void MultipleConnections_ShouldBeIndependent()
    {
        // Arrange & Act
        var connection1 = new NodeConnection
        {
            SourceNodeId = "A",
            TargetNodeId = "B",
            TriggerMessageType = MessageType.Complete
        };

        var connection2 = new NodeConnection
        {
            SourceNodeId = "A",
            TargetNodeId = "C",
            TriggerMessageType = MessageType.Fail
        };

        // Assert
        connection1.TargetNodeId.Should().Be("B");
        connection2.TargetNodeId.Should().Be("C");
        connection1.TriggerMessageType.Should().Be(MessageType.Complete);
        connection2.TriggerMessageType.Should().Be(MessageType.Fail);
    }
}
