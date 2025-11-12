// -----------------------------------------------------------------------
// <copyright file="CircularBufferTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Queue;

using ExecutionEngine.Enums;
using ExecutionEngine.Queue;
using FluentAssertions;

[TestClass]
public class CircularBufferTests
{
    [TestMethod]
    public async Task EnqueueAsync_SingleMessage_ShouldSucceed()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage",
            Payload = "test data"
        };

        // Act
        var result = await buffer.EnqueueAsync(envelope);

        // Assert
        result.Should().BeTrue();
        var count = await buffer.GetCountAsync();
        count.Should().Be(1);
    }

    [TestMethod]
    public async Task EnqueueAsync_CapacityMessages_ShouldFillBuffer()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 5);

        // Act
        for (int i = 0; i < 5; i++)
        {
            await buffer.EnqueueAsync(new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = "TestMessage",
                Payload = $"message-{i}"
            });
        }

        // Assert
        var count = await buffer.GetCountAsync();
        count.Should().Be(5);
    }

    [TestMethod]
    public async Task EnqueueAsync_OverCapacity_ShouldDropOldestReadyMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 3);
        var msg1 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test", Payload = "1" };
        var msg2 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test", Payload = "2" };
        var msg3 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test", Payload = "3" };
        var msg4 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test", Payload = "4" };

        await buffer.EnqueueAsync(msg1);
        await buffer.EnqueueAsync(msg2);
        await buffer.EnqueueAsync(msg3);

        // Act - enqueue 4th message (over capacity)
        await buffer.EnqueueAsync(msg4);

        // Assert - oldest message should be dropped
        var count = await buffer.GetCountAsync();
        count.Should().Be(3);
    }

    [TestMethod]
    public async Task CheckoutAsync_LeasesMessageWithTimeout()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "TestMessage",
            Payload = "test"
        };
        await buffer.EnqueueAsync(envelope);

        // Act
        var leased = await buffer.CheckoutAsync(
            typeof(TestMessage),
            "handler-1",
            TimeSpan.FromMinutes(5));

        // Assert
        leased.Should().NotBeNull();
        leased!.Status.Should().Be(MessageStatus.InFlight);
        leased.Lease.Should().NotBeNull();
        leased.Lease!.HandlerId.Should().Be("handler-1");
        leased.Lease.LeaseExpiry.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(5),
            TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task CheckoutAsync_NoMessages_ReturnsNull()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        var result = await buffer.CheckoutAsync(
            typeof(TestMessage),
            "handler-1",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task AcknowledgeAsync_RemovesMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "TestMessage",
            Payload = "test"
        };
        await buffer.EnqueueAsync(envelope);
        var leased = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(5));

        // Act
        var result = await buffer.AcknowledgeAsync(leased!.MessageId);

        // Assert
        result.Should().BeTrue();
        var count = await buffer.GetCountAsync();
        count.Should().Be(0);
    }

    [TestMethod]
    public async Task RequeueAsync_IncrementsRetryCount()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "TestMessage",
            Payload = "test",
            RetryCount = 0,
            MaxRetries = 3
        };
        await buffer.EnqueueAsync(envelope);
        var leased = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(5));

        // Act
        var result = await buffer.RequeueAsync(leased!.MessageId);

        // Assert
        result.Should().BeTrue();
        var requeued = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(5));
        requeued.Should().NotBeNull();
        requeued!.RetryCount.Should().Be(1);
    }

    [TestMethod]
    public async Task ConcurrentEnqueue_ThreadSafe()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 1000);
        var tasks = new List<Task>();

        // Act - enqueue from 10 threads concurrently
        for (int i = 0; i < 10; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < 100; j++)
                {
                    await buffer.EnqueueAsync(new MessageEnvelope
                    {
                        MessageId = Guid.NewGuid(),
                        MessageType = "Test",
                        Payload = $"thread-{threadId}-msg-{j}"
                    });
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var count = await buffer.GetCountAsync();
        count.Should().Be(1000); // All messages should be enqueued
    }

    [TestMethod]
    public async Task ReplaceAsync_SupersedesExistingMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var original = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "Test",
            DeduplicationKey = "unique-key",
            Payload = "original"
        };
        await buffer.EnqueueAsync(original);

        var replacement = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "Test",
            DeduplicationKey = "unique-key",
            Payload = "replacement"
        };

        // Act
        var result = await buffer.ReplaceAsync(replacement, "unique-key");

        // Assert
        result.Should().BeTrue();
        var count = await buffer.GetCountAsync();
        count.Should().Be(1); // Should still have 1 message
    }

    [TestMethod]
    public async Task GetAllMessagesAsync_ReturnsActiveMessages()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Test1" });
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Test2" });
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Test3" });

        // Act
        var messages = await buffer.GetAllMessagesAsync();

        // Assert
        messages.Should().HaveCount(3);
        messages.Should().OnlyContain(m => m.Status != MessageStatus.Completed);
    }

    [TestMethod]
    public async Task CheckoutAsync_WithNotBeforeInFuture_ShouldReturnNull()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "TestMessage",
            Payload = "test",
            NotBefore = DateTime.UtcNow.AddMinutes(5) // 5 minutes in the future
        };
        await buffer.EnqueueAsync(envelope);

        // Act
        var leased = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(1));

        // Assert
        leased.Should().BeNull();
    }

    [TestMethod]
    public async Task CheckoutAsync_WithSupersededMessage_ShouldReturnNull()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "TestMessage",
            Payload = "test",
            IsSuperseded = true
        };
        await buffer.EnqueueAsync(envelope);

        // Act
        var leased = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(1));

        // Assert
        leased.Should().BeNull();
    }

    [TestMethod]
    public async Task RemoveAsync_WithValidMessageId_ShouldRemoveMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };
        await buffer.EnqueueAsync(envelope);

        // Act
        var result = await buffer.RemoveAsync(envelope.MessageId);

        // Assert
        result.Should().BeTrue();
        var count = await buffer.GetCountAsync();
        count.Should().Be(0);
    }

    [TestMethod]
    public async Task RemoveAsync_WithInvalidMessageId_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        var result = await buffer.RemoveAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task RestoreAsync_WithValidEnvelope_ShouldSucceed()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage",
            Status = MessageStatus.InFlight
        };

        // Act
        var result = await buffer.RestoreAsync(envelope);

        // Assert
        result.Should().BeTrue();
        var messages = await buffer.GetAllMessagesAsync();
        messages.Should().HaveCount(1);
        messages[0].Status.Should().Be(MessageStatus.InFlight); // Status preserved
    }

    [TestMethod]
    public async Task RestoreAsync_FullBuffer_ShouldOverwriteCompletedMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 3);

        // Fill buffer with messages
        var msg1 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test1" };
        var msg2 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test2" };
        var msg3 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test3" };

        await buffer.EnqueueAsync(msg1);
        await buffer.EnqueueAsync(msg2);
        await buffer.EnqueueAsync(msg3);

        // Mark one as completed
        await buffer.AcknowledgeAsync(msg2.MessageId);

        // Act - restore a new message
        var restoredMsg = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Restored" };
        var result = await buffer.RestoreAsync(restoredMsg);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task EnqueueAsync_FullBufferWithAllInFlight_ShouldForceOverwrite()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 2);

        var msg1 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = typeof(TestMessage).FullName ?? "Test" };
        var msg2 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = typeof(TestMessage).FullName ?? "Test" };

        await buffer.EnqueueAsync(msg1);
        await buffer.EnqueueAsync(msg2);

        // Check out both messages (they're now InFlight)
        await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(5));
        await buffer.CheckoutAsync(typeof(TestMessage), "handler-2", TimeSpan.FromMinutes(5));

        // Act - enqueue when all are InFlight (should force overwrite)
        var msg3 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Test3" };
        var result = await buffer.EnqueueAsync(msg3);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task CheckoutAsync_AfterMultipleFailedAttempts_ShouldEventuallyFindMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 5);

        // Add messages of different types
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Type1" });
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Type2" });
        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = typeof(TestMessage).FullName ?? "Test" });

        // Act
        var leased = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(1));

        // Assert
        leased.Should().NotBeNull();
    }

    [TestMethod]
    public async Task RequeueAsync_WithNonExistentMessage_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        var result = await buffer.RequeueAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task ReplaceAsync_WithNoMatchingDeduplicationKey_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var replacement = new MessageEnvelope
        {
            MessageType = "Test",
            DeduplicationKey = "key-1"
        };

        // Act
        var result = await buffer.ReplaceAsync(replacement, "non-existent-key");

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task Count_Property_ReflectsActiveMessages()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act & Assert
        buffer.Count.Should().Be(0);

        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Test1" });
        buffer.Count.Should().Be(1);

        await buffer.EnqueueAsync(new MessageEnvelope { MessageType = "Test2" });
        buffer.Count.Should().Be(2);

        var messages = await buffer.GetAllMessagesAsync();
        await buffer.AcknowledgeAsync(messages[0].MessageId);
        buffer.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task Capacity_Property_ReturnsCorrectValue()
    {
        // Arrange & Act
        var buffer = new CircularBuffer(capacity: 25);

        // Assert
        buffer.Capacity.Should().Be(25);
    }

    [TestMethod]
    public async Task ConcurrentCheckout_MultipleHandlers_ShouldNotCheckoutSameMessage()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 100);

        // Enqueue 10 messages
        for (int i = 0; i < 10; i++)
        {
            await buffer.EnqueueAsync(new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = typeof(TestMessage).FullName ?? "Test",
                Payload = $"msg-{i}"
            });
        }

        // Act - Multiple handlers try to checkout concurrently
        var tasks = new List<Task<MessageEnvelope?>>();
        for (int i = 0; i < 10; i++)
        {
            var handlerId = $"handler-{i}";
            tasks.Add(Task.Run(() => buffer.CheckoutAsync(typeof(TestMessage), handlerId, TimeSpan.FromMinutes(1))));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - Each message should be checked out by only one handler
        var checkedOutMessages = results.Where(r => r != null).ToList();
        checkedOutMessages.Should().HaveCount(10);

        var uniqueMessageIds = checkedOutMessages.Select(m => m!.MessageId).Distinct().Count();
        uniqueMessageIds.Should().Be(10); // All different messages
    }

    [TestMethod]
    public async Task ConcurrentEnqueueAndDequeue_ShouldMaintainConsistency()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 50);
        var enqueuedCount = 0;
        var dequeuedCount = 0;

        // Act - Concurrent enqueue and checkout
        var enqueueTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await buffer.EnqueueAsync(new MessageEnvelope
                {
                    MessageType = typeof(TestMessage).FullName ?? "Test",
                    Payload = $"msg-{i}"
                });
                Interlocked.Increment(ref enqueuedCount);
                await Task.Delay(1);
            }
        });

        var dequeueTask = Task.Run(async () =>
        {
            while (Volatile.Read(ref enqueuedCount) < 100 || buffer.Count > 0)
            {
                var msg = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(1));
                if (msg != null)
                {
                    await buffer.AcknowledgeAsync(msg.MessageId);
                    Interlocked.Increment(ref dequeuedCount);
                }
                await Task.Delay(1);
            }
        });

        await Task.WhenAll(enqueueTask, dequeueTask);

        // Assert
        enqueuedCount.Should().Be(100);
        dequeuedCount.Should().BeLessOrEqualTo(100);
    }

    [TestMethod]
    public async Task RestoreAsync_FullBuffer_AllInFlight_ShouldForceOverwrite()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 2);

        var msg1 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = typeof(TestMessage).FullName ?? "Test" };
        var msg2 = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = typeof(TestMessage).FullName ?? "Test" };

        await buffer.EnqueueAsync(msg1);
        await buffer.EnqueueAsync(msg2);

        // Checkout both (now InFlight)
        await buffer.CheckoutAsync(typeof(TestMessage), "h1", TimeSpan.FromMinutes(1));
        await buffer.CheckoutAsync(typeof(TestMessage), "h2", TimeSpan.FromMinutes(1));

        // Act - Restore when buffer full with InFlight messages
        var restoredMsg = new MessageEnvelope { MessageId = Guid.NewGuid(), MessageType = "Restored", Status = MessageStatus.Ready };
        var result = await buffer.RestoreAsync(restoredMsg);

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task ReplaceAsync_WithInFlightMessage_ShouldSupersede()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var originalMsg = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = typeof(TestMessage).FullName ?? "Test",
            DeduplicationKey = "unique-key-123"
        };

        await buffer.EnqueueAsync(originalMsg);

        // Checkout the message (now InFlight)
        var checkedOut = await buffer.CheckoutAsync(typeof(TestMessage), "handler-1", TimeSpan.FromMinutes(1));
        checkedOut.Should().NotBeNull();

        // Act - Replace the InFlight message
        var replacementMsg = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "Replacement",
            DeduplicationKey = "unique-key-123"
        };

        var result = await buffer.ReplaceAsync(replacementMsg, "unique-key-123");

        // Assert
        result.Should().BeTrue();
    }

    [TestMethod]
    public async Task EnqueueAsync_NullEnvelope_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        Func<Task> act = async () => await buffer.EnqueueAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task CheckoutAsync_NullMessageType_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        Func<Task> act = async () => await buffer.CheckoutAsync(null!, "handler-1", TimeSpan.FromMinutes(1));

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task CheckoutAsync_NullOrWhitespaceHandlerId_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        Func<Task> act = async () => await buffer.CheckoutAsync(typeof(TestMessage), "", TimeSpan.FromMinutes(1));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task ReplaceAsync_NullEnvelope_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        Func<Task> act = async () => await buffer.ReplaceAsync(null!, "key");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task ReplaceAsync_NullOrWhitespaceDeduplicationKey_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);
        var envelope = new MessageEnvelope { MessageType = "Test" };

        // Act
        Func<Task> act = async () => await buffer.ReplaceAsync(envelope, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task RestoreAsync_NullEnvelope_ShouldThrowException()
    {
        // Arrange
        var buffer = new CircularBuffer(capacity: 10);

        // Act
        Func<Task> act = async () => await buffer.RestoreAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

// Test message type for type-based checkout
public class TestMessage
{
}
