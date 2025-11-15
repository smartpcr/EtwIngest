// -----------------------------------------------------------------------
// <copyright file="MessageRouterTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Routing;

using ExecutionEngine.Contexts;
using ExecutionEngine.Messages;
using ExecutionEngine.Queue;
using ExecutionEngine.Routing;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class MessageRouterTests
{
    [TestMethod]
    public void Constructor_WithValidDeadLetterQueue_ShouldSucceed()
    {
        // Arrange
        var dlq = new DeadLetterQueue();

        // Act
        var router = new MessageRouter(dlq);

        // Assert
        router.Should().NotBeNull();
        router.RouteCount.Should().Be(0);
    }

    [TestMethod]
    public void Constructor_WithNullDeadLetterQueue_ShouldThrowException()
    {
        // Arrange & Act
        Action act = () => new MessageRouter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void AddRoute_WithValidNodes_ShouldSucceed()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        // Assert
        var targets = router.GetTargets("node-1");
        targets.Should().HaveCount(1);
        targets[0].Should().Be("node-2");
        router.RouteCount.Should().Be(1);
    }

    [TestMethod]
    public void AddRoute_WithNullSourceNodeId_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        var act = () => router.AddRoute(new NodeConnection { SourceNodeId = null!, TargetNodeId = "node-2" });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void AddRoute_WithNullTargetNodeId_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        var act = () => router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = null! });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void AddRoute_MultipleTargets_ShouldAddAll()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-4" });

        // Assert
        var targets = router.GetTargets("node-1");
        targets.Should().HaveCount(3);
        targets.Should().Contain(new[] { "node-2", "node-3", "node-4" });
        router.RouteCount.Should().Be(3);
    }

    [TestMethod]
    public void AddRoute_DuplicateTarget_ShouldNotAddDuplicate()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" }); // Duplicate

        // Assert
        var targets = router.GetTargets("node-1");
        targets.Should().HaveCount(1);
        router.RouteCount.Should().Be(1);
    }

    [TestMethod]
    public void RemoveRoute_WithExistingRoute_ShouldReturnTrue()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        // Act
        var result = router.RemoveRoute("node-1", "node-2");

        // Assert
        result.Should().BeTrue();
        router.GetTargets("node-1").Should().BeEmpty();
    }

    [TestMethod]
    public void RemoveRoute_WithNonExistingRoute_ShouldReturnFalse()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        var result = router.RemoveRoute("node-1", "node-2");

        // Assert
        result.Should().BeFalse();
    }

    [TestMethod]
    public void GetTargets_WithNoRoutes_ShouldReturnEmpty()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        var targets = router.GetTargets("node-1");

        // Assert
        targets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithValidRoute_ShouldDeliverMessage()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Setup routes
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        // Create target queue
        var targetQueue = new NodeMessageQueue(capacity: 100);
        workflowContext.NodeQueues["node-2"] = targetQueue;

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(1);
        targetQueue.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithMultipleTargets_ShouldDeliverToAll()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Setup routes
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });

        // Create target queues
        var queue2 = new NodeMessageQueue(capacity: 100);
        var queue3 = new NodeMessageQueue(capacity: 100);
        workflowContext.NodeQueues["node-2"] = queue2;
        workflowContext.NodeQueues["node-3"] = queue3;

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(2);
        queue2.Count.Should().Be(1);
        queue3.Count.Should().Be(1);
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithNoRoutes_ShouldReturnZero()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(0);
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithMissingTargetQueue_ShouldSkipTarget()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Setup route but no queue
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(0);
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithNullMessage_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Act
        Func<Task> act = async () => await router.RouteMessageAsync(null!, workflowContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithNullWorkflowContext_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await router.RouteMessageAsync(message, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task RouteToTargetsAsync_WithSpecificTargets_ShouldDeliverToThoseOnly()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Setup routes (should be ignored)
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        // Create queues
        var queue2 = new NodeMessageQueue(capacity: 100);
        var queue3 = new NodeMessageQueue(capacity: 100);
        workflowContext.NodeQueues["node-2"] = queue2;
        workflowContext.NodeQueues["node-3"] = queue3;

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act - Route to specific targets, ignoring routing table
        var deliveryCount = await router.RouteToTargetsAsync(
            message,
            new[] { "node-3" },
            workflowContext);

        // Assert
        deliveryCount.Should().Be(1);
        queue2.Count.Should().Be(0); // Should not receive
        queue3.Count.Should().Be(1); // Should receive
    }

    [TestMethod]
    public async Task RouteToTargetsAsync_WithNullTargets_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();
        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await router.RouteToTargetsAsync(message, null!, workflowContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public void ClearRoutes_ShouldRemoveAllRoutes()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-2", TargetNodeId = "node-4" });

        // Act
        router.ClearRoutes();

        // Assert
        router.RouteCount.Should().Be(0);
        router.GetTargets("node-1").Should().BeEmpty();
        router.GetTargets("node-2").Should().BeEmpty();
    }

    [TestMethod]
    public void RouteCount_ShouldReturnTotalRoutes()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);

        // Act
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-2", TargetNodeId = "node-4" });

        // Assert
        router.RouteCount.Should().Be(3);
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithInvalidQueueType_ShouldSkipTarget()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });

        // Put an invalid object type in NodeQueues (not a NodeMessageQueue)
        workflowContext.NodeQueues["node-2"] = "invalid-queue-type";

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(0);
    }

    [TestMethod]
    public async Task RouteToTargetsAsync_WithEmptyTargetArray_ShouldThrowException()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();
        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await router.RouteToTargetsAsync(message, Array.Empty<string>(), workflowContext);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task RouteToTargetsAsync_WithInvalidQueueType_ShouldSkipTarget()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        // Put an invalid object type in NodeQueues
        workflowContext.NodeQueues["node-2"] = 12345; // Not a NodeMessageQueue

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteToTargetsAsync(message, new[] { "node-2" }, workflowContext);

        // Assert
        deliveryCount.Should().Be(0);
    }

    [TestMethod]
    public async Task RouteMessageAsync_PartialFailure_ShouldDeliverToSuccessfulTargets()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });

        // Only create one valid queue
        workflowContext.NodeQueues["node-2"] = new NodeMessageQueue(capacity: 100);
        // node-3 queue doesn't exist - should skip

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(1); // Only delivered to node-2
    }

    [TestMethod]
    public async Task RouteToTargetsAsync_PartialFailure_ShouldDeliverToSuccessfulTargets()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        workflowContext.NodeQueues["node-2"] = new NodeMessageQueue(capacity: 100);
        // node-3 doesn't exist

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteToTargetsAsync(
            message,
            new[] { "node-2", "node-3" },
            workflowContext);

        // Assert
        deliveryCount.Should().Be(1); // Only delivered to node-2
    }

    [TestMethod]
    public async Task RouteMessageAsync_WithMultipleTargetsAndPartialFailure_ShouldContinueRouting()
    {
        // Arrange
        var dlq = new DeadLetterQueue();
        var router = new MessageRouter(dlq);
        var workflowContext = new WorkflowExecutionContext();

        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-3" });
        router.AddRoute(new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-4" });

        workflowContext.NodeQueues["node-2"] = new NodeMessageQueue(capacity: 100);
        workflowContext.NodeQueues["node-3"] = "invalid"; // Will be skipped
        workflowContext.NodeQueues["node-4"] = new NodeMessageQueue(capacity: 100);

        var message = new NodeCompleteMessage
        {
            NodeId = "node-1",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var deliveryCount = await router.RouteMessageAsync(message, workflowContext);

        // Assert
        deliveryCount.Should().Be(2); // Delivered to node-2 and node-4, skipped node-3
    }
}
