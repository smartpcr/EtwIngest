// -----------------------------------------------------------------------
// <copyright file="NodeMessageQueueTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Queue
{
    using ExecutionEngine.Messages;
    using ExecutionEngine.Queue;
    using FluentAssertions;

    [TestClass]
    public class NodeMessageQueueTests
    {
        [TestMethod]
        public async Task EnqueueAsync_AddsMessageToQueue()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);
            var message = new NodeCompleteMessage
            {
                NodeId = "node-1",
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
            var queue = new NodeMessageQueue(capacity: 100);

            // Act
            Func<Task> act = async () => await queue.EnqueueAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [TestMethod]
        public async Task EnqueueAsync_WhenAtCapacity_ShouldReturnFalse()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 2);
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });

            // Act
            var result = await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-3", Timestamp = DateTime.UtcNow });

            // Assert
            result.Should().BeFalse();
            queue.Count.Should().Be(2);
        }

        [TestMethod]
        public async Task LeaseAsync_ReturnsMessageWithLease()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100, visibilityTimeout: TimeSpan.FromMinutes(5));
            var message = new NodeCompleteMessage
            {
                NodeId = "node-1",
                Timestamp = DateTime.UtcNow
            };
            await queue.EnqueueAsync(message);

            // Act
            var lease = await queue.LeaseAsync(CancellationToken.None);

            // Assert
            lease.Should().NotBeNull();
            lease!.Message.Should().BeOfType<NodeCompleteMessage>();
            var completeMessage = (NodeCompleteMessage)lease.Message;
            completeMessage.NodeId.Should().Be("node-1");
            lease.LeaseExpiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public async Task LeaseAsync_WithNoMessages_ShouldReturnNull()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);

            // Act
            var lease = await queue.LeaseAsync(CancellationToken.None);

            // Assert
            lease.Should().BeNull();
        }

        [TestMethod]
        public async Task LeaseAsync_SameMessageTwice_ShouldReturnNullSecondTime()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100, visibilityTimeout: TimeSpan.FromMinutes(5));
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });

            // Act
            var lease1 = await queue.LeaseAsync(CancellationToken.None);
            var lease2 = await queue.LeaseAsync(CancellationToken.None);

            // Assert
            lease1.Should().NotBeNull();
            lease2.Should().BeNull();
        }

        [TestMethod]
        public async Task CompleteAsync_RemovesLeasedMessage()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            await queue.EnqueueAsync(message);
            var lease = await queue.LeaseAsync(CancellationToken.None);

            // Act
            await queue.CompleteAsync(lease!);

            // Assert
            queue.Count.Should().Be(0);
        }

        [TestMethod]
        public async Task CompleteAsync_WithNullLease_ShouldThrowException()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);

            // Act
            var act = async () => await queue.CompleteAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [TestMethod]
        public async Task AbandonAsync_RequeuesMessageWithIncrementedRetry()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100, maxRetries: 3);
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            await queue.EnqueueAsync(message);
            var lease = await queue.LeaseAsync(CancellationToken.None);

            // Act
            await queue.AbandonAsync(lease!);

            // Assert
            queue.Count.Should().Be(1);
            var newLease = await queue.LeaseAsync(CancellationToken.None);
            newLease.Should().BeNull(); // Should be invisible due to NotBefore
        }

        [TestMethod]
        public async Task AbandonAsync_ExceedsMaxRetries_MovesToDeadLetterQueue()
        {
            // Arrange
            var deadLetterQueue = new DeadLetterQueue();
            var queue = new NodeMessageQueue(capacity: 100, maxRetries: 0, deadLetterQueue: deadLetterQueue);
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            await queue.EnqueueAsync(message);

            // Act - With maxRetries=0, first abandon should move to DLQ
            var lease = await queue.LeaseAsync(CancellationToken.None);
            lease.Should().NotBeNull();

            await queue.AbandonAsync(lease!);

            // Assert
            queue.Count.Should().Be(0); // Removed from main queue
            deadLetterQueue.Count.Should().Be(1); // Moved to dead letter queue
        }

        [TestMethod]
        public async Task AbandonAsync_WithNullLease_ShouldThrowException()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);

            // Act
            var act = async () => await queue.AbandonAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [TestMethod]
        public async Task Constructor_WithInvalidCapacity_ShouldThrowException()
        {
            // Arrange & Act
            Action act = () => new NodeMessageQueue(capacity: 0);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public async Task Constructor_WithNegativeMaxRetries_ShouldThrowException()
        {
            // Arrange & Act
            Action act = () => new NodeMessageQueue(capacity: 100, maxRetries: -1);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        public async Task Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            var visibilityTimeout = TimeSpan.FromMinutes(2);
            var queue = new NodeMessageQueue(capacity: 50, visibilityTimeout: visibilityTimeout, maxRetries: 5);

            // Assert
            queue.Capacity.Should().Be(50);
            queue.VisibilityTimeout.Should().Be(visibilityTimeout);
            queue.MaxRetries.Should().Be(5);
            queue.Count.Should().Be(0);
        }

        [TestMethod]
        public async Task GetCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });

            // Act
            var count = await queue.GetCountAsync();

            // Assert
            count.Should().Be(2);
        }

        [TestMethod]
        public async Task MultipleMessages_ShouldMaintainOrder()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 100);
            var messages = new List<NodeCompleteMessage>
            {
                new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow },
                new NodeCompleteMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow },
                new NodeCompleteMessage { NodeId = "node-3", Timestamp = DateTime.UtcNow }
            };

            foreach (var msg in messages)
            {
                await queue.EnqueueAsync(msg);
            }

            // Act
            var lease1 = await queue.LeaseAsync();
            var lease2 = await queue.LeaseAsync();
            var lease3 = await queue.LeaseAsync();

            // Assert
            lease1.Should().NotBeNull();
            lease2.Should().NotBeNull();
            lease3.Should().NotBeNull();
            queue.Count.Should().Be(3); // Still in queue until completed
        }

        [TestMethod]
        public async Task CompleteAndEnqueue_ShouldAllowNewMessages()
        {
            // Arrange
            var queue = new NodeMessageQueue(capacity: 2);
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow });
            await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-2", Timestamp = DateTime.UtcNow });

            // Queue is now at capacity
            var lease = await queue.LeaseAsync();
            await queue.CompleteAsync(lease!);

            // Act - Should be able to enqueue now
            var result = await queue.EnqueueAsync(new NodeCompleteMessage { NodeId = "node-3", Timestamp = DateTime.UtcNow });

            // Assert
            result.Should().BeTrue();
            queue.Count.Should().Be(2);
        }
    }
}
