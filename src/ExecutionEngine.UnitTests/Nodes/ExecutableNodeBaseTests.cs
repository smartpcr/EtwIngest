// -----------------------------------------------------------------------
// <copyright file="ExecutableNodeBaseTests.cs" company="Microsoft Corp.">
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
public class ExecutableNodeBaseTests
{
    [TestMethod]
    public void Initialize_ShouldStoreDefinition()
    {
        // Arrange
        var node = new TestExecutableNode();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            NodeName = "Test Node"
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.NodeId.Should().Be("test-node");
        node.NodeName.Should().Be("Test Node");
    }

    [TestMethod]
    public void Initialize_WithNullDefinition_ShouldThrowException()
    {
        // Arrange
        var node = new TestExecutableNode();

        // Act
        var act = () => node.Initialize(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void OnStart_EventRaised_ShouldContainCorrectData()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        NodeStartEventArgs? capturedArgs = null;
        node.OnStart += (sender, args) => capturedArgs = args;

        // Act
        node.TriggerOnStart();

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.NodeId.Should().Be("test-start");
        capturedArgs.NodeInstanceId.Should().NotBe(Guid.Empty);
    }

    [TestMethod]
    public void OnProgress_EventRaised_ShouldContainCorrectData()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        ProgressEventArgs? capturedArgs = null;
        node.OnProgress += (sender, args) => capturedArgs = args;

        // Act
        node.TriggerOnProgress();

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Status.Should().Be("Processing");
        capturedArgs.ProgressPercent.Should().Be(50);
    }

    [TestMethod]
    public void CreateExecutionState_ShouldSetupAllProperties()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["x"] = 10;

        // Act
        var state = node.CreateExecutionStatePublic(workflowContext, nodeContext);

        // Assert
        state.WorkflowContext.Should().BeSameAs(workflowContext);
        state.NodeContext.Should().BeSameAs(nodeContext);
        state.Input.Should().BeSameAs(nodeContext.InputData);
        state.Output.Should().BeSameAs(nodeContext.OutputData);
        state.Local.Should().BeSameAs(nodeContext.LocalVariables);
        state.GlobalVariables.Should().BeSameAs(workflowContext.Variables);
    }

    [TestMethod]
    public void CreateExecutionState_SetOutput_ShouldUpdateNodeContext()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var state = node.CreateExecutionStatePublic(workflowContext, nodeContext);
        state.SetOutput("result", 42);

        // Assert
        nodeContext.OutputData["result"].Should().Be(42);
    }

    [TestMethod]
    public void CreateExecutionState_GetInput_ShouldRetrieveFromNodeContext()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["count"] = 10;

        // Act
        var state = node.CreateExecutionStatePublic(workflowContext, nodeContext);
        var value = state.GetInput("count");

        // Assert
        value.Should().Be(10);
    }

    [TestMethod]
    public void CreateExecutionState_GetGlobal_ShouldRetrieveFromWorkflowContext()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["path"] = "/data";
        var nodeContext = new NodeExecutionContext();

        // Act
        var state = node.CreateExecutionStatePublic(workflowContext, nodeContext);
        var value = state.GetGlobal("path");

        // Assert
        value.Should().Be("/data");
    }

    [TestMethod]
    public void CreateExecutionState_SetGlobal_ShouldUpdateWorkflowContext()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var state = node.CreateExecutionStatePublic(workflowContext, nodeContext);
        state.SetGlobal("outputPath", "/output");

        // Assert
        workflowContext.Variables["outputPath"].Should().Be("/output");
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnNodeInstance()
    {
        // Arrange
        var node = new TestExecutableNode();
        node.Initialize(new NodeDefinition { NodeId = "test", NodeName = "Test" });

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.NodeId.Should().Be("test");
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
    }

    [TestMethod]
    public void NodeId_BeforeInitialize_ShouldBeEmpty()
    {
        // Arrange
        var node = new TestExecutableNode();

        // Act & Assert
        node.NodeId.Should().BeEmpty();
        node.NodeName.Should().BeEmpty();
    }
}

// Test implementation of ExecutableNodeBase for testing purposes
public class TestExecutableNode : ExecutableNodeBase
{
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        return new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = NodeId,
            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Completed,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };
    }

    // Public wrapper for testing protected method
    public ExecutionState CreateExecutionStatePublic(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext)
    {
        return CreateExecutionState(workflowContext, nodeContext);
    }

    // Test methods to trigger events
    public void TriggerOnStart()
    {
        RaiseOnStart(new NodeStartEventArgs
        {
            NodeId = "test-start",
            NodeInstanceId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });
    }

    public void TriggerOnProgress()
    {
        RaiseOnProgress(new ProgressEventArgs
        {
            Status = "Processing",
            ProgressPercent = 50
        });
    }
}
