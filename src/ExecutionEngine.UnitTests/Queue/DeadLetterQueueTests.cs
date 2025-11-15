// -----------------------------------------------------------------------
// <copyright file="DeadLetterQueueTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Queue;

using ExecutionEngine.Queue;
using FluentAssertions;

[TestClass]
public class DeadLetterQueueTests
{
    [TestMethod]
    public void Constructor_WithValidMaxSize_ShouldSucceed()
    {
        // Arrange & Act
        var dlq = new DeadLetterQueue(maxSize: 100);

        // Assert
        dlq.MaxSize.Should().Be(100);
        dlq.Count.Should().Be(0);
    }

    [TestMethod]
    public void Constructor_WithInvalidMaxSize_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => new DeadLetterQueue(maxSize: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public async Task AddAsync_WithValidEntry_ShouldSucceed()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };

        // Act
        var result = await dlq.AddAsync(envelope, "Test failure reason");

        // Assert
        result.Should().BeTrue();
        dlq.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task AddAsync_WithNullEnvelope_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        // Act
        Func<Task> act = async () => await dlq.AddAsync(null!, "reason");

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task AddAsync_WithNullReason_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };

        // Act
        Func<Task> act = async () => await dlq.AddAsync(envelope, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task AddAsync_WithException_ShouldStoreException()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };
        var exception = new InvalidOperationException("Test exception");

        // Act
        await dlq.AddAsync(envelope, "Test failure", exception);

        // Assert
        var entries = await dlq.GetAllEntriesAsync();
        entries.Should().HaveCount(1);
        entries[0].Exception.Should().BeSameAs(exception);
        entries[0].Reason.Should().Be("Test failure");
    }

    [TestMethod]
    public async Task AddAsync_ExceedingMaxSize_ShouldDropOldestEntry()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 3);

        // Add 4 entries
        for (var i = 0; i < 4; i++)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = $"Message-{i}"
            };
            await dlq.AddAsync(envelope, $"Reason-{i}");
        }

        // Act
        var entries = await dlq.GetAllEntriesAsync();

        // Assert
        dlq.Count.Should().Be(3);
        entries.Should().HaveCount(3);
        entries[0].Envelope.MessageType.Should().Be("Message-1"); // First one dropped
    }

    [TestMethod]
    public async Task GetAllEntriesAsync_ShouldReturnAllEntries()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        for (var i = 0; i < 3; i++)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = $"Message-{i}"
            };
            await dlq.AddAsync(envelope, $"Reason-{i}");
        }

        // Act
        var entries = await dlq.GetAllEntriesAsync();

        // Assert
        entries.Should().HaveCount(3);
        entries[0].Envelope.MessageType.Should().Be("Message-0");
        entries[1].Envelope.MessageType.Should().Be("Message-1");
        entries[2].Envelope.MessageType.Should().Be("Message-2");
    }

    [TestMethod]
    public async Task GetEntryAsync_WithValidId_ShouldReturnEntry()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);
        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };

        await dlq.AddAsync(envelope, "Test reason");
        var entries = await dlq.GetAllEntriesAsync();
        var entryId = entries[0].EntryId;

        // Act
        var entry = await dlq.GetEntryAsync(entryId);

        // Assert
        entry.Should().NotBeNull();
        entry!.EntryId.Should().Be(entryId);
        entry.Reason.Should().Be("Test reason");
    }

    [TestMethod]
    public async Task GetEntryAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        // Act
        var entry = await dlq.GetEntryAsync(Guid.NewGuid());

        // Assert
        entry.Should().BeNull();
    }

    [TestMethod]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        for (var i = 0; i < 5; i++)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = $"Message-{i}"
            };
            await dlq.AddAsync(envelope, $"Reason-{i}");
        }

        // Act
        var cleared = await dlq.ClearAsync();

        // Assert
        cleared.Should().Be(5);
        dlq.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task RemoveEntryAsync_WithValidId_ShouldRemoveEntry()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        for (var i = 0; i < 3; i++)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = $"Message-{i}"
            };
            await dlq.AddAsync(envelope, $"Reason-{i}");
        }

        var entries = await dlq.GetAllEntriesAsync();
        var entryToRemove = entries[1].EntryId;

        // Act
        var result = await dlq.RemoveEntryAsync(entryToRemove);

        // Assert
        result.Should().BeTrue();
        dlq.Count.Should().Be(2);

        var remainingEntries = await dlq.GetAllEntriesAsync();
        remainingEntries.Should().HaveCount(2);
        remainingEntries.Should().NotContain(e => e.EntryId == entryToRemove);
    }

    [TestMethod]
    public async Task RemoveEntryAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        // Act
        var result = await dlq.RemoveEntryAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public async Task DeadLetterEntry_ShouldHaveUniqueEntryId()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);

        for (var i = 0; i < 3; i++)
        {
            var envelope = new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                MessageType = $"Message-{i}"
            };
            await dlq.AddAsync(envelope, $"Reason-{i}");
        }

        // Act
        var entries = await dlq.GetAllEntriesAsync();

        // Assert
        var entryIds = entries.Select(e => e.EntryId).ToArray();
        entryIds.Should().OnlyHaveUniqueItems();
    }

    [TestMethod]
    public async Task DeadLetterEntry_ShouldHaveTimestamp()
    {
        // Arrange
        var dlq = new DeadLetterQueue(maxSize: 10);
        var beforeAdd = DateTime.UtcNow;

        var envelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid(),
            MessageType = "TestMessage"
        };

        // Act
        await dlq.AddAsync(envelope, "Test reason");
        var afterAdd = DateTime.UtcNow;

        // Assert
        var entries = await dlq.GetAllEntriesAsync();
        entries[0].Timestamp.Should().BeOnOrAfter(beforeAdd);
        entries[0].Timestamp.Should().BeOnOrBefore(afterAdd);
    }
}
