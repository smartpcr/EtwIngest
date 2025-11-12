// -----------------------------------------------------------------------
// <copyright file="NodeMessageQueueTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Queue;

using ExecutionEngine.Enums;
using ExecutionEngine.Messages;
using ExecutionEngine.Queue;
using FluentAssertions;

[TestClass]
public class NodeMessageQueueTests
{
    [TestMethod]
    public void Constructor_WithValidNodeId_ShouldSucceed()
    {
        // Arrange & Act
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        // Assert
        queue.NodeId.Should().Be("test-node");
        queue.Capacity.Should().Be(10);
        queue.Count.Should().Be(0);
    }

    [TestMethod]
    public void Constructor_WithNullNodeId_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => new NodeMessageQueue(null!, capacity: 10);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Constructor_WithEmptyNodeId_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => new NodeMessageQueue(string.Empty, capacity: 10);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Constructor_WithCustomBuffer_ShouldSucceed()
    {
        // Arrange
        var customBuffer = new CircularBuffer(capacity: 20);

        // Act
        var queue = new NodeMessageQueue("test-node", customBuffer);

        // Assert
        queue.NodeId.Should().Be("test-node");
        queue.Capacity.Should().Be(20);
    }

    [TestMethod]
    public void Constructor_WithNullBuffer_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => new NodeMessageQueue("test-node", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public async Task EnqueueAsync_WithValidMessage_ShouldSucceed()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);
        var message = new NodeCompleteMessage
        {
            NodeId = "source-node",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await queue.EnqueueAsync(message);

        // Assert
        result.Should().BeTrue();
        queue.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task EnqueueAsync_WithNullMessage_ShouldThrowException()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        // Act
        Func<Task> act = async () => await queue.EnqueueAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task CheckoutAsync_WithAvailableMessage_ShouldReturnMessage()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);
        var message = new NodeCompleteMessage
        {
            NodeId = "source-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid()
        };

        await queue.EnqueueAsync(message);

        // Act
        var checkedOut = await queue.CheckoutAsync<NodeCompleteMessage>(
            "handler-1",
            TimeSpan.FromSeconds(30));

        // Assert
        checkedOut.Should().NotBeNull();
        checkedOut!.NodeId.Should().Be("source-node");
        checkedOut.MessageId.Should().Be(message.MessageId);
    }

    [TestMethod]
    public async Task CheckoutAsync_WithNoMessages_ShouldReturnNull()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        // Act
        var checkedOut = await queue.CheckoutAsync<NodeCompleteMessage>(
            "handler-1",
            TimeSpan.FromSeconds(30));

        // Assert
        checkedOut.Should().BeNull();
    }

    [TestMethod]
    public async Task CheckoutAsync_WithWrongMessageType_ShouldReturnNull()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);
        var message = new NodeFailMessage
        {
            NodeId = "source-node",
            Timestamp = DateTime.UtcNow
        };

        await queue.EnqueueAsync(message);

        // Act
        var checkedOut = await queue.CheckoutAsync<NodeCompleteMessage>(
            "handler-1",
            TimeSpan.FromSeconds(30));

        // Assert
        checkedOut.Should().BeNull();
    }

    [TestMethod]
    public async Task AcknowledgeAsync_WithValidMessageId_ShouldRemoveMessage()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);
        var message = new NodeCompleteMessage
        {
            NodeId = "source-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid()
        };

        await queue.EnqueueAsync(message);
        var checkedOut = await queue.CheckoutAsync<NodeCompleteMessage>("handler-1", TimeSpan.FromSeconds(30));

        // Act
        var result = await queue.AcknowledgeAsync(checkedOut!.MessageId);

        // Assert
        result.Should().BeTrue();
        queue.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task RequeueAsync_WithValidMessageId_ShouldRequeueMessage()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);
        var message = new NodeCompleteMessage
        {
            NodeId = "source-node",
            Timestamp = DateTime.UtcNow,
            MessageId = Guid.NewGuid()
        };

        await queue.EnqueueAsync(message);
        var checkedOut = await queue.CheckoutAsync<NodeCompleteMessage>("handler-1", TimeSpan.FromSeconds(30));

        // Act
        var result = await queue.RequeueAsync(checkedOut!.MessageId);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task GetAllMessagesAsync_ShouldReturnAllMessages()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        for (int i = 0; i < 3; i++)
        {
            await queue.EnqueueAsync(new NodeCompleteMessage
            {
                NodeId = $"source-{i}",
                Timestamp = DateTime.UtcNow
            });
        }

        // Act
        var messages = await queue.GetAllMessagesAsync();

        // Assert
        messages.Should().HaveCount(3);
        messages.Should().AllBeOfType<NodeCompleteMessage>();
    }

    [TestMethod]
    public async Task GetCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
        await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });

        // Act
        var count = await queue.GetCountAsync();

        // Assert
        count.Should().Be(2);
    }

    [TestMethod]
    public async Task EnqueueAsync_MultipleMessageTypes_ShouldStoreAll()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
        await queue.EnqueueAsync(new NodeFailMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });
        await queue.EnqueueAsync(new ProgressMessage { NodeId = "node-3", Timestamp = DateTime.UtcNow });

        // Act
        var count = await queue.GetCountAsync();
        var messages = await queue.GetAllMessagesAsync();

        // Assert
        count.Should().Be(3);
        messages.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task CheckoutAsync_TypeFiltering_ShouldReturnCorrectType()
    {
        // Arrange
        var queue = new NodeMessageQueue("test-node", capacity: 10);

        await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
        await queue.EnqueueAsync(new NodeFailMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });

        // Act
        var failMessage = await queue.CheckoutAsync<NodeFailMessage>("handler-1", TimeSpan.FromSeconds(30));

        // Assert
        failMessage.Should().NotBeNull();
        failMessage.Should().BeOfType<NodeFailMessage>();
        failMessage!.NodeId.Should().Be("node-2");
    }
}
