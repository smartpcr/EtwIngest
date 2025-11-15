// -----------------------------------------------------------------------
// <copyright file="RetryPolicyTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Policies;

using ExecutionEngine.Enums;
using ExecutionEngine.Policies;
using FluentAssertions;

[TestClass]
public class RetryPolicyTests
{
    [TestMethod]
    public void Constructor_DefaultValues_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var policy = new RetryPolicy();

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.None);
        policy.MaxAttempts.Should().Be(3);
        policy.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(60));
        policy.Multiplier.Should().Be(2.0);
    }

    [TestMethod]
    public void CalculateDelay_FixedStrategy_ShouldReturnInitialDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Fixed,
            InitialDelay = TimeSpan.FromSeconds(2)
        };

        // Act
        var delay1 = policy.CalculateDelay(0);
        var delay2 = policy.CalculateDelay(1);
        var delay3 = policy.CalculateDelay(2);

        // Assert - all should be close to InitialDelay with jitter (±25%)
        delay1.TotalMilliseconds.Should().BeInRange(1500, 2500);
        delay2.TotalMilliseconds.Should().BeInRange(1500, 2500);
        delay3.TotalMilliseconds.Should().BeInRange(1500, 2500);
    }

    [TestMethod]
    public void CalculateDelay_ExponentialStrategy_ShouldDoubleEachTime()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Exponential,
            InitialDelay = TimeSpan.FromSeconds(1),
            Multiplier = 2.0
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);

        // Assert - exponential backoff with jitter
        // delay0 ~= 1s * 2^0 = 1s (750ms - 1250ms with jitter)
        // delay1 ~= 1s * 2^1 = 2s (1500ms - 2500ms with jitter)
        // delay2 ~= 1s * 2^2 = 4s (3000ms - 5000ms with jitter)
        delay0.TotalMilliseconds.Should().BeInRange(750, 1250);
        delay1.TotalMilliseconds.Should().BeInRange(1500, 2500);
        delay2.TotalMilliseconds.Should().BeInRange(3000, 5000);
    }

    [TestMethod]
    public void CalculateDelay_LinearStrategy_ShouldIncreaseLinearly()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Linear,
            InitialDelay = TimeSpan.FromSeconds(1)
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);

        // Assert - linear increase with jitter
        // delay0 ~= 1s * 1 = 1s (750ms - 1250ms)
        // delay1 ~= 1s * 2 = 2s (1500ms - 2500ms)
        // delay2 ~= 1s * 3 = 3s (2250ms - 3750ms)
        delay0.TotalMilliseconds.Should().BeInRange(750, 1250);
        delay1.TotalMilliseconds.Should().BeInRange(1500, 2500);
        delay2.TotalMilliseconds.Should().BeInRange(2250, 3750);
    }

    [TestMethod]
    public void CalculateDelay_NoneStrategy_ShouldReturnZero()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.None,
            InitialDelay = TimeSpan.FromSeconds(1)
        };

        // Act
        var delay = policy.CalculateDelay(5);

        // Assert - None strategy means no retry, so no delay
        delay.Should().Be(TimeSpan.Zero);
    }

    [TestMethod]
    public void CalculateDelay_ExceedsMaxDelay_ShouldCapAtMaxDelay()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Exponential,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(5),
            Multiplier = 2.0
        };

        // Act
        var delay = policy.CalculateDelay(10); // Would be 1024s without cap

        // Assert - should be capped at MaxDelay (5s) with jitter
        delay.TotalMilliseconds.Should().BeInRange(3750, 6250);
    }

    [TestMethod]
    public void ShouldRetry_NoLists_ShouldReturnTrue()
    {
        // Arrange
        var policy = new RetryPolicy();
        var exception = new InvalidOperationException("Test");

        // Act
        var shouldRetry = policy.ShouldRetry(exception);

        // Assert
        shouldRetry.Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRetry_ExceptionInDoNotRetryOn_ShouldReturnFalse()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            DoNotRetryOn = new List<Type> { typeof(ArgumentException) }
        };
        var exception = new ArgumentException("Test");

        // Act
        var shouldRetry = policy.ShouldRetry(exception);

        // Assert
        shouldRetry.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRetry_ExceptionInRetryOn_ShouldReturnTrue()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            RetryOn = new List<Type> { typeof(InvalidOperationException) }
        };
        var exception = new InvalidOperationException("Test");

        // Act
        var shouldRetry = policy.ShouldRetry(exception);

        // Assert
        shouldRetry.Should().BeTrue();
    }

    [TestMethod]
    public void ShouldRetry_ExceptionNotInRetryOn_ShouldReturnFalse()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            RetryOn = new List<Type> { typeof(InvalidOperationException) }
        };
        var exception = new ArgumentException("Test");

        // Act
        var shouldRetry = policy.ShouldRetry(exception);

        // Assert
        shouldRetry.Should().BeFalse();
    }

    [TestMethod]
    public void ShouldRetry_DoNotRetryOnTakesPrecedence_ShouldReturnFalse()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            RetryOn = new List<Type> { typeof(Exception) },
            DoNotRetryOn = new List<Type> { typeof(ArgumentException) }
        };
        var exception = new ArgumentException("Test");

        // Act
        var shouldRetry = policy.ShouldRetry(exception);

        // Assert
        shouldRetry.Should().BeFalse();
    }

    [TestMethod]
    public void Validate_ValidPolicy_ShouldNotThrow()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Exponential,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(60),
            Multiplier = 2.0
        };

        // Act
        var act = () => policy.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public void Validate_NegativeMaxAttempts_ShouldThrow()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            MaxAttempts = -1
        };

        // Act
        var act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxAttempts*");
    }

    [TestMethod]
    public void Validate_NegativeMultiplier_ShouldThrow()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            Strategy = RetryStrategy.Exponential,
            Multiplier = -1.0
        };

        // Act
        var act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Multiplier*");
    }

    [TestMethod]
    public void Validate_MaxDelayLessThanInitialDelay_ShouldThrow()
    {
        // Arrange
        var policy = new RetryPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(10),
            MaxDelay = TimeSpan.FromSeconds(5)
        };

        // Act
        var act = () => policy.Validate();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*MaxDelay*InitialDelay*");
    }
}
