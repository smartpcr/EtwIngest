// -----------------------------------------------------------------------
// <copyright file="MessageLeaseTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Queue
{
    using ExecutionEngine.Messages;
    using ExecutionEngine.Queue;
    using FluentAssertions;

    [TestClass]
    public class MessageLeaseTests
    {
        [TestMethod]
        public void Constructor_WithValidParameters_ShouldInitialize()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var messageId = Guid.NewGuid();
            var leaseExpiry = DateTime.UtcNow.AddMinutes(5);
            var retryCount = 0;

            // Act
            var lease = new MessageLease(message, messageId, leaseExpiry, retryCount);

            // Assert
            lease.Message.Should().BeSameAs(message);
            lease.MessageId.Should().Be(messageId);
            lease.LeaseExpiry.Should().Be(leaseExpiry);
            lease.RetryCount.Should().Be(retryCount);
            lease.LeasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [TestMethod]
        public void Constructor_WithNullMessage_ShouldThrowException()
        {
            // Arrange & Act
            Action act = () => new MessageLease(null!, Guid.NewGuid(), DateTime.UtcNow, 0);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void IsExpired_WhenNotExpired_ShouldReturnFalse()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var leaseExpiry = DateTime.UtcNow.AddMinutes(5);
            var lease = new MessageLease(message, Guid.NewGuid(), leaseExpiry, 0);

            // Act & Assert
            lease.IsExpired.Should().BeFalse();
        }

        [TestMethod]
        public void IsExpired_WhenExpired_ShouldReturnTrue()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var leaseExpiry = DateTime.UtcNow.AddMilliseconds(-100); // Already expired
            var lease = new MessageLease(message, Guid.NewGuid(), leaseExpiry, 0);

            // Act & Assert
            lease.IsExpired.Should().BeTrue();
        }

        [TestMethod]
        public void TimeRemaining_WhenNotExpired_ShouldReturnPositiveDuration()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var leaseExpiry = DateTime.UtcNow.AddMinutes(5);
            var lease = new MessageLease(message, Guid.NewGuid(), leaseExpiry, 0);

            // Act & Assert
            lease.TimeRemaining.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
            lease.TimeRemaining.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [TestMethod]
        public void TimeRemaining_WhenExpired_ShouldReturnZero()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var leaseExpiry = DateTime.UtcNow.AddMilliseconds(-100);
            var lease = new MessageLease(message, Guid.NewGuid(), leaseExpiry, 0);

            // Act & Assert
            lease.TimeRemaining.Should().Be(TimeSpan.Zero);
        }

        [TestMethod]
        public void RetryCount_ShouldBeAccessible()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var lease = new MessageLease(message, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5), 3);

            // Act & Assert
            lease.RetryCount.Should().Be(3);
        }

        [TestMethod]
        public void Properties_ShouldBeReadOnly()
        {
            // Arrange
            var message = new NodeCompleteMessage { NodeId = "node-1", Timestamp = DateTime.UtcNow };
            var messageId = Guid.NewGuid();
            var leaseExpiry = DateTime.UtcNow.AddMinutes(5);
            var lease = new MessageLease(message, messageId, leaseExpiry, 0);

            // Assert - Properties should not have setters
            typeof(MessageLease).GetProperty(nameof(MessageLease.Message))!.CanWrite.Should().BeFalse();
            typeof(MessageLease).GetProperty(nameof(MessageLease.MessageId))!.CanWrite.Should().BeFalse();
            typeof(MessageLease).GetProperty(nameof(MessageLease.LeaseExpiry))!.CanWrite.Should().BeFalse();
            typeof(MessageLease).GetProperty(nameof(MessageLease.LeasedAt))!.CanWrite.Should().BeFalse();
            typeof(MessageLease).GetProperty(nameof(MessageLease.RetryCount))!.CanWrite.Should().BeFalse();
        }
    }
}
