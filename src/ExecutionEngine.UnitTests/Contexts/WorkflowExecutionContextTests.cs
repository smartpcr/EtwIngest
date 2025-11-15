// -----------------------------------------------------------------------
// <copyright file="WorkflowExecutionContextTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Contexts;

using System.Collections.Concurrent;
using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Queue;
using ExecutionEngine.Routing;
using FluentAssertions;

[TestClass]
public class WorkflowExecutionContextTests
{
    [TestMethod]
    public void Constructor_ShouldGenerateUniqueInstanceId()
    {
        // Arrange & Act
        var context1 = new WorkflowExecutionContext();
        var context2 = new WorkflowExecutionContext();

        // Assert
        context1.InstanceId.Should().NotBe(Guid.Empty);
        context2.InstanceId.Should().NotBe(Guid.Empty);
        context1.InstanceId.Should().NotBe(context2.InstanceId);
    }

    [TestMethod]
    public void Constructor_ShouldInitializeStatusAsPending()
    {
        // Arrange & Act
        var context = new WorkflowExecutionContext();

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Pending);
    }

    [TestMethod]
    public void Constructor_ShouldSetStartTime()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var context = new WorkflowExecutionContext();
        var afterCreate = DateTime.UtcNow;

        // Assert
        context.StartTime.Should().BeOnOrAfter(beforeCreate);
        context.StartTime.Should().BeOnOrBefore(afterCreate);
    }

    [TestMethod]
    public void Variables_ShouldBeConcurrentDictionary()
    {
        // Arrange
        var context = new WorkflowExecutionContext();

        // Act
        context.Variables["key"] = "value";

        // Assert
        context.Variables.Should().BeOfType<ConcurrentDictionary<string, object>>();
        context.Variables["key"].Should().Be("value");
    }

    [TestMethod]
    public void NodeQueues_ShouldSupportMultipleNodes()
    {
        // Arrange
        var context = new WorkflowExecutionContext();
        var queue1 = new object(); // Placeholder for NodeMessageQueue
        var queue2 = new object();

        // Act
        context.NodeQueues["node-1"] = queue1;
        context.NodeQueues["node-2"] = queue2;

        // Assert
        context.NodeQueues.Count.Should().Be(2);
        context.NodeQueues["node-1"].Should().BeSameAs(queue1);
        context.NodeQueues["node-2"].Should().BeSameAs(queue2);
    }

    [TestMethod]
    public void Duration_ShouldBeNullWhenWorkflowNotCompleted()
    {
        // Arrange
        var context = new WorkflowExecutionContext();

        // Act
        var duration = context.Duration;

        // Assert
        duration.Should().BeNull();
    }

    [TestMethod]
    public void Duration_ShouldCalculateCorrectly()
    {
        // Arrange
        var context = new WorkflowExecutionContext();
        var startTime = context.StartTime;

        // Act
        context.EndTime = startTime.AddSeconds(5);
        var duration = context.Duration;

        // Assert
        duration.Should().NotBeNull();
        duration.Value.TotalSeconds.Should().BeApproximately(5, 0.001);
    }

    [TestMethod]
    public void GraphId_CanBeSet()
    {
        // Arrange
        var context = new WorkflowExecutionContext();

        // Act
        context.GraphId = "my-workflow-graph";

        // Assert
        context.GraphId.Should().Be("my-workflow-graph");
    }

    [TestMethod]
    public void Status_CanBeUpdated()
    {
        // Arrange
        var context = new WorkflowExecutionContext();

        // Act
        context.Status = WorkflowExecutionStatus.Running;

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Running);
    }

    [TestMethod]
    public void Variables_ThreadSafe_ConcurrentAccess()
    {
        // Arrange
        var context = new WorkflowExecutionContext();
        var tasks = new List<Task>();

        // Act - Write from 20 threads concurrently
        for (var i = 0; i < 20; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                context.Variables[$"var-{index}"] = $"value-{index}";
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        context.Variables.Should().HaveCount(20);
        for (var i = 0; i < 20; i++)
        {
            context.Variables[$"var-{i}"].Should().Be($"value-{i}");
        }
    }

    [TestMethod]
    public void Router_CanBeAssigned()
    {
        // Arrange
        var context = new WorkflowExecutionContext();
        var router = new MessageRouter(new DeadLetterQueue()); // Placeholder for MessageRouter

        // Act
        context.Router = router;

        // Assert
        context.Router.Should().BeSameAs(router);
    }

    [TestMethod]
    public void DeadLetterQueue_CanBeAssigned()
    {
        // Arrange
        var context = new WorkflowExecutionContext();
        var dlq = new DeadLetterQueue(); // Placeholder for DeadLetterQueue

        // Act
        context.DeadLetterQueue = dlq;

        // Assert
        context.DeadLetterQueue.Should().BeSameAs(dlq);
    }
}
