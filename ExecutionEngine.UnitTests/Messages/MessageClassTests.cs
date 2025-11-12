// -----------------------------------------------------------------------
// <copyright file="MessageClassTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Messages;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Messages;
using FluentAssertions;

[TestClass]
public class MessageClassTests
{
    [TestMethod]
    public void NodeCompleteMessage_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        nodeContext.OutputData["result"] = 42;

        // Act
        var message = new NodeCompleteMessage
        {
            NodeId = "test-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid(),
            NodeInstanceId = Guid.NewGuid(),
            NodeContext = nodeContext
        };

        // Assert
        message.NodeId.Should().Be("test-node");
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.MessageId.Should().NotBe(Guid.Empty);
        message.NodeInstanceId.Should().NotBe(Guid.Empty);
        message.NodeContext.Should().BeSameAs(nodeContext);
        message.MessageType.Should().Be(MessageType.Complete);
    }

    [TestMethod]
    public void NodeCompleteMessage_MessageType_ShouldAlwaysBeComplete()
    {
        // Arrange & Act
        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageType.Should().Be(MessageType.Complete);
    }

    [TestMethod]
    public void NodeFailMessage_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        var exception = new InvalidOperationException("Test error");

        // Act
        var message = new NodeFailMessage
        {
            NodeId = "failed-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid(),
            NodeInstanceId = Guid.NewGuid(),
            NodeContext = nodeContext,
            Exception = exception,
            ErrorMessage = "Node execution failed"
        };

        // Assert
        message.NodeId.Should().Be("failed-node");
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.MessageId.Should().NotBe(Guid.Empty);
        message.NodeInstanceId.Should().NotBe(Guid.Empty);
        message.NodeContext.Should().BeSameAs(nodeContext);
        message.Exception.Should().BeSameAs(exception);
        message.ErrorMessage.Should().Be("Node execution failed");
        message.MessageType.Should().Be(MessageType.Fail);
    }

    [TestMethod]
    public void NodeFailMessage_MessageType_ShouldAlwaysBeFail()
    {
        // Arrange & Act
        var message = new NodeFailMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageType.Should().Be(MessageType.Fail);
    }

    [TestMethod]
    public void ProgressMessage_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var message = new ProgressMessage
        {
            NodeId = "progress-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid(),
            NodeInstanceId = Guid.NewGuid(),
            Status = "Processing items",
            ProgressPercent = 75
        };

        // Assert
        message.NodeId.Should().Be("progress-node");
        message.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        message.MessageId.Should().NotBe(Guid.Empty);
        message.NodeInstanceId.Should().NotBe(Guid.Empty);
        message.Status.Should().Be("Processing items");
        message.ProgressPercent.Should().Be(75);
        message.MessageType.Should().Be(MessageType.Progress);
    }

    [TestMethod]
    public void ProgressMessage_MessageType_ShouldAlwaysBeProgress()
    {
        // Arrange & Act
        var message = new ProgressMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageType.Should().Be(MessageType.Progress);
    }

    [TestMethod]
    public void NodeCompleteMessage_DefaultMessageId_ShouldBeGenerated()
    {
        // Arrange & Act
        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageId.Should().NotBe(Guid.Empty);
    }

    [TestMethod]
    public void NodeFailMessage_DefaultMessageId_ShouldBeGenerated()
    {
        // Arrange & Act
        var message = new NodeFailMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageId.Should().NotBe(Guid.Empty);
    }

    [TestMethod]
    public void ProgressMessage_DefaultMessageId_ShouldBeGenerated()
    {
        // Arrange & Act
        var message = new ProgressMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Assert
        message.MessageId.Should().NotBe(Guid.Empty);
    }

    [TestMethod]
    public void NodeCompleteMessage_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var message = new NodeCompleteMessage();

        // Assert
        message.NodeId.Should().BeEmpty();
        message.MessageId.Should().NotBe(Guid.Empty);
        message.MessageType.Should().Be(MessageType.Complete);
    }

    [TestMethod]
    public void NodeFailMessage_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var message = new NodeFailMessage();

        // Assert
        message.NodeId.Should().BeEmpty();
        message.ErrorMessage.Should().BeEmpty();
        message.MessageId.Should().NotBe(Guid.Empty);
        message.MessageType.Should().Be(MessageType.Fail);
    }

    [TestMethod]
    public void ProgressMessage_DefaultValues_ShouldBeSet()
    {
        // Arrange & Act
        var message = new ProgressMessage();

        // Assert
        message.NodeId.Should().BeEmpty();
        message.Status.Should().BeEmpty();
        message.MessageId.Should().NotBe(Guid.Empty);
        message.MessageType.Should().Be(MessageType.Progress);
    }
}
