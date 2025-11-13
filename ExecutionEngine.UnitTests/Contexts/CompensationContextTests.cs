// -----------------------------------------------------------------------
// <copyright file="CompensationContextTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Contexts;

using ExecutionEngine.Contexts;
using FluentAssertions;

[TestClass]
public class CompensationContextTests
{
    [TestMethod]
    public void Constructor_ValidParameters_ShouldInitializeCorrectly()
    {
        // Arrange
        var failedNodeId = "node1";
        var exception = new InvalidOperationException("Test failure");
        var output = new { Result = "partial" };

        // Act
        var context = new CompensationContext(failedNodeId, exception, output);

        // Assert
        context.FailedNodeId.Should().Be(failedNodeId);
        context.FailureReason.Should().Be(exception);
        context.FailedNodeOutput.Should().Be(output);
        context.NodesToCompensate.Should().BeEmpty();
        context.FailureTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        context.PartialCompensation.Should().BeFalse();
        context.Metadata.Should().BeNull();
    }

    [TestMethod]
    public void Constructor_NullNodeId_ShouldThrowException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test");

        // Act
        var act = () => new CompensationContext(null!, exception);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("failedNodeId");
    }

    [TestMethod]
    public void Constructor_NullException_ShouldThrowException()
    {
        // Arrange
        var failedNodeId = "node1";

        // Act
        var act = () => new CompensationContext(failedNodeId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("failureReason");
    }

    [TestMethod]
    public void Constructor_NullOutput_ShouldAcceptNull()
    {
        // Arrange
        var failedNodeId = "node1";
        var exception = new InvalidOperationException("Test");

        // Act
        var context = new CompensationContext(failedNodeId, exception, null);

        // Assert
        context.FailedNodeOutput.Should().BeNull();
    }

    [TestMethod]
    public void AddNodeToCompensate_ValidNodeId_ShouldAddToBeginning()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));

        // Act
        context.AddNodeToCompensate("node1");
        context.AddNodeToCompensate("node2");
        context.AddNodeToCompensate("node3");

        // Assert
        context.NodesToCompensate.Should().HaveCount(3);
        context.NodesToCompensate[0].Should().Be("node3"); // Last added is first
        context.NodesToCompensate[1].Should().Be("node2");
        context.NodesToCompensate[2].Should().Be("node1");
    }

    [TestMethod]
    public void AddNodeToCompensate_NullNodeId_ShouldThrowException()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));

        // Act
        var act = () => context.AddNodeToCompensate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddNodeToCompensate_EmptyNodeId_ShouldThrowException()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));

        // Act
        var act = () => context.AddNodeToCompensate(string.Empty);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void PartialCompensation_CanBeSet()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));

        // Act
        context.PartialCompensation = true;

        // Assert
        context.PartialCompensation.Should().BeTrue();
    }

    [TestMethod]
    public void Metadata_CanBeSet()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));
        var metadata = new Dictionary<string, object>
        {
            { "Key1", "Value1" },
            { "Key2", 42 }
        };

        // Act
        context.Metadata = metadata;

        // Assert
        context.Metadata.Should().NotBeNull();
        context.Metadata.Should().HaveCount(2);
        context.Metadata!["Key1"].Should().Be("Value1");
        context.Metadata["Key2"].Should().Be(42);
    }

    [TestMethod]
    public void NodesToCompensate_ReverseOrderPreserved()
    {
        // Arrange
        var context = new CompensationContext("failed", new Exception("Test"));

        // Act - simulate workflow completion order
        context.AddNodeToCompensate("firstCompleted");
        context.AddNodeToCompensate("secondCompleted");
        context.AddNodeToCompensate("thirdCompleted");
        context.AddNodeToCompensate("lastCompleted");

        // Assert - should be in reverse order (LIFO)
        context.NodesToCompensate.Should().Equal(new[]
        {
            "lastCompleted",
            "thirdCompleted",
            "secondCompleted",
            "firstCompleted"
        });
    }

    [TestMethod]
    public void FailureTimestamp_ShouldBeSetAtCreation()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var context = new CompensationContext("failed", new Exception("Test"));
        var afterCreation = DateTime.UtcNow;

        // Assert
        context.FailureTimestamp.Should().BeOnOrAfter(beforeCreation);
        context.FailureTimestamp.Should().BeOnOrBefore(afterCreation);
    }
}
