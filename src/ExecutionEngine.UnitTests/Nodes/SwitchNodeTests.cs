// -----------------------------------------------------------------------
// <copyright file="SwitchNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using FluentAssertions;

[TestClass]
public class SwitchNodeTests
{
    [TestMethod]
    public async Task ExecuteAsync_MatchingCase_SelectsCorrectPort()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"status\")",
            Cases = new Dictionary<string, string>
            {
                { "success", "SuccessPort" },
                { "failure", "FailurePort" },
                { "pending", "PendingPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["status"] = "success";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("SuccessPort");
        nodeContext.OutputData["ExpressionResult"].Should().Be("success");
        nodeContext.OutputData["MatchedCase"].Should().Be("SuccessPort");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoMatchingCase_SelectsDefaultPort()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"status\")",
            Cases = new Dictionary<string, string>
            {
                { "success", "SuccessPort" },
                { "failure", "FailurePort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["status"] = "unknown";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(SwitchNode.DefaultPort);
        nodeContext.OutputData["ExpressionResult"].Should().Be("unknown");
        nodeContext.OutputData["MatchedCase"].Should().Be("Default");
    }

    [TestMethod]
    public async Task ExecuteAsync_IntegerExpression_MatchesCorrectly()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"code\")",
            Cases = new Dictionary<string, string>
            {
                { "200", "OkPort" },
                { "404", "NotFoundPort" },
                { "500", "ErrorPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["code"] = 404;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("NotFoundPort");
        nodeContext.OutputData["ExpressionResult"].Should().Be("404");
    }

    [TestMethod]
    public async Task ExecuteAsync_CasesWithEmptyPortName_UsesKeyAsPort()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"action\")",
            Cases = new Dictionary<string, string>
            {
                { "create", "" },  // Empty port name - should use key
                { "update", "" },
                { "delete", "" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["action"] = "update";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("update");
    }

    [TestMethod]
    public async Task ExecuteAsync_ComplexExpression_EvaluatesCorrectly()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "(int)GetGlobal(\"value\") > 10 ? \"high\" : \"low\"",
            Cases = new Dictionary<string, string>
            {
                { "high", "HighValuePort" },
                { "low", "LowValuePort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["value"] = 25;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("HighValuePort");
        nodeContext.OutputData["ExpressionResult"].Should().Be("high");
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidExpression_ShouldFail()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "this is not valid C#",
            Cases = new Dictionary<string, string>
            {
                { "test", "TestPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
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
    public async Task ExecuteAsync_EmptyExpression_ShouldFail()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = string.Empty,
            Cases = new Dictionary<string, string>
            {
                { "test", "TestPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Expression is not defined");
    }

    [TestMethod]
    public async Task ExecuteAsync_NullExpressionResult_MatchesEmptyString()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"nullValue\")",
            Cases = new Dictionary<string, string>
            {
                { "", "EmptyPort" },
                { "value", "ValuePort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["nullValue"] = null!;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("EmptyPort");
        nodeContext.OutputData["ExpressionResult"].Should().Be("");
    }

    [TestMethod]
    public void Initialize_WithConfiguration_SetsProperties()
    {
        // Arrange
        var node = new SwitchNode();
        var definition = new SwitchNodeDefinition
        {
            NodeId = "switch-1",
            Configuration = new Dictionary<string, object>
            {
                { "Expression", "GetGlobal(\"type\")" },
                {
                    "Cases", new Dictionary<string, string>
                    {
                        { "typeA", "PortA" },
                        { "typeB", "PortB" }
                    }
                }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.Expression.Should().Be("GetGlobal(\"type\")");
        node.Cases.Should().ContainKey("typeA");
        node.Cases.Should().ContainKey("typeB");
        node.Cases["typeA"].Should().Be("PortA");
        node.Cases["typeB"].Should().Be("PortB");
    }

    [TestMethod]
    public void Initialize_WithObjectDictionary_ConvertsToCases()
    {
        // Arrange
        var node = new SwitchNode();
        var definition = new SwitchNodeDefinition
        {
            NodeId = "switch-1",
            Configuration = new Dictionary<string, object>
            {
                { "Expression", "GetGlobal(\"status\")" },
                {
                    "Cases", new Dictionary<string, object>
                    {
                        { "active", "ActivePort" },
                        { "inactive", "InactivePort" }
                    }
                }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.Cases.Should().ContainKey("active");
        node.Cases.Should().ContainKey("inactive");
        node.Cases["active"].Should().Be("ActivePort");
    }

    [TestMethod]
    public void Initialize_WithNullConfiguration_UsesDefaults()
    {
        // Arrange
        var node = new SwitchNode();
        var definition = new SwitchNodeDefinition
        {
            NodeId = "switch-1",
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.Expression.Should().BeEmpty();
        node.Cases.Should().BeEmpty();
    }

    [TestMethod]
    public void GetAvailablePorts_ShouldReturnAllCasePortsAndDefault()
    {
        // Arrange
        var node = new SwitchNode
        {
            Cases = new Dictionary<string, string>
            {
                { "case1", "Port1" },
                { "case2", "Port2" },
                { "case3", "Port3" }
            }
        };

        // Act
        var ports = node.GetAvailablePorts();

        // Assert
        ports.Should().NotBeNull();
        ports.Should().HaveCount(4); // 3 case ports + default
        ports.Should().Contain("Port1");
        ports.Should().Contain("Port2");
        ports.Should().Contain("Port3");
        ports.Should().Contain(SwitchNode.DefaultPort);
    }

    [TestMethod]
    public void GetAvailablePorts_WithEmptyPortNames_UsesKeysAsPorts()
    {
        // Arrange
        var node = new SwitchNode
        {
            Cases = new Dictionary<string, string>
            {
                { "create", "" },
                { "update", "" },
                { "delete", "" }
            }
        };

        // Act
        var ports = node.GetAvailablePorts();

        // Assert
        ports.Should().NotBeNull();
        ports.Should().HaveCount(4); // 3 case ports + default
        ports.Should().Contain("create");
        ports.Should().Contain("update");
        ports.Should().Contain("delete");
        ports.Should().Contain(SwitchNode.DefaultPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "\"test\"",
            Cases = new Dictionary<string, string>
            {
                { "test", "TestPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public void NodeFactory_CreateSwitchNode_ShouldSucceed()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new SwitchNodeDefinition
        {
            NodeId = "switch-1",
            Configuration = new Dictionary<string, object>
            {
                { "Expression", "\"value\"" },
                {
                    "Cases", new Dictionary<string, string>
                    {
                        { "value", "ValuePort" }
                    }
                }
            }
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<SwitchNode>();
        node.NodeId.Should().Be("switch-1");
    }

    [TestMethod]
    public async Task ExecuteAsync_CaseSensitiveMatching_OnlyMatchesExactCase()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"status\")",
            Cases = new Dictionary<string, string>
            {
                { "Success", "SuccessPort" },  // Capital S
                { "success", "LowerSuccessPort" }  // Lower s
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["status"] = "success";  // Lower case
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("LowerSuccessPort");  // Should match exact case
    }

    [TestMethod]
    public async Task ExecuteAsync_BooleanExpression_ConvertsToString()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "(int)GetGlobal(\"value\") > 5",
            Cases = new Dictionary<string, string>
            {
                { "True", "TruePort" },
                { "False", "FalsePort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["value"] = 10;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be("TruePort");
        nodeContext.OutputData["ExpressionResult"].Should().Be("True");
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleCallsWithDifferentValues_RoutesCorrectly()
    {
        // Arrange
        var node = new SwitchNode
        {
            Expression = "GetGlobal(\"state\")",
            Cases = new Dictionary<string, string>
            {
                { "running", "RunningPort" },
                { "stopped", "StoppedPort" },
                { "paused", "PausedPort" }
            }
        };
        var definition = new SwitchNodeDefinition { NodeId = "switch-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();

        // Act & Assert - First call
        workflowContext.Variables["state"] = "running";
        var instance1 = await node.ExecuteAsync(workflowContext, new NodeExecutionContext(), CancellationToken.None);
        instance1.SourcePort.Should().Be("RunningPort");

        // Act & Assert - Second call
        workflowContext.Variables["state"] = "stopped";
        var instance2 = await node.ExecuteAsync(workflowContext, new NodeExecutionContext(), CancellationToken.None);
        instance2.SourcePort.Should().Be("StoppedPort");

        // Act & Assert - Third call with no match
        workflowContext.Variables["state"] = "crashed";
        var instance3 = await node.ExecuteAsync(workflowContext, new NodeExecutionContext(), CancellationToken.None);
        instance3.SourcePort.Should().Be(SwitchNode.DefaultPort);
    }
}
