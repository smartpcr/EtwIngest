// -----------------------------------------------------------------------
// <copyright file="SwitchNodeIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Integration;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

/// <summary>
/// Integration tests for Switch node with WorkflowEngine.
/// Tests verify proper branching behavior with multiple cases,
/// downstream node execution, and port-based routing.
/// </summary>
[TestClass]
public class SwitchNodeIntegrationTests
{
    private readonly List<string> tempFiles = new List<string>();

    [TestMethod]
    public async Task SwitchWithMultipleBranches_ShouldRouteToCorrectBranch()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-branches-test",
            WorkflowName = "Switch Multiple Branches Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "setup",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"status\", \"success\");" }
                    }
                },
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Expression", "GetGlobal(\"status\")" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "success", "SuccessPort" },
                                { "failure", "FailurePort" },
                                { "pending", "PendingPort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "success-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"branch\", \"success\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "failure-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"branch\", \"failure\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "pending-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"branch\", \"pending\");" }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "switch-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "SuccessPort",
                    TargetNodeId = "success-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "FailurePort",
                    TargetNodeId = "failure-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "PendingPort",
                    TargetNodeId = "pending-handler",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify switch completed successfully
        var switchInstances = allInstances.Where(i => i.NodeId == "switch-1").ToList();
        switchInstances.Should().HaveCount(1);
        switchInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        switchInstances[0].SourcePort.Should().Be("SuccessPort");

        // Verify ONLY success-handler executed (correct branch)
        var successInstances = allInstances.Where(i => i.NodeId == "success-handler").ToList();
        successInstances.Should().HaveCount(1, "Success handler should execute for success status");
        successInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        successInstances[0].ExecutionContext?.OutputData["branch"].Should().Be("success");

        // Verify failure-handler did NOT execute
        var failureInstances = allInstances.Where(i => i.NodeId == "failure-handler").ToList();
        failureInstances.Should().BeEmpty("Failure handler should not execute");

        // Verify pending-handler did NOT execute
        var pendingInstances = allInstances.Where(i => i.NodeId == "pending-handler").ToList();
        pendingInstances.Should().BeEmpty("Pending handler should not execute");
    }

    [TestMethod]
    public async Task SwitchWithDefaultCase_ShouldRouteToDefault()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-default-test",
            WorkflowName = "Switch Default Case Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "setup",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"status\", \"unknown\");" }
                    }
                },
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Expression", "GetGlobal(\"status\")" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "success", "SuccessPort" },
                                { "failure", "FailurePort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "success-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"branch\", \"success\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "default-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"branch\", \"default\");" }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "switch-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "SuccessPort",
                    TargetNodeId = "success-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "Default",
                    TargetNodeId = "default-handler",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify switch completed with Default port
        var switchInstances = allInstances.Where(i => i.NodeId == "switch-1").ToList();
        switchInstances.Should().HaveCount(1);
        switchInstances[0].SourcePort.Should().Be("Default");

        // Verify default-handler executed
        var defaultInstances = allInstances.Where(i => i.NodeId == "default-handler").ToList();
        defaultInstances.Should().HaveCount(1, "Default handler should execute for unmatched case");
        defaultInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        defaultInstances[0].ExecutionContext?.OutputData["branch"].Should().Be("default");

        // Verify success-handler did NOT execute
        var successInstances = allInstances.Where(i => i.NodeId == "success-handler").ToList();
        successInstances.Should().BeEmpty("Success handler should not execute");
    }

    [TestMethod]
    public async Task SwitchWithDownstreamNode_ShouldExecuteAfterBranch()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-downstream-test",
            WorkflowName = "Switch Downstream Node Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "setup",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"type\", \"A\");" }
                    }
                },
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Expression", "GetGlobal(\"type\")" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "A", "TypeAPort" },
                                { "B", "TypeBPort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "type-a-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"processed\", \"typeA\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "type-b-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"processed\", \"typeB\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "downstream-aggregator",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", @"
                            var processed = GetGlobal(""processed"");
                            SetOutput(""result"", $""Aggregated: {processed}"");
                        " }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "switch-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "TypeAPort",
                    TargetNodeId = "type-a-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "TypeBPort",
                    TargetNodeId = "type-b-handler",
                    TriggerMessageType = MessageType.Complete
                },
                // Both branch handlers connect to downstream aggregator
                new NodeConnection
                {
                    SourceNodeId = "type-a-handler",
                    TargetNodeId = "downstream-aggregator",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "type-b-handler",
                    TargetNodeId = "downstream-aggregator",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify type-a-handler executed (correct branch)
        var typeAInstances = allInstances.Where(i => i.NodeId == "type-a-handler").ToList();
        typeAInstances.Should().HaveCount(1);
        typeAInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);

        // Verify type-b-handler did NOT execute
        var typeBInstances = allInstances.Where(i => i.NodeId == "type-b-handler").ToList();
        typeBInstances.Should().BeEmpty();

        // Verify downstream aggregator executed AFTER the branch handler
        var aggregatorInstances = allInstances.Where(i => i.NodeId == "downstream-aggregator").ToList();
        aggregatorInstances.Should().HaveCount(1, "Downstream node should execute after branch");
        aggregatorInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        aggregatorInstances[0].ExecutionContext?.OutputData["result"].Should().Be("Aggregated: typeA");

        // Verify execution order: setup -> switch -> type-a-handler -> aggregator
        var setupEnd = allInstances.First(i => i.NodeId == "setup").EndTime!.Value;
        var switchEnd = allInstances.First(i => i.NodeId == "switch-1").EndTime!.Value;
        var handlerEnd = allInstances.First(i => i.NodeId == "type-a-handler").EndTime!.Value;
        var aggregatorEnd = allInstances.First(i => i.NodeId == "downstream-aggregator").EndTime!.Value;

        switchEnd.Should().BeAfter(setupEnd);
        handlerEnd.Should().BeAfter(switchEnd);
        aggregatorEnd.Should().BeAfter(handlerEnd);
    }

    [TestMethod]
    public async Task SwitchWithIntegerExpression_ShouldMatchCorrectly()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-integer-test",
            WorkflowName = "Switch Integer Expression Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "setup",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"code\", 200);" }
                    }
                },
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Expression", "GetGlobal(\"code\")" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "200", "OkPort" },
                                { "404", "NotFoundPort" },
                                { "500", "ErrorPort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "ok-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"status\", \"OK\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "notfound-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"status\", \"Not Found\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "error-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"status\", \"Server Error\");" }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "switch-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "OkPort",
                    TargetNodeId = "ok-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "NotFoundPort",
                    TargetNodeId = "notfound-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "ErrorPort",
                    TargetNodeId = "error-handler",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify switch matched code 200 -> OkPort
        var switchInstances = allInstances.Where(i => i.NodeId == "switch-1").ToList();
        switchInstances[0].SourcePort.Should().Be("OkPort");
        switchInstances[0].ExecutionContext?.OutputData["ExpressionResult"].Should().Be("200");

        // Verify ok-handler executed
        var okInstances = allInstances.Where(i => i.NodeId == "ok-handler").ToList();
        okInstances.Should().HaveCount(1);
        okInstances[0].ExecutionContext?.OutputData["status"].Should().Be("OK");

        // Verify other handlers did NOT execute
        allInstances.Where(i => i.NodeId == "notfound-handler").Should().BeEmpty();
        allInstances.Where(i => i.NodeId == "error-handler").Should().BeEmpty();
    }

    [TestMethod]
    public async Task SwitchFail_ShouldExecuteOnFailNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-fail-test",
            WorkflowName = "Switch OnFail Test",
            Nodes = new List<NodeDefinition>
            {
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        // Invalid expression to trigger failure
                        { "Expression", "this is not valid C#" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "test", "TestPort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "error-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"errorHandled\", true);" }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    TargetNodeId = "error-handler",
                    TriggerMessageType = MessageType.Fail
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Failed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify Switch node failed
        var switchInstances = allInstances.Where(i => i.NodeId == "switch-1").ToList();
        switchInstances.Should().HaveCount(1);
        switchInstances[0].Status.Should().Be(NodeExecutionStatus.Failed);
        switchInstances[0].ErrorMessage.Should().Contain("compilation failed");

        // Verify error-handler executed
        var errorInstances = allInstances.Where(i => i.NodeId == "error-handler").ToList();
        errorInstances.Should().HaveCount(1, "Error handler should execute on switch failure");
        errorInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        errorInstances[0].ExecutionContext?.OutputData["errorHandled"].Should().Be(true);
    }

    [TestMethod]
    public async Task SwitchWithConditionalExpression_ShouldEvaluateAndRoute()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "switch-conditional-test",
            WorkflowName = "Switch Conditional Expression Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "setup",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetGlobal(\"value\", 75);" }
                    }
                },
                new SwitchNodeDefinition
                {
                    NodeId = "switch-1",
                    Configuration = new Dictionary<string, object>
                    {
                        // Conditional expression that returns a category
                        { "Expression", "(int)GetGlobal(\"value\") >= 90 ? \"excellent\" : (int)GetGlobal(\"value\") >= 70 ? \"good\" : \"poor\"" },
                        {
                            "Cases", new Dictionary<string, string>
                            {
                                { "excellent", "ExcellentPort" },
                                { "good", "GoodPort" },
                                { "poor", "PoorPort" }
                            }
                        }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "excellent-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"grade\", \"A\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "good-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"grade\", \"B\");" }
                    }
                },
                new CSharpTaskNodeDefinition
                {
                    NodeId = "poor-handler",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "SetOutput(\"grade\", \"C\");" }
                    }
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "switch-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "ExcellentPort",
                    TargetNodeId = "excellent-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "GoodPort",
                    TargetNodeId = "good-handler",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "switch-1",
                    SourcePort = "PoorPort",
                    TargetNodeId = "poor-handler",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify switch evaluated to "good" (value 75 is >= 70 but < 90)
        var switchInstances = allInstances.Where(i => i.NodeId == "switch-1").ToList();
        switchInstances[0].SourcePort.Should().Be("GoodPort");
        switchInstances[0].ExecutionContext?.OutputData["ExpressionResult"].Should().Be("good");

        // Verify good-handler executed
        var goodInstances = allInstances.Where(i => i.NodeId == "good-handler").ToList();
        goodInstances.Should().HaveCount(1);
        goodInstances[0].ExecutionContext?.OutputData["grade"].Should().Be("B");

        // Verify other handlers did NOT execute
        allInstances.Where(i => i.NodeId == "excellent-handler").Should().BeEmpty();
        allInstances.Where(i => i.NodeId == "poor-handler").Should().BeEmpty();
    }

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

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up temp script files
        foreach (var file in this.tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        this.tempFiles.Clear();
    }
}
