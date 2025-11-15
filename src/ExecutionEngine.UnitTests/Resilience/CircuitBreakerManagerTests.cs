// -----------------------------------------------------------------------
// <copyright file="CircuitBreakerManagerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Resilience;

using ExecutionEngine.Enums;
using ExecutionEngine.Policies;
using ExecutionEngine.Resilience;
using FluentAssertions;

[TestClass]
public class CircuitBreakerManagerTests
{
    [TestMethod]
    public void RegisterNode_ValidPolicy_ShouldRegisterSuccessfully()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();

        // Act
        var act = () => manager.RegisterNode("node1", policy);

        // Assert
        act.Should().NotThrow();
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.Dispose();
    }

    [TestMethod]
    public void RegisterNode_NullNodeId_ShouldThrowException()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();

        // Act
        var act = () => manager.RegisterNode(null!, policy);

        // Assert
        act.Should().Throw<ArgumentNullException>();
        manager.Dispose();
    }

    [TestMethod]
    public void RegisterNode_NullPolicy_ShouldThrowException()
    {
        // Arrange
        var manager = new CircuitBreakerManager();

        // Act
        var act = () => manager.RegisterNode("node1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
        manager.Dispose();
    }

    [TestMethod]
    public void AllowRequest_NoCircuitBreaker_ShouldAllowRequest()
    {
        // Arrange
        var manager = new CircuitBreakerManager();

        // Act
        var allowed = manager.AllowRequest("node1");

        // Assert
        allowed.Should().BeTrue();
        manager.Dispose();
    }

    [TestMethod]
    public void AllowRequest_ClosedCircuit_ShouldAllowRequest()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();
        manager.RegisterNode("node1", policy);

        // Act
        var allowed = manager.AllowRequest("node1");

        // Assert
        allowed.Should().BeTrue();
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.Dispose();
    }

    [TestMethod]
    public void RecordSuccess_InClosedState_ShouldIncrementCounters()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();
        manager.RegisterNode("node1", policy);

        // Act
        manager.RecordSuccess("node1");
        manager.RecordSuccess("node1");

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.GetFailureRate("node1").Should().Be(0);
        manager.Dispose();
    }

    [TestMethod]
    public void RecordFailure_BelowThreshold_ShouldStayClosed()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50, // 50%
            MinimumThroughput = 10
        };
        manager.RegisterNode("node1", policy);

        // Act - 4 failures out of 10 = 40%
        for (var i = 0; i < 6; i++)
        {
            manager.RecordSuccess("node1");
        }
        for (var i = 0; i < 4; i++)
        {
            manager.RecordFailure("node1");
        }

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.GetFailureRate("node1").Should().Be(40);
        manager.Dispose();
    }

    [TestMethod]
    public void RecordFailure_ExceedsThreshold_ShouldOpenCircuit()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50, // 50%
            MinimumThroughput = 10
        };
        manager.RegisterNode("node1", policy);

        // Act - 6 failures out of 10 = 60%
        for (var i = 0; i < 4; i++)
        {
            manager.RecordSuccess("node1");
        }
        for (var i = 0; i < 6; i++)
        {
            manager.RecordFailure("node1");
        }

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Open);
        manager.GetFailureRate("node1").Should().Be(0); // Metrics reset when circuit opens
        manager.Dispose();
    }

    [TestMethod]
    public void AllowRequest_OpenCircuit_ShouldBlockRequest()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50,
            MinimumThroughput = 2
        };
        manager.RegisterNode("node1", policy);

        // Open the circuit
        manager.RecordFailure("node1");
        manager.RecordFailure("node1");

        // Act
        var allowed = manager.AllowRequest("node1");

        // Assert
        allowed.Should().BeFalse();
        manager.GetState("node1").Should().Be(CircuitState.Open);
        manager.Dispose();
    }

    [TestMethod]
    public async Task AllowRequest_OpenCircuitAfterDuration_ShouldTransitionToHalfOpen()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50,
            MinimumThroughput = 2,
            OpenDuration = TimeSpan.FromMilliseconds(100)
        };
        manager.RegisterNode("node1", policy);

        // Open the circuit
        manager.RecordFailure("node1");
        manager.RecordFailure("node1");

        // Act - wait for OpenDuration
        await Task.Delay(150);
        var allowed = manager.AllowRequest("node1");

        // Assert
        allowed.Should().BeTrue();
        manager.GetState("node1").Should().Be(CircuitState.HalfOpen);
        manager.Dispose();
    }

    [TestMethod]
    public void RecordSuccess_InHalfOpenState_WithEnoughSuccesses_ShouldCloseCircuit()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50,
            MinimumThroughput = 2,
            OpenDuration = TimeSpan.FromMilliseconds(1),
            HalfOpenSuccesses = 3
        };
        manager.RegisterNode("node1", policy);

        // Open the circuit
        manager.RecordFailure("node1");
        manager.RecordFailure("node1");

        // Wait and transition to HalfOpen
        Thread.Sleep(10);
        manager.AllowRequest("node1");

        // Act - record enough successes in HalfOpen
        manager.RecordSuccess("node1");
        manager.RecordSuccess("node1");
        manager.RecordSuccess("node1");

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.Dispose();
    }

    [TestMethod]
    public void RecordFailure_InHalfOpenState_ShouldReopenCircuit()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50,
            MinimumThroughput = 2,
            OpenDuration = TimeSpan.FromMilliseconds(1)
        };
        manager.RegisterNode("node1", policy);

        // Open the circuit
        manager.RecordFailure("node1");
        manager.RecordFailure("node1");

        // Wait and transition to HalfOpen
        Thread.Sleep(10);
        manager.AllowRequest("node1");

        // Act - fail in HalfOpen
        manager.RecordFailure("node1");

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Open);
        manager.Dispose();
    }

    [TestMethod]
    public void Reset_ShouldCloseCircuit()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            FailureThreshold = 50,
            MinimumThroughput = 2
        };
        manager.RegisterNode("node1", policy);

        // Open the circuit
        manager.RecordFailure("node1");
        manager.RecordFailure("node1");

        // Act
        manager.Reset("node1");

        // Assert
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.GetFailureRate("node1").Should().Be(0);
        manager.Dispose();
    }

    [TestMethod]
    public void GetFailureRate_NoRequests_ShouldReturnZero()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();
        manager.RegisterNode("node1", policy);

        // Act
        var rate = manager.GetFailureRate("node1");

        // Assert
        rate.Should().Be(0);
        manager.Dispose();
    }

    [TestMethod]
    public void GetFailureRate_MixedResults_ShouldCalculateCorrectly()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy
        {
            MinimumThroughput = 100 // Prevent opening
        };
        manager.RegisterNode("node1", policy);

        // Act - 7 failures out of 10 = 70%
        for (var i = 0; i < 3; i++)
        {
            manager.RecordSuccess("node1");
        }
        for (var i = 0; i < 7; i++)
        {
            manager.RecordFailure("node1");
        }

        // Assert
        manager.GetFailureRate("node1").Should().Be(70);
        manager.Dispose();
    }

    [TestMethod]
    public void Dispose_ShouldClearAllStates()
    {
        // Arrange
        var manager = new CircuitBreakerManager();
        var policy = new CircuitBreakerPolicy();
        manager.RegisterNode("node1", policy);
        manager.RegisterNode("node2", policy);

        // Act
        manager.Dispose();

        // Assert - after dispose, should behave as if no circuit breakers registered
        manager.GetState("node1").Should().Be(CircuitState.Closed);
        manager.GetState("node2").Should().Be(CircuitState.Closed);
    }
}
