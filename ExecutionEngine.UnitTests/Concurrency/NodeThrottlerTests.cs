// -----------------------------------------------------------------------
// <copyright file="NodeThrottlerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Concurrency;

using ExecutionEngine.Concurrency;
using FluentAssertions;

[TestClass]
public class NodeThrottlerTests
{
    [TestMethod]
    public void RegisterNode_WithZeroConcurrency_ShouldAllowUnlimitedExecutions()
    {
        // Arrange
        var throttler = new NodeThrottler();

        // Act
        throttler.RegisterNode("node1", 0); // 0 = unlimited

        // Assert
        throttler.GetMaxConcurrency("node1").Should().Be(0);
        throttler.GetAvailableSlots("node1").Should().Be(int.MaxValue);

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public void RegisterNode_WithNegativeConcurrency_ShouldThrowException()
    {
        // Arrange
        var throttler = new NodeThrottler();

        // Act
        var act = () => throttler.RegisterNode("node1", -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Max concurrent executions cannot be negative*");

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public void RegisterNode_WithNullNodeId_ShouldThrowException()
    {
        // Arrange
        var throttler = new NodeThrottler();

        // Act
        var act = () => throttler.RegisterNode(null!, 5);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nodeId");

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_UnregisteredNode_ShouldReturnNull()
    {
        // Arrange
        var throttler = new NodeThrottler();

        // Act
        var slot = await throttler.AcquireAsync("node1");

        // Assert
        slot.Should().BeNull();

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_NodeWithZeroLimit_ShouldReturnNull()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 0); // 0 = unlimited, no throttling

        // Act
        var slot = await throttler.AcquireAsync("node1");

        // Assert
        slot.Should().BeNull(); // No throttling needed

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_WithinLimit_ShouldAcquireImmediately()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 2);

        // Act
        var slot1 = await throttler.AcquireAsync("node1");
        var slot2 = await throttler.AcquireAsync("node1");

        // Assert
        slot1.Should().NotBeNull();
        slot2.Should().NotBeNull();
        throttler.GetAvailableSlots("node1").Should().Be(0);

        // Cleanup
        slot1!.Dispose();
        slot2!.Dispose();
        throttler.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_ExceedingLimit_ShouldBlock()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 1);
        var slot1 = await throttler.AcquireAsync("node1");

        // Act
        var acquireTask = throttler.AcquireAsync("node1");
        await Task.Delay(50); // Give time to block

        // Assert
        acquireTask.IsCompleted.Should().BeFalse();
        throttler.GetAvailableSlots("node1").Should().Be(0);

        // Cleanup
        slot1!.Dispose();
        var slot2 = await acquireTask;
        slot2!.Dispose();
        throttler.Dispose();
    }

    [TestMethod]
    public async Task Release_ShouldMakeSlotAvailable()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 1);
        var slot = await throttler.AcquireAsync("node1");
        throttler.GetAvailableSlots("node1").Should().Be(0);

        // Act
        slot!.Dispose();

        // Assert
        throttler.GetAvailableSlots("node1").Should().Be(1);

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public async Task AcquireAsync_DifferentNodes_ShouldBeIndependent()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 1);
        throttler.RegisterNode("node2", 1);

        // Act
        var slot1 = await throttler.AcquireAsync("node1");
        var slot2 = await throttler.AcquireAsync("node2");

        // Assert
        slot1.Should().NotBeNull();
        slot2.Should().NotBeNull();
        throttler.GetAvailableSlots("node1").Should().Be(0);
        throttler.GetAvailableSlots("node2").Should().Be(0);

        // Cleanup
        slot1!.Dispose();
        slot2!.Dispose();
        throttler.Dispose();
    }

    [TestMethod]
    public async Task UnregisterNode_ShouldRemoveThrottling()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 1);
        var slot = await throttler.AcquireAsync("node1");

        // Act
        throttler.UnregisterNode("node1");

        // Assert
        throttler.GetMaxConcurrency("node1").Should().Be(0); // Unregistered
        var newSlot = await throttler.AcquireAsync("node1");
        newSlot.Should().BeNull(); // No throttling anymore

        // Cleanup
        slot!.Dispose();
        throttler.Dispose();
    }

    [TestMethod]
    public async Task GetMaxConcurrency_ShouldReturnCorrectLimit()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 5);

        // Assert
        throttler.GetMaxConcurrency("node1").Should().Be(5);
        throttler.GetMaxConcurrency("node2").Should().Be(0); // Unregistered

        // Cleanup
        throttler.Dispose();
    }

    [TestMethod]
    public void Dispose_ShouldCleanupAllSemaphores()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 1);
        throttler.RegisterNode("node2", 1);

        // Act
        throttler.Dispose();

        // Assert - No exception should be thrown
        var act = () => throttler.GetAvailableSlots("node1");
        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task NodeThrottleSlot_DisposeMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var throttler = new NodeThrottler();
        throttler.RegisterNode("node1", 2);
        var slot = await throttler.AcquireAsync("node1");

        // Act
        slot!.Dispose();
        slot.Dispose();
        slot.Dispose();

        // Assert
        throttler.GetAvailableSlots("node1").Should().Be(2); // Should only release once

        // Cleanup
        throttler.Dispose();
    }
}
