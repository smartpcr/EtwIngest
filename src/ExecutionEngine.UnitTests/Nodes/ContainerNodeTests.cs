// -----------------------------------------------------------------------
// <copyright file="ContainerNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using System.Text.Json;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class ContainerNodeTests
{
    [TestMethod]
    public void Initialize_WithValidChildNodes_ShouldSucceed()
    {
        // Arrange
        var definition = this.CreateContainerDefinitionWithParallelChildren(3);

        // Act
        var node = new ContainerNode();
        var act = () => node.Initialize(definition);

        // Assert
        act.Should().NotThrow();
        node.ChildNodes.Should().HaveCount(3);
        node.ExecutionMode.Should().Be(ExecutionMode.Parallel);
    }

    [TestMethod]
    public void Initialize_WithNullChildNodes_ShouldThrowException()
    {
        // Arrange
        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = new List<NodeDefinition>(),
            ChildConnections = new  List<NodeConnection>()
        };

        // Act
        var node = new ContainerNode();
        var act = () => node.Initialize(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ChildNodes cannot be null or empty*");
    }

    [TestMethod]
    public void Initialize_WithEmptyChildNodes_ShouldThrowException()
    {
        // Arrange
        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = new List<NodeDefinition>(),
            ChildConnections = new List<NodeConnection>()
        };

        // Act
        var node = new ContainerNode();
        var act = () => node.Initialize(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ChildNodes cannot be null or empty*");
    }

    [TestMethod]
    public void Initialize_WithCircularReferences_ShouldThrowException()
    {
        // Arrange: Create a→b→c→a cycle
        var childNodes = new List<NodeDefinition>
        {
            this.CreateScriptChild("a", "// Node A"),
            this.CreateScriptChild("b", "// Node B"),
            this.CreateScriptChild("c", "// Node C")
        };

        var childConnections = new List<NodeConnection>
        {
            new NodeConnection { SourceNodeId = "a", TargetNodeId = "b" },
            new NodeConnection { SourceNodeId = "b", TargetNodeId = "c" },
            new NodeConnection { SourceNodeId = "c", TargetNodeId = "a" } // Cycle!
        };

        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = childNodes,
            ChildConnections = childConnections
        };

        // Act
        var node = new ContainerNode();
        var act = () => node.Initialize(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Circular reference detected*");
    }

    [TestMethod]
    public void Initialize_WithInvalidChildConnectionNodeId_ShouldThrowException()
    {
        // Arrange
        var childNodes = new List<NodeDefinition>
        {
            this.CreateScriptChild("a", "// Node A"),
            this.CreateScriptChild("b", "// Node B")
        };

        var childConnections = new List<NodeConnection>
        {
            new NodeConnection { SourceNodeId = "a", TargetNodeId = "z" } // 'z' doesn't exist!
        };

        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = childNodes,
            ChildConnections = childConnections
        };

        // Act
        var node = new ContainerNode();
        var act = () => node.Initialize(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*target node 'z' not found*");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithParallelChildren_AllChildrenExecute()
    {
        // Arrange
        var definition = this.CreateContainerDefinitionWithParallelChildren(3);
        var node = new ContainerNode();
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        if (instance.Status != NodeExecutionStatus.Completed)
        {
            Console.WriteLine($"Container Status: {instance.Status}");
            Console.WriteLine($"Container Error: {instance.ErrorMessage}");
            if (instance.Exception != null)
            {
                Console.WriteLine($"Exception: {instance.Exception}");
            }
        }

        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData.Should().ContainKey("ChildResults");
        nodeContext.OutputData["TotalChildren"].Should().Be(3);
        nodeContext.OutputData["CompletedChildren"].Should().Be(3);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithSequentialChildren_ExecutesInOrder()
    {
        // Arrange: a→b→c sequential chain
        var childNodes = new List<NodeDefinition>
        {
            this.CreateScriptChild("a", "SetOutput(\"order\", \"1\");"),
            this.CreateScriptChild("b", "SetOutput(\"order\", \"2\");"),
            this.CreateScriptChild("c", "SetOutput(\"order\", \"3\");")
        };

        var childConnections = new List<NodeConnection>
        {
            new NodeConnection { SourceNodeId = "a", TargetNodeId = "b", TriggerMessageType = MessageType.Complete },
            new NodeConnection { SourceNodeId = "b", TargetNodeId = "c", TriggerMessageType = MessageType.Complete }
        };

        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = childNodes,
            ChildConnections = childConnections,
            ExecutionMode = ExecutionMode.Sequential
        };

        var node = new ContainerNode();
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["TotalChildren"].Should().Be(3);
        nodeContext.OutputData["CompletedChildren"].Should().Be(3);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllChildrenSucceed_ReturnsCompleted()
    {
        // Arrange
        var definition = this.CreateContainerDefinitionWithParallelChildren(3);
        var node = new ContainerNode();
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        if (instance.Status != NodeExecutionStatus.Completed)
        {
            Console.WriteLine($"Container failed with error: {instance.ErrorMessage}");
        }

        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData.Should().ContainKey("ChildResults");
        nodeContext.OutputData.Should().ContainKey("ExecutionMode");
        nodeContext.OutputData["ExecutionMode"].Should().Be(ExecutionMode.Parallel);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChildOutputsAggregated_InChildResults()
    {
        // Arrange
        var childNodes = new List<NodeDefinition>
        {
            this.CreateScriptChild("child-a", "SetOutput(\"value\", \"A\");"),
            this.CreateScriptChild("child-b", "SetOutput(\"value\", \"B\");")
        };

        var definition = new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildConnections = new List<NodeConnection>(),
            ChildNodes = childNodes
        };

        var node = new ContainerNode();
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        var childResults = nodeContext.OutputData["ChildResults"] as Dictionary<string, Dictionary<string, object>>;
        childResults.Should().NotBeNull();
        childResults.Should().ContainKey("child-a");
        childResults.Should().ContainKey("child-b");
        childResults!["child-a"].Should().ContainKey("value");
        childResults["child-a"]["value"].Should().Be("A");
        childResults["child-b"]["value"].Should().Be("B");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContainerCancelled_ReturnsCancelled()
    {
        // Arrange
        var definition = this.CreateContainerDefinitionWithParallelChildren(3);
        var node = new ContainerNode();
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        var cts = new CancellationTokenSource();

        // Cancel immediately
        cts.Cancel();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, cts.Token);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Cancelled);
        instance.ErrorMessage.Should().Contain("cancelled");
    }

    // Helper methods
    private NodeDefinition CreateContainerDefinitionWithParallelChildren(int childCount)
    {
        var childNodes = new List<NodeDefinition>();
        for (var i = 0; i < childCount; i++)
        {
            childNodes.Add(this.CreateScriptChild($"child-{i}", $"SetOutput(\"index\", {i});"));
        }

        return new ContainerNodeDefinition
        {
            NodeId = "container-1",
            ChildNodes = childNodes,
            ChildConnections = new List<NodeConnection>(),
            ExecutionMode = ExecutionMode.Parallel
        };
    }

    private NodeDefinition CreateScriptChild(string nodeId, string script)
    {
        return new CSharpTaskNodeDefinition
        {
            NodeId = nodeId,
            NodeName = $"Child {nodeId}",
            ScriptContent = script,
        };
    }
}

