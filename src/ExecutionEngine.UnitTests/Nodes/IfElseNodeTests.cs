// -----------------------------------------------------------------------
// <copyright file="IfElseNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class IfElseNodeTests
{
    private readonly List<string> tempFiles = new List<string>();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var file in this.tempFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        this.tempFiles.Clear();
    }
    [TestMethod]
    public async Task ExecuteAsync_ConditionTrue_ExecutesTrueBranch()
    {
        // Arrange
        var node = new IfElseNode
        {

        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") > 5"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 10;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
        nodeContext.OutputData["BranchTaken"].Should().Be(IfElseNode.TrueBranchPort);
        nodeContext.OutputData["ConditionResult"].Should().Be(true);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionFalse_ExecutesFalseBranch()
    {
        // Arrange
        var node = new IfElseNode
        {

        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") > 5"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 3;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.FalseBranchPort);
        nodeContext.OutputData["BranchTaken"].Should().Be(IfElseNode.FalseBranchPort);
        nodeContext.OutputData["ConditionResult"].Should().Be(false);
    }

    [TestMethod]
    public async Task ExecuteAsync_ComplexBooleanExpression_EvaluatesCorrectly()
    {
        // Arrange
        var node = new IfElseNode();
        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test Complex If-Else",
            Condition = "((int)GetGlobal(\"x\") > 5 && (int)GetGlobal(\"y\") < 10) || (bool)GetGlobal(\"z\") == true"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 7;
        workflowContext.Variables["y"] = 8;
        workflowContext.Variables["z"] = false;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithStringComparison_EvaluatesCorrectly()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test String If-Else",
            Condition = "(string)GetGlobal(\"status\") == \"success\""
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["status"] = "success";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInputData_AccessesNodeContext()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test Input If-Else",
            Condition = "(int)GetInput(\"value\") > 100"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["value"] = 150;

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithEmptyCondition_ShouldFail()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = string.Empty
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Condition expression is not defined");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInvalidSyntax_ShouldFail()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") >" // Invalid syntax
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 10;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Condition compilation failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNonBooleanExpression_ShouldFail()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") + 5" // Returns int, not bool
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 10;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Condition compilation failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithRuntimeError_ShouldFail()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") / (int)GetGlobal(\"y\") > 0" // Will cause divide by zero
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 10;
        workflowContext.Variables["y"] = 0;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.Exception.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "true"
        };
        node.Initialize(definition);

        var eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public void GetAvailablePorts_ShouldReturnTrueBranchAndFalseBranch()
    {
        // Arrange
        var node = new IfElseNode
        {
            Condition = "true"
        };

        // Act
        var ports = node.GetAvailablePorts();

        // Assert
        ports.Should().HaveCount(2);
        ports.Should().Contain(IfElseNode.TrueBranchPort);
        ports.Should().Contain(IfElseNode.FalseBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConditionFromConfiguration_ShouldEvaluate()
    {
        // Arrange
        var node = new IfElseNode();
        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"x\") >= 10",
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["x"] = 10;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNull_ShouldHandleCorrectly()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "GetGlobal(\"value\") == null"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["value"] = null;
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithLinqExpression_ShouldEvaluate()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "((System.Collections.Generic.List<int>)GetGlobal(\"items\")).Any(i => i > 5)"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["items"] = new List<int> { 1, 2, 7, 4 };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleEvaluations_ShouldProduceDifferentResults()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "(int)GetGlobal(\"counter\") > 5"
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();

        // Act - First execution with counter = 3
        workflowContext.Variables["counter"] = 3;
        var nodeContext1 = new NodeExecutionContext();
        var instance1 = await node.ExecuteAsync(workflowContext, nodeContext1, CancellationToken.None);

        // Act - Second execution with counter = 10
        workflowContext.Variables["counter"] = 10;
        var nodeContext2 = new NodeExecutionContext();
        var instance2 = await node.ExecuteAsync(workflowContext, nodeContext2, CancellationToken.None);

        // Assert
        instance1.Status.Should().Be(NodeExecutionStatus.Completed);
        instance1.SourcePort.Should().Be(IfElseNode.FalseBranchPort);

        instance2.Status.Should().Be(NodeExecutionStatus.Completed);
        instance2.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    [TestMethod]
    public void Initialize_WithNullConfiguration_ShouldNotThrow()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "true",
            Configuration = null
        };

        // Act & Assert - Should not throw
        node.Initialize(definition);
        node.Condition.Should().Be("true"); // Condition should remain unchanged
    }

    [TestMethod]
    public void Initialize_WithConfigurationMissingCondition_ShouldNotOverwriteCondition()
    {
        // Arrange
        var node = new IfElseNode
        {
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Condition = "existing condition",
            Configuration = new Dictionary<string, object>
            {
                { "SomeOtherKey", "value" }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert - Condition should remain unchanged
        node.Condition.Should().Be("existing condition");
    }

    [TestMethod]
    public void Initialize_WithNullConditionValue_ShouldSetEmptyString()
    {
        // Arrange
        var node = new IfElseNode();
        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
            NodeName = "Test If-Else",
            Configuration = new Dictionary<string, object>
            {
                { "Condition", null! }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert - Should set to empty string
        node.Condition.Should().Be(string.Empty);
    }

    [TestMethod]
    public void NodeFactory_CreateIfElseNode_ShouldSucceed()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new IfElseNodeDefinition
        {
            NodeId = "test-if",
            Condition = "true",
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<IfElseNode>();
        var ifElseNode = (IfElseNode)node;
        ifElseNode.Condition.Should().Be("true");
    }

    #region Integration Tests with Workflow Engine

    [TestMethod]
    public async Task WorkflowIntegration_ConditionTrue_ExecutesTrueBranchNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "if-else-true-test",
            WorkflowName = "If-Else True Branch Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"value\", 10);")
                },
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Condition = "(int)GetGlobal(\"value\") > 5",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"result\", \"true-branch-executed\");")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"result\", \"false-branch-executed\");")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup-node",
                    TargetNodeId = "if-node",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-branch-node",
                    SourcePort = IfElseNode.TrueBranchPort,
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-branch-node",
                    SourcePort = IfElseNode.FalseBranchPort,
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();

        // Debug output
        var allVars = string.Join(", ", result.Variables.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        var status = result.Status;
        var error = result.Variables.ContainsKey("__error") ? result.Variables["__error"] : "none";
        var nodeErrors = result.Variables.ContainsKey("__node_errors") ? result.Variables["__node_errors"] : "none";

        result.Status.Should().Be(WorkflowExecutionStatus.Completed,
            $"Workflow failed with status {status}. Error: {error}. Node Errors: {nodeErrors}. All variables: {allVars}");
        result.Variables.Should().ContainKey("result",
            $"Expected 'result' key but found: {allVars}. Status: {status}, Error: {error}, NodeErrors: {nodeErrors}");
        result.Variables["result"].Should().Be("true-branch-executed");
    }

    [TestMethod]
    public async Task WorkflowIntegration_ConditionFalse_ExecutesFalseBranchNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "if-else-false-test",
            WorkflowName = "If-Else False Branch Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"value\", 3);")
                },
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Condition = "(int)GetGlobal(\"value\") > 5",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"result\", \"true-branch-executed\");")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"result\", \"false-branch-executed\");")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup-node",
                    TargetNodeId = "if-node",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-branch-node",
                    SourcePort = IfElseNode.TrueBranchPort,
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-branch-node",
                    SourcePort = IfElseNode.FalseBranchPort,
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables.Should().ContainKey("result");
        result.Variables["result"].Should().Be("false-branch-executed");
    }

    [TestMethod]
    public async Task WorkflowIntegration_ChainedNodes_CorrectExecutionOrder()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "if-else-chain-test",
            WorkflowName = "If-Else Chained Nodes Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"value\", 10); SetGlobal(\"execution\", \"setup\");")
                },
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Condition = "(int)GetGlobal(\"value\") > 5",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-branch-node",
                    ScriptPath = this.CreateTempScript(@"
                        var exec = (string)GetGlobal(""execution"");
                        SetGlobal(""execution"", exec + ""-true"");
                    ")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-branch-node",
                    ScriptPath = this.CreateTempScript(@"
                        var exec = (string)GetGlobal(""execution"");
                        SetGlobal(""execution"", exec + ""-false"");
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup-node",
                    TargetNodeId = "if-node",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-branch-node",
                    SourcePort = IfElseNode.TrueBranchPort,
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-branch-node",
                    SourcePort = IfElseNode.FalseBranchPort,
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables.Should().ContainKey("execution");
        result.Variables["execution"].Should().Be("setup-true");
    }

    [TestMethod]
    public async Task WorkflowIntegration_BothBranches_OnlySelectedExecutes()
    {
        // Arrange
        // Test 1: Condition is true
        var engine1 = new WorkflowEngine();
        var workflow1 = new WorkflowDefinition
        {
            WorkflowId = "if-else-exclusive-test-1",
            WorkflowName = "If-Else Exclusive Execution Test 1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"value\", 10);")
                },
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Condition = "(int)GetGlobal(\"value\") > 5",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"true_executed\", true);")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"false_executed\", true);")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup-node",
                    TargetNodeId = "if-node",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-branch-node",
                    SourcePort = IfElseNode.TrueBranchPort,
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-branch-node",
                    SourcePort = IfElseNode.FalseBranchPort,
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act - Test with true condition
        var result1 = await engine1.StartAsync(workflow1);

        // Assert - Only true branch should execute
        result1.Should().NotBeNull();
        result1.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result1.Variables.Should().ContainKey("true_executed");
        result1.Variables["true_executed"].Should().Be(true);
        result1.Variables.Should().NotContainKey("false_executed");

        // Test 2: Condition is false
        var engine2 = new WorkflowEngine();
        var workflow2 = new WorkflowDefinition
        {
            WorkflowId = "if-else-exclusive-test-2",
            WorkflowName = "If-Else Exclusive Execution Test 2",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"value\", 3);")
                },
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Condition = "(int)GetGlobal(\"value\") > 5",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"true_executed\", true);")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-branch-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"false_executed\", true);")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup-node",
                    TargetNodeId = "if-node",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-branch-node",
                    SourcePort = IfElseNode.TrueBranchPort,
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-branch-node",
                    SourcePort = IfElseNode.FalseBranchPort,
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act - Test with false condition
        var result2 = await engine2.StartAsync(workflow2);

        // Assert - Only false branch should execute
        result2.Should().NotBeNull();
        result2.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result2.Variables.Should().ContainKey("false_executed");
        result2.Variables["false_executed"].Should().Be(true);
        result2.Variables.Should().NotContainKey("true_executed");
    }

    #endregion

    /// <summary>
    /// Creates a temporary C# script file for testing.
    /// </summary>
    private string CreateTempScript(string scriptContent)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_script_{Guid.NewGuid()}.csx");
        File.WriteAllText(tempFile, scriptContent);
        this.tempFiles.Add(tempFile);
        return tempFile;
    }
}
