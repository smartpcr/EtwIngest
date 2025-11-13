// -----------------------------------------------------------------------
// <copyright file="ConcurrencyLimiterTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Concurrency;

using ExecutionEngine.Concurrency;
using ExecutionEngine.Enums;
using FluentAssertions;

[TestClass]
public class ConcurrencyLimiterTests
{
    [TestMethod]
    public void Constructor_WithZeroConcurrency_ShouldAllowUnlimitedAcquisitions()
    {
        // Arrange & Act
        var limiter = new ConcurrencyLimiter(0); // 0 = unlimited

        // Assert
        limiter.MaxConcurrency.Should().Be(0);
        limiter.AvailableSlots.Should().BeGreaterThan(1000000); // Effectively unlimited
    }

    [TestMethod]
    public void Constructor_WithNegativeConcurrency_ShouldThrowException()
    {
        // Act
        var act = () => new ConcurrencyLimiter(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Max concurrency cannot be negative*");
    }

    [TestMethod]
    public async Task AcquireAsync_WithinLimit_ShouldAcquireImmediately()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(2);

        // Act
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);
        var slot2 = await limiter.AcquireAsync(NodePriority.Normal);

        // Assert
        slot1.Should().NotBeNull();
        slot2.Should().NotBeNull();
        limiter.AvailableSlots.Should().Be(0);

        // Cleanup
        slot1.Dispose();
        slot2.Dispose();
        limiter.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_ExceedingLimit_ShouldQueue()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);

        // Act
        var acquireTask = limiter.AcquireAsync(NodePriority.Normal);
        await Task.Delay(50); // Give time to queue

        // Assert
        acquireTask.IsCompleted.Should().BeFalse();
        limiter.QueuedCount.Should().Be(1);

        // Cleanup
        slot1.Dispose();
        await acquireTask; // Should complete now
        limiter.Dispose();
    }

    [TestMethod]
    public async Task Release_ShouldMakeSlotAvailable()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot = await limiter.AcquireAsync(NodePriority.Normal);
        limiter.AvailableSlots.Should().Be(0);

        // Act
        slot.Dispose();

        // Assert
        limiter.AvailableSlots.Should().Be(1);

        // Cleanup
        limiter.Dispose();
    }

    [TestMethod]
    public async Task Release_WithQueuedRequests_ShouldGrantToNextInQueue()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);

        var acquireTask = limiter.AcquireAsync(NodePriority.Normal);
        await Task.Delay(50); // Give time to queue
        acquireTask.IsCompleted.Should().BeFalse();

        // Act
        slot1.Dispose();
        await Task.Delay(50); // Give time to process queue

        // Assert
        acquireTask.IsCompleted.Should().BeTrue();
        var slot2 = await acquireTask;
        slot2.Should().NotBeNull();

        // Cleanup
        slot2.Dispose();
        limiter.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_HighPriority_ShouldExecuteBeforeNormal()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);

        // Queue multiple requests with different priorities
        var normalTask = limiter.AcquireAsync(NodePriority.Normal);
        var highTask = limiter.AcquireAsync(NodePriority.High);
        await Task.Delay(50); // Give time to queue

        limiter.QueuedCount.Should().Be(2);

        // Act - Release slot1, should grant to high priority first
        slot1.Dispose();
        await Task.Delay(100);

        // Assert - High priority should complete first
        highTask.IsCompleted.Should().BeTrue();
        normalTask.IsCompleted.Should().BeFalse();

        // Cleanup
        var highSlot = await highTask;
        highSlot.Dispose();
        var normalSlot = await normalTask;
        normalSlot.Dispose();
        limiter.Dispose();
    }

    [TestMethod]
    public async Task GetQueuedCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);

        // Act
        var highTask = limiter.AcquireAsync(NodePriority.High);
        var normalTask = limiter.AcquireAsync(NodePriority.Normal);
        var lowTask = limiter.AcquireAsync(NodePriority.Low);
        await Task.Delay(50);

        // Assert
        limiter.GetQueuedCount(NodePriority.High).Should().Be(1);
        limiter.GetQueuedCount(NodePriority.Normal).Should().Be(1);
        limiter.GetQueuedCount(NodePriority.Low).Should().Be(1);
        limiter.QueuedCount.Should().Be(3);

        // Cleanup
        slot1.Dispose();
        (await highTask).Dispose();
        (await normalTask).Dispose();
        (await lowTask).Dispose();
        limiter.Dispose();
    }

    [TestMethod]
    public async Task Dispose_ShouldCancelPendingRequests()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(1);
        var slot1 = await limiter.AcquireAsync(NodePriority.Normal);
        var acquireTask = limiter.AcquireAsync(NodePriority.Normal);
        await Task.Delay(50);

        // Act
        limiter.Dispose();

        // Assert
        await Task.Delay(50);
        acquireTask.IsCanceled.Should().BeTrue();
    }

    [TestMethod]
    public async Task ConcurrencySlot_DisposeMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var limiter = new ConcurrencyLimiter(2);
        var slot = await limiter.AcquireAsync(NodePriority.Normal);

        // Act
        slot.Dispose();
        slot.Dispose();
        slot.Dispose();

        // Assert
        limiter.AvailableSlots.Should().Be(2); // Should only release once

        // Cleanup
        limiter.Dispose();
    }
}
