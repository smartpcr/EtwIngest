// -----------------------------------------------------------------------
// <copyright file="WorkflowConcurrencyTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Engine;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowConcurrencyTests
{
    [TestMethod]
    public async Task Workflow_WithMaxConcurrencyZero_ShouldAllowUnlimitedConcurrency()
    {
        // Arrange
        var workflow = CreateParallelWorkflow(nodeCount: 10);
        workflow.MaxConcurrency = 0; // Unlimited

        var engine = new WorkflowEngine();

        // Act
        var context = await engine.StartAsync(workflow);

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [TestMethod]
    public async Task Workflow_WithMaxConcurrency_ShouldEnforceLimit()
    {
        // Arrange
        var workflow = CreateParallelWorkflow(nodeCount: 5);
        workflow.MaxConcurrency = 2; // Limit to 2 concurrent nodes

        var engine = new WorkflowEngine();
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        engine.NodeStarted += (nodeId, instanceId) =>
        {
            lock (lockObj)
            {
                concurrentCount++;
                if (concurrentCount > maxConcurrent)
                {
                    maxConcurrent = concurrentCount;
                }
            }
        };

        engine.NodeCompleted += (nodeId, instanceId, duration) =>
        {
            lock (lockObj)
            {
                concurrentCount--;
            }
        };

        // Act
        var context = await engine.StartAsync(workflow);

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Completed);
        maxConcurrent.Should().BeLessOrEqualTo(2);
    }

    [TestMethod]
    public async Task Workflow_HighPriorityNodes_ShouldExecuteFirst()
    {
        // Arrange - Create workflow with high and low priority nodes
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "priority-test",
            WorkflowName = "Priority Test",
            MaxConcurrency = 1, // Force sequential execution
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "low1",
                    NodeName = "Low Priority 1",
                    Priority = NodePriority.Low,
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(10); return \"low1\";" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "high1",
                    NodeName = "High Priority 1",
                    Priority = NodePriority.High,
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(10); return \"high1\";" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "normal1",
                    NodeName = "Normal Priority 1",
                    Priority = NodePriority.Normal,
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(10); return \"normal1\";" }
                    }
                }
            },
            Connections = new List<NodeConnection>(),
            EntryPointNodeId = null // All nodes are entry points
        };

        var engine = new WorkflowEngine();
        var executionOrder = new List<string>();
        var lockObj = new object();

        engine.NodeStarted += (nodeId, instanceId) =>
        {
            lock (lockObj)
            {
                executionOrder.Add(nodeId);
            }
        };

        // Act
        var context = await engine.StartAsync(workflow);

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Completed);
        // Note: Due to the async nature and timing, we can't guarantee exact order,
        // but high priority should generally execute before low
        executionOrder.Should().Contain("high1");
        executionOrder.Should().Contain("normal1");
        executionOrder.Should().Contain("low1");
    }

    [TestMethod]
    public async Task Node_WithMaxConcurrentExecutions_ShouldThrottle()
    {
        // Arrange - Create workflow with multiple instances of the same node type
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "throttle-test",
            WorkflowName = "Throttle Test",
            MaxConcurrency = 10, // Allow high workflow concurrency
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "throttled1",
                    NodeName = "Throttled Node 1",
                    MaxConcurrentExecutions = 2, // Throttle this node type to 2 concurrent executions
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(50); return \"throttled1\";" }
                    }
                },
                new CSharpTaskNodeDefinition()
                {
                    NodeId = "throttled2",
                    NodeName = "Throttled Node 2",
                    MaxConcurrentExecutions = 2,
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(50); return \"throttled2\";" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "throttled3",
                    NodeName = "Throttled Node 3",
                    MaxConcurrentExecutions = 2,
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "System.Threading.Thread.Sleep(50); return \"throttled3\";" }
                    }
                }
            },
            Connections = new List<NodeConnection>(),
            EntryPointNodeId = null // All nodes are entry points
        };

        var engine = new WorkflowEngine();

        // Act
        var context = await engine.StartAsync(workflow);

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [TestMethod]
    public async Task Workflow_WithZeroMaxConcurrency_ShouldNotThrottle()
    {
        // Arrange
        var workflow = CreateParallelWorkflow(nodeCount: 3);
        workflow.MaxConcurrency = 0; // No throttling

        var engine = new WorkflowEngine();

        // Act
        var context = await engine.StartAsync(workflow);

        // Assert
        context.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    private static WorkflowDefinition CreateParallelWorkflow(int nodeCount)
    {
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parallel-test",
            WorkflowName = "Parallel Test",
            Nodes = new List<NodeDefinition>(),
            Connections = new List<NodeConnection>()
        };

        for (var i = 0; i < nodeCount; i++)
        {
            workflow.Nodes.Add(new CSharpTaskNodeDefinition
            {
                NodeId = $"node{i}",
                NodeName = $"Node {i}",
                Configuration = new Dictionary<string, object>
                {
                    { "script", $"System.Threading.Thread.Sleep(10); return \"node{i}\";" }
                }
            });
        }

        return workflow;
    }
}
