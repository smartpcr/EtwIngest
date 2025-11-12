// -----------------------------------------------------------------------
// <copyright file="NodeInstanceTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Core;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using FluentAssertions;

[TestClass]
public class NodeInstanceTests
{
    [TestMethod]
    public void NodeInstance_TracksExecutionLifecycle()
    {
        // Arrange & Act
        var instance = new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = "test-node",
            WorkflowInstanceId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            Status = NodeExecutionStatus.Running
        };

        // Assert
        instance.NodeInstanceId.Should().NotBe(Guid.Empty);
        instance.NodeId.Should().Be("test-node");
        instance.Status.Should().Be(NodeExecutionStatus.Running);
        instance.EndTime.Should().BeNull();
    }

    [TestMethod]
    public void NodeInstance_CalculatesDuration()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var instance = new NodeInstance
        {
            StartTime = startTime,
            EndTime = startTime.AddSeconds(5)
        };

        // Act
        var duration = instance.Duration;

        // Assert
        duration.Should().NotBeNull();
        duration.Value.TotalSeconds.Should().BeApproximately(5, 0.1);
    }

    [TestMethod]
    public void Duration_ShouldBeNullWhenNotCompleted()
    {
        // Arrange
        var instance = new NodeInstance
        {
            StartTime = DateTime.UtcNow
        };

        // Act
        var duration = instance.Duration;

        // Assert
        duration.Should().BeNull();
    }

    [TestMethod]
    public void Duration_ShouldBeNullWhenStartTimeNotSet()
    {
        // Arrange
        var instance = new NodeInstance
        {
            EndTime = DateTime.UtcNow
        };

        // Act
        var duration = instance.Duration;

        // Assert
        duration.Should().BeNull();
    }

    [TestMethod]
    public void ExecutionContext_CanBeAssigned()
    {
        // Arrange
        var instance = new NodeInstance();
        var context = new NodeExecutionContext();

        // Act
        instance.ExecutionContext = context;

        // Assert
        instance.ExecutionContext.Should().BeSameAs(context);
    }

    [TestMethod]
    public void ErrorMessage_CanBeSetOnFailure()
    {
        // Arrange
        var instance = new NodeInstance
        {
            Status = NodeExecutionStatus.Failed,
            ErrorMessage = "Test error occurred"
        };

        // Act & Assert
        instance.ErrorMessage.Should().Be("Test error occurred");
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
    }

    [TestMethod]
    public void Exception_CanBeStoredOnFailure()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var instance = new NodeInstance
        {
            Status = NodeExecutionStatus.Failed,
            Exception = exception
        };

        // Act & Assert
        instance.Exception.Should().BeSameAs(exception);
        instance.Exception.Message.Should().Be("Test exception");
    }

    [TestMethod]
    public void AllStatuses_CanBeSet()
    {
        // Arrange
        var statuses = new[]
        {
            NodeExecutionStatus.Pending,
            NodeExecutionStatus.Running,
            NodeExecutionStatus.Completed,
            NodeExecutionStatus.Failed,
            NodeExecutionStatus.Cancelled,
            NodeExecutionStatus.Skipped
        };

        // Act & Assert
        foreach (var status in statuses)
        {
            var instance = new NodeInstance { Status = status };
            instance.Status.Should().Be(status);
        }
    }

    [TestMethod]
    public void WorkflowInstanceId_LinksToWorkflow()
    {
        // Arrange
        var workflowInstanceId = Guid.NewGuid();
        var instance = new NodeInstance
        {
            WorkflowInstanceId = workflowInstanceId
        };

        // Act & Assert
        instance.WorkflowInstanceId.Should().Be(workflowInstanceId);
    }

    [TestMethod]
    public void Duration_CalculatesCorrectlyForLongRunningNode()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var instance = new NodeInstance
        {
            StartTime = startTime,
            EndTime = startTime.AddMinutes(10).AddSeconds(30)
        };

        // Act
        var duration = instance.Duration;

        // Assert
        duration.Should().NotBeNull();
        duration.Value.TotalMinutes.Should().BeApproximately(10.5, 0.01);
        duration.Value.TotalSeconds.Should().BeApproximately(630, 1);
    }
}
