// -----------------------------------------------------------------------
// <copyright file="WhileNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using FluentAssertions;

[TestClass]
public class WhileNodeTests
{
    [TestMethod]
    public async Task ExecuteAsync_ConditionTrue_IteratesUntilFalse()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "(int)GetGlobal(\"counter\") < 5",
            MaxIterations = 100
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["counter"] = 0;
        var nodeContext = new NodeExecutionContext();

        var iterationCount = 0;
        node.OnNext += (sender, args) =>
        {
            // Simulate child node incrementing counter
            var counter = (int)workflowContext.Variables["counter"];
            workflowContext.Variables["counter"] = counter + 1;
            iterationCount++;
        };

        // Act - Simulate feedback loop by calling ExecuteAsync multiple times
        NodeInstance? instance;
        while ((int)workflowContext.Variables["counter"] < 5)
        {
            instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // If condition was true, we should get IterationCheck port
            if ((int)workflowContext.Variables["counter"] < 5)
            {
                instance.SourcePort.Should().Be("IterationCheck", "Should continue iteration when condition is true");
            }
        }

        // Final call when condition is false
        instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(WhileNode.LoopBodyPort, "Should use LoopBody port when loop completes");
        iterationCount.Should().Be(5); // Iterations: 0,1,2,3,4
        workflowContext.Variables["counter"].Should().Be(5);
        nodeContext.OutputData["IterationCount"].Should().Be(5);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionFalseInitially_DoesNotIterate()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "(bool)GetGlobal(\"flag\") == true"
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["flag"] = false;
        var nodeContext = new NodeExecutionContext();

        var iterationCount = 0;
        node.OnNext += (sender, args) => iterationCount++;

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        iterationCount.Should().Be(0);
        nodeContext.OutputData["IterationCount"].Should().Be(0);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxIterationsReached_StopsAndFails()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "true", // Always true - would be infinite
            MaxIterations = 10
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var iterationCount = 0;
        node.OnNext += (sender, args) => iterationCount++;

        // Act - Simulate feedback loop until max iterations reached
        NodeInstance? instance;
        for (var i = 0; i < 10; i++)
        {
            instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);
            instance.Status.Should().Be(NodeExecutionStatus.Completed, $"Iteration {i} should complete");
            instance.SourcePort.Should().Be("IterationCheck", $"Iteration {i} should use IterationCheck port");
        }

        // 11th call should fail due to max iterations
        instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        iterationCount.Should().Be(10);
        instance.ErrorMessage.Should().Contain("Maximum iterations");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionReEvaluatedEachIteration()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "((List<string>)GetGlobal(\"items\")).Count > 0"
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var items = new List<string> { "a", "b", "c" };
        workflowContext.Variables["items"] = items;
        var nodeContext = new NodeExecutionContext();

        var processedItems = new List<string>();
        node.OnNext += (sender, args) =>
        {
            // Child node removes item from list
            var list = (List<string>)workflowContext.Variables["items"];
            if (list.Count > 0)
            {
                processedItems.Add(list[0]);
                list.RemoveAt(0);
            }
        };

        // Act - Simulate feedback loop by manually re-executing WhileNode after each iteration
        NodeInstance? instance;
        while (true)
        {
            instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // Check if loop is done (SourcePort is "LoopBody" when condition becomes false)
            if (instance.SourcePort == WhileNode.LoopBodyPort)
            {
                break;
            }

            // Check if we're in iteration mode (SourcePort is "IterationCheck" when condition is true)
            if (instance.SourcePort != "IterationCheck")
            {
                break; // Unexpected state, exit
            }

            // Continue to next iteration
        }

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        processedItems.Should().Equal("a", "b", "c");
        items.Count.Should().Be(0);
        nodeContext.OutputData["IterationCount"].Should().Be(3);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnNextEvent_ContainsIterationContext()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "(int)GetGlobal(\"count\") < 3"
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["count"] = 0;
        var nodeContext = new NodeExecutionContext();

        var iterationIndices = new List<int>();
        node.OnNext += (sender, args) =>
        {
            iterationIndices.Add(args.IterationIndex);
            args.IterationContext.Should().NotBeNull();
            args.IterationContext!.InputData.Should().ContainKey("iterationIndex");

            // Increment counter
            var count = (int)workflowContext.Variables["count"];
            workflowContext.Variables["count"] = count + 1;
        };

        // Act - Simulate feedback loop by manually re-executing WhileNode after each iteration
        NodeInstance? instance;
        while (true)
        {
            instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // Check if loop is done (SourcePort is "LoopBody" when condition becomes false)
            if (instance.SourcePort == WhileNode.LoopBodyPort)
            {
                break;
            }

            // Check if we're in iteration mode (SourcePort is "IterationCheck" when condition is true)
            if (instance.SourcePort != "IterationCheck")
            {
                break; // Unexpected state, exit
            }

            // Continue to next iteration
        }

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        iterationIndices.Should().Equal(0, 1, 2);
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidCondition_ShouldFail()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "this is not valid C#"
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("compilation failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithCancellation_ShouldCancel()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "true",
            MaxIterations = 10000
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var cts = new CancellationTokenSource();

        var iterationCount = 0;
        node.OnNext += (sender, args) =>
        {
            iterationCount++;
            if (iterationCount >= 5)
            {
                cts.Cancel();
            }
        };

        // Act - Simulate feedback loop with cancellation
        NodeInstance? instance = null;
        try
        {
            while (true)
            {
                instance = await node.ExecuteAsync(workflowContext, nodeContext, cts.Token);

                // Check if loop is done
                if (instance.SourcePort == WhileNode.LoopBodyPort)
                {
                    break;
                }

                if (instance.SourcePort != "IterationCheck")
                {
                    break;
                }

                // Continue to next iteration
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation expected - instance will have Cancelled status
        }

        // Assert
        instance.Should().NotBeNull();
        instance!.Status.Should().Be(NodeExecutionStatus.Cancelled);
        iterationCount.Should().BeLessThanOrEqualTo(5);
    }

    [TestMethod]
    public void Initialize_WithConfiguration_SetsProperties()
    {
        // Arrange
        var node = new WhileNode();
        var definition = new WhileNodeDefinition
        {
            NodeId = "while-1",
            ConditionExpression = "GetGlobal(\"x\") < 10",
            MaxIterations = 10
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.Condition.Should().Be("GetGlobal(\"x\") < 10");
        node.MaxIterations.Should().Be(10);
    }

    [TestMethod]
    public void Initialize_WithNullConfiguration_UsesDefaults()
    {
        // Arrange
        var node = new WhileNode();
        var definition = new WhileNodeDefinition
        {
            NodeId = "while-1",
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.Condition.Should().BeEmpty();
        node.MaxIterations.Should().Be(WhileNode.DefaultMaxIterations);
    }

    [TestMethod]
    public void GetAvailablePorts_ShouldReturnLoopBodyPort()
    {
        // Arrange
        var node = new WhileNode();

        // Act
        var ports = node.GetAvailablePorts();

        // Assert
        ports.Should().NotBeNull();
        ports.Should().HaveCount(1);
        ports.Should().Contain(WhileNode.LoopBodyPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldSetSourcePort()
    {
        // Arrange
        var node = new WhileNode { Condition = "(int)GetGlobal(\"count\") < 2" };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["count"] = 0;
        var nodeContext = new NodeExecutionContext();

        node.OnNext += (sender, args) =>
        {
            var count = (int)workflowContext.Variables["count"];
            workflowContext.Variables["count"] = count + 1;
        };

        // Act - Simulate feedback loop by manually re-executing WhileNode after each iteration
        NodeInstance? instance;
        while (true)
        {
            instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // Check if loop is done (SourcePort is "LoopBody" when condition becomes false)
            if (instance.SourcePort == WhileNode.LoopBodyPort)
            {
                break;
            }

            // Check if we're in iteration mode (SourcePort is "IterationCheck" when condition is true)
            if (instance.SourcePort != "IterationCheck")
            {
                break; // Unexpected state, exit
            }

            // Continue to next iteration
        }

        // Assert
        instance.SourcePort.Should().Be(WhileNode.LoopBodyPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        // Arrange
        var node = new WhileNode { Condition = "(int)GetGlobal(\"count\") < 1" };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["count"] = 0;
        var nodeContext = new NodeExecutionContext();

        var eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        node.OnNext += (sender, args) =>
        {
            var count = (int)workflowContext.Variables["count"];
            workflowContext.Variables["count"] = count + 1;
        };

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_WithProgressTracking_EmitsProgress()
    {
        // Arrange
        var node = new WhileNode
        {
            Condition = "(int)GetGlobal(\"count\") < 5",
            MaxIterations = 10
        };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["count"] = 0;
        var nodeContext = new NodeExecutionContext();

        var progressCount = 0;
        node.OnProgress += (sender, args) => progressCount++;

        node.OnNext += (sender, args) =>
        {
            var count = (int)workflowContext.Variables["count"];
            workflowContext.Variables["count"] = count + 1;
        };

        // Act - Simulate feedback loop by manually re-executing WhileNode after each iteration
        while (true)
        {
            var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // Check if loop is done (SourcePort is "LoopBody" when condition becomes false)
            if (instance.SourcePort == WhileNode.LoopBodyPort)
            {
                break;
            }

            // Check if we're in iteration mode (SourcePort is "IterationCheck" when condition is true)
            if (instance.SourcePort != "IterationCheck")
            {
                break; // Unexpected state, exit
            }

            // Continue to next iteration
        }

        // Assert
        progressCount.Should().Be(5);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCondition_ShouldFail()
    {
        // Arrange
        var node = new WhileNode { Condition = string.Empty };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Condition cannot be null or empty");
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionReturnsNonBoolean_ShouldFail()
    {
        // Arrange
        var node = new WhileNode { Condition = "42" }; // Returns int, not bool
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("did not return a boolean");
    }

    [TestMethod]
    public void NodeFactory_CreateWhileNode_ShouldSucceed()
    {
        // Arrange
        var factory = new NodeFactory(AssemblyInitialize.ServiceProvider);
        var definition = new WhileNodeDefinition
        {
            NodeId = "while-1",
            ConditionExpression = "true",
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<WhileNode>();
        node!.NodeId.Should().Be("while-1");
    }

    [TestMethod]
    public async Task ExecuteAsync_OnNextEvent_ContainsMetadata()
    {
        // Arrange
        var node = new WhileNode { Condition = "(int)GetGlobal(\"count\") < 2" };
        var definition = new WhileNodeDefinition { NodeId = "while-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["count"] = 0;
        var nodeContext = new NodeExecutionContext();

        ExecutionEngine.Core.NodeNextEventArgs? firstEvent = null;
        node.OnNext += (sender, args) =>
        {
            if (firstEvent == null) firstEvent = args;

            var count = (int)workflowContext.Variables["count"];
            workflowContext.Variables["count"] = count + 1;
        };

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        firstEvent.Should().NotBeNull();
        firstEvent!.Metadata.Should().NotBeNull();
        firstEvent.Metadata!.Should().ContainKey("Condition");
        firstEvent.Metadata!.Should().ContainKey("IterationIndex");
        firstEvent.Metadata!["IterationIndex"].Should().Be(0);
    }
}
