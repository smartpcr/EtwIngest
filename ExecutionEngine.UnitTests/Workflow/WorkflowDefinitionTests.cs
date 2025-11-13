// -----------------------------------------------------------------------
// <copyright file="WorkflowDefinitionTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowDefinitionTests
{
    [TestMethod]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var workflow = new WorkflowDefinition();

        // Assert
        workflow.WorkflowId.Should().BeEmpty();
        workflow.WorkflowName.Should().BeEmpty();
        workflow.Description.Should().BeNull();
        workflow.Version.Should().Be("1.0");
        workflow.Nodes.Should().NotBeNull().And.BeEmpty();
        workflow.Connections.Should().NotBeNull().And.BeEmpty();
        workflow.Metadata.Should().NotBeNull().And.BeEmpty();
        workflow.EntryPointNodeId.Should().BeNull();
        workflow.MaxConcurrency.Should().Be(0);
        workflow.AllowPause.Should().BeTrue();
        workflow.TimeoutSeconds.Should().Be(0);
    }

    [TestMethod]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            WorkflowName = "Test Workflow",
            Description = "A test workflow",
            Version = "2.0",
            EntryPointNodeId = "node-1",
            MaxConcurrency = 5,
            AllowPause = false,
            TimeoutSeconds = 300
        };

        // Assert
        workflow.WorkflowId.Should().Be("wf-001");
        workflow.WorkflowName.Should().Be("Test Workflow");
        workflow.Description.Should().Be("A test workflow");
        workflow.Version.Should().Be("2.0");
        workflow.EntryPointNodeId.Should().Be("node-1");
        workflow.MaxConcurrency.Should().Be(5);
        workflow.AllowPause.Should().BeFalse();
        workflow.TimeoutSeconds.Should().Be(300);
    }

    [TestMethod]
    public void Nodes_ShouldBeModifiable()
    {
        // Arrange
        var workflow = new WorkflowDefinition();
        var node1 = new NodeDefinition { NodeId = "node-1", RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript };
        var node2 = new NodeDefinition { NodeId = "node-2", RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell};

        // Act
        workflow.Nodes.Add(node1);
        workflow.Nodes.Add(node2);

        // Assert
        workflow.Nodes.Should().HaveCount(2);
        workflow.Nodes[0].Should().BeSameAs(node1);
        workflow.Nodes[1].Should().BeSameAs(node2);
    }

    [TestMethod]
    public void Connections_ShouldBeModifiable()
    {
        // Arrange
        var workflow = new WorkflowDefinition();
        var connection = new NodeConnection
        {
            SourceNodeId = "node-1",
            TargetNodeId = "node-2"
        };

        // Act
        workflow.Connections.Add(connection);

        // Assert
        workflow.Connections.Should().HaveCount(1);
        workflow.Connections[0].Should().BeSameAs(connection);
    }

    [TestMethod]
    public void Metadata_ShouldBeModifiable()
    {
        // Arrange
        var workflow = new WorkflowDefinition();

        // Act
        workflow.Metadata["author"] = "Test User";
        workflow.Metadata["created"] = DateTime.UtcNow;
        workflow.Metadata["tags"] = new[] { "test", "demo" };

        // Assert
        workflow.Metadata.Should().HaveCount(3);
        workflow.Metadata["author"].Should().Be("Test User");
        workflow.Metadata["created"].Should().BeOfType<DateTime>();
        workflow.Metadata["tags"].Should().BeEquivalentTo(new[] { "test", "demo" });
    }

    [TestMethod]
    public void CompleteWorkflow_ShouldHaveAllPropertiesSet()
    {
        // Arrange & Act
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-complete",
            WorkflowName = "Complete Workflow",
            Description = "A complete workflow with all properties",
            Version = "1.5",
            EntryPointNodeId = "start-node",
            MaxConcurrency = 10,
            AllowPause = true,
            TimeoutSeconds = 600,
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition { NodeId = "start-node", RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript, ScriptPath = "start.csx" },
                new NodeDefinition { NodeId = "process-node", RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell, ScriptPath = "process.ps1" },
                new NodeDefinition { NodeId = "end-node", RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript, ScriptPath = "end.csx" }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "start-node", TargetNodeId = "process-node" },
                new NodeConnection { SourceNodeId = "process-node", TargetNodeId = "end-node" }
            },
            Metadata = new Dictionary<string, object>
            {
                { "environment", "production" },
                { "version", 1 }
            }
        };

        // Assert
        workflow.WorkflowId.Should().Be("wf-complete");
        workflow.Nodes.Should().HaveCount(3);
        workflow.Connections.Should().HaveCount(2);
        workflow.Metadata.Should().HaveCount(2);
        workflow.AllowPause.Should().BeTrue();
    }
}
