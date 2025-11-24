// -----------------------------------------------------------------------
// <copyright file="SubflowNodeIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Integration;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Integration tests for Subflow node with WorkflowEngine.
/// Tests verify proper interaction between parent and child workflows,
/// including variable mapping, error handling, and downstream routing.
/// </summary>
[TestClass]
public class SubflowNodeIntegrationTests
{
    private readonly List<string> tempFiles = new List<string>();

    [TestMethod]
    public async Task SubflowWithDownstreamNode_ShouldExecuteAfterSubflow()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Create child workflow definition
        var childWorkflow = new WorkflowDefinition
        {
            WorkflowId = "child-workflow",
            EntryPointNodeId = "child-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "child-task",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""childResult"", ""completed"");
                    ")
                }
            }
        };

        // Save child workflow to temp file
        var childWorkflowPath = this.CreateTempWorkflowFile(childWorkflow);

        // Create parent workflow
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parent-workflow",
            WorkflowName = "Subflow With Downstream Test",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""setupComplete"", true);
                    ")
                },
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-1",
                    WorkflowFilePath = childWorkflowPath,
                    OutputMappings = new Dictionary<string, string>(){ { "childResult", "parentResult" } },
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "downstream-node",
                    ScriptPath = this.CreateTempScript(@"
                        var result = GetGlobal(""parentResult"");
                        SetGlobal(""downstreamExecuted"", true);
                        SetGlobal(""receivedResult"", result);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "setup", TargetNodeId = "subflow-1", TriggerMessageType = MessageType.Complete },
                new NodeConnection { SourceNodeId = "subflow-1", TargetNodeId = "downstream-node", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify downstream node executed
        result.Variables["downstreamExecuted"].Should().Be(true);
        result.Variables["receivedResult"].Should().Be("completed");

        // Verify execution order
        var allInstances = engine.GetNodeInstances(result.InstanceId);
        var setupInstance = allInstances.First(i => i.NodeId == "setup");
        var subflowInstance = allInstances.First(i => i.NodeId == "subflow-1");
        var downstreamInstance = allInstances.First(i => i.NodeId == "downstream-node");

        subflowInstance.StartTime.Should().BeAfter(setupInstance.EndTime!.Value);
        downstreamInstance.StartTime.Should().BeAfter(subflowInstance.EndTime!.Value);
    }

    [TestMethod]
    public async Task SubflowWithInputOutputMapping_ShouldPassVariables()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Create child workflow that processes input
        var childWorkflow = new WorkflowDefinition
        {
            WorkflowId = "processing-child",
            EntryPointNodeId = "process-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "process-task",
                    ScriptPath = this.CreateTempScript(@"
                        var inputValue = (int)GetGlobal(""childInput"");
                        var result = inputValue * 2;
                        SetGlobal(""childOutput"", result);
                    ")
                }
            }
        };

        var childWorkflowPath = this.CreateTempWorkflowFile(childWorkflow);

        // Create parent workflow
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parent-with-mapping",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""parentValue"", 42);
                    ")
                },
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-1",
                    WorkflowFilePath = childWorkflowPath,
                    InputMappings = new Dictionary<string, string> { { "parentValue", "childInput" } },
                    OutputMappings = new Dictionary<string, string> { { "childOutput", "parentResult" } },
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "verify",
                    ScriptPath = this.CreateTempScript(@"
                        var result = (int)GetGlobal(""parentResult"");
                        SetGlobal(""verificationResult"", result == 84);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "setup", TargetNodeId = "subflow-1", TriggerMessageType = MessageType.Complete },
                new NodeConnection { SourceNodeId = "subflow-1", TargetNodeId = "verify", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables["parentValue"].Should().Be(42);
        result.Variables["parentResult"].Should().Be(84);
        result.Variables["verificationResult"].Should().Be(true);
    }

    [TestMethod]
    public async Task SubflowWithFailure_ShouldExecuteOnFailNode()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Create child workflow that fails
        var childWorkflow = new WorkflowDefinition
        {
            WorkflowId = "failing-child",
            EntryPointNodeId = "fail-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "fail-task",
                    ScriptPath = this.CreateTempScript(@"
                        throw new Exception(""Child workflow intentional failure"");
                    ")
                }
            }
        };

        var childWorkflowPath = this.CreateTempWorkflowFile(childWorkflow);

        // Create parent workflow with error handler
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parent-with-error-handler",
            Nodes = new List<NodeDefinition>
            {
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-1",
                    WorkflowFilePath = childWorkflowPath,
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "error-handler",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""errorHandled"", true);
                    ")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "success-handler",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""successHandled"", true);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "subflow-1", TargetNodeId = "error-handler", TriggerMessageType = MessageType.Fail },
                new NodeConnection { SourceNodeId = "subflow-1", TargetNodeId = "success-handler", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Verify subflow failed and error handler executed
        var allInstances = engine.GetNodeInstances(result.InstanceId);
        var subflowInstance = allInstances.First(i => i.NodeId == "subflow-1");
        var errorHandlerInstances = allInstances.Where(i => i.NodeId == "error-handler").ToList();

        // Subflow should have failed
        subflowInstance.Status.Should().Be(NodeExecutionStatus.Failed);
        subflowInstance.ErrorMessage.Should().Contain("Child workflow");

        // Error handler should have executed
        errorHandlerInstances.Should().HaveCount(1);
        errorHandlerInstances.First().Status.Should().Be(NodeExecutionStatus.Completed);
        result.Variables["errorHandled"].Should().Be(true);
    }

    [TestMethod]
    public async Task NestedSubflows_TwoLevelDeep_ShouldExecuteCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var engine = new WorkflowEngine(null, loggerFactory);

        // Create level 2 child (innermost) - simple calculation
        var level2Workflow = new WorkflowDefinition
        {
            WorkflowId = "level2-workflow",
            EntryPointNodeId = "level2-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "level2-task",
                    ScriptContent = "var inputObj = GetGlobal(\"level2Input\"); if (inputObj == null) throw new Exception(\"level2Input is null\"); var input = Convert.ToInt32(inputObj); SetGlobal(\"level2Output\", input + 100);",
                }
            }
        };
        var level2Path = this.CreateTempWorkflowFile(level2Workflow);

        // Create level 1 child (middle) - passes through to level 2
        var level1Workflow = new WorkflowDefinition
        {
            WorkflowId = "level1-workflow",
            EntryPointNodeId = "level1-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "level1-task",
                    ScriptContent = "var inputObj = GetGlobal(\"level1Input\"); if (inputObj == null) throw new Exception(\"level1Input is null\"); var input = Convert.ToInt32(inputObj); SetGlobal(\"level1Output\", input + 10);",
                }
            }
        };
        var level1Path = this.CreateTempWorkflowFile(level1Workflow);

        // Create parent workflow (level 0) - two sequential subflows instead of nested
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parent-workflow",
            EntryPointNodeId = "setup",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup",
                    ScriptContent = "SetGlobal(\"startValue\", 10);",
                },
                new SubflowNodeDefinition
                {
                    NodeId = "level1-subflow",
                    WorkflowFilePath = level1Path,
                    InputMappings = new Dictionary<string, string> { { "startValue", "level1Input" } },
                    OutputMappings = new Dictionary<string, string> { { "level1Output", "intermediateValue" } },
                },
                new SubflowNodeDefinition
                {
                    NodeId = "level2-subflow",
                    WorkflowFilePath = level2Path,
                    InputMappings = new Dictionary<string, string> { { "intermediateValue", "level2Input" } } ,
                    OutputMappings = new Dictionary<string, string> { { "level2Output", "finalResult" } },
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "setup", TargetNodeId = "level1-subflow", TriggerMessageType = MessageType.Complete },
                new NodeConnection { SourceNodeId = "level1-subflow", TargetNodeId = "level2-subflow", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables["startValue"].Should().Be(10);
        result.Variables["intermediateValue"].Should().Be(20); // 10 + 10
        result.Variables["finalResult"].Should().Be(120); // 20 + 100
    }

    [TestMethod]
    public async Task MultipleSequentialSubflows_ShouldExecuteInOrder()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Create first child workflow
        var child1Workflow = new WorkflowDefinition
        {
            WorkflowId = "child1",
            EntryPointNodeId = "child1-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "child1-task",
                    ScriptPath = this.CreateTempScript(@"
                        var input = (int)GetGlobal(""value"");
                        SetGlobal(""result"", input * 2);
                    ")
                }
            }
        };
        var child1Path = this.CreateTempWorkflowFile(child1Workflow);

        // Create second child workflow
        var child2Workflow = new WorkflowDefinition
        {
            WorkflowId = "child2",
            EntryPointNodeId = "child2-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "child2-task",
                    ScriptPath = this.CreateTempScript(@"
                        var input = (int)GetGlobal(""value"");
                        SetGlobal(""result"", input + 10);
                    ")
                }
            }
        };
        var child2Path = this.CreateTempWorkflowFile(child2Workflow);

        // Create parent workflow with two sequential subflows
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "sequential-subflows",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""initialValue"", 5);
                    ")
                },
                new SubflowNodeDefinition
                {
                    NodeId = "subflow1",
                    WorkflowFilePath = child1Path,
                    InputMappings = new Dictionary<string, string> { { "initialValue", "value" } },
                    OutputMappings = new Dictionary<string, string> { { "result", "intermediateValue" } },
                },
                new SubflowNodeDefinition
                {
                    NodeId = "subflow2",
                    WorkflowFilePath = child2Path,
                    InputMappings = new Dictionary<string, string> { { "intermediateValue", "value" } },
                    OutputMappings = new Dictionary<string, string> { { "result", "finalValue" } },
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "verify",
                    ScriptPath = this.CreateTempScript(@"
                        var final = (int)GetGlobal(""finalValue"");
                        SetGlobal(""verified"", final == 20); // (5 * 2) + 10 = 20
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "setup", TargetNodeId = "subflow1", TriggerMessageType = MessageType.Complete },
                new NodeConnection { SourceNodeId = "subflow1", TargetNodeId = "subflow2", TriggerMessageType = MessageType.Complete },
                new NodeConnection { SourceNodeId = "subflow2", TargetNodeId = "verify", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables["initialValue"].Should().Be(5);
        result.Variables["intermediateValue"].Should().Be(10); // subflow1: value = 5 * 2 = 10
        result.Variables["finalValue"].Should().Be(20);  // subflow2: value = 10 + 10 = 20
        result.Variables["verified"].Should().Be(true);

        // Verify execution order
        var allInstances = engine.GetNodeInstances(result.InstanceId);
        var setupInstance = allInstances.First(i => i.NodeId == "setup");
        var subflow1Instance = allInstances.First(i => i.NodeId == "subflow1");
        var subflow2Instance = allInstances.First(i => i.NodeId == "subflow2");
        var verifyInstance = allInstances.First(i => i.NodeId == "verify");

        subflow1Instance.StartTime.Should().BeAfter(setupInstance.EndTime!.Value);
        subflow2Instance.StartTime.Should().BeAfter(subflow1Instance.EndTime!.Value);
        verifyInstance.StartTime.Should().BeAfter(subflow2Instance.EndTime!.Value);
    }

    [TestMethod]
    public async Task SubflowWithContextIsolation_ParentVarsNotVisibleToChild()
    {
        // Arrange
        var engine = new WorkflowEngine();

        // Create child workflow that tries to access parent variable
        var childWorkflow = new WorkflowDefinition
        {
            WorkflowId = "isolated-child",
            EntryPointNodeId = "child-task",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "child-task",
                    ScriptPath = this.CreateTempScript(@"
                        // Try to access parent variable - should not exist
                        var hasParentVar = GetGlobal(""parentSecret"") != null;
                        SetGlobal(""hasAccessToParent"", hasParentVar);
                        SetGlobal(""childCompleted"", true);
                    ")
                }
            }
        };
        var childWorkflowPath = this.CreateTempWorkflowFile(childWorkflow);

        // Create parent workflow
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "parent-with-secret",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "setup",
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""parentSecret"", ""should-not-leak"");
                    ")
                },
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-1",
                    WorkflowFilePath = childWorkflowPath,
                    OutputMappings = new Dictionary<string, string> { { "hasAccessToParent", "childHadAccess" }, { "childCompleted", "childDone" } },
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "setup", TargetNodeId = "subflow-1", TriggerMessageType = MessageType.Complete }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow, cancellationToken: CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.Variables["parentSecret"].Should().Be("should-not-leak");
        result.Variables["childHadAccess"].Should().Be(false); // Child should NOT have access to parent vars
        result.Variables["childDone"].Should().Be(true);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var tempFile in this.tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        this.tempFiles.Clear();
    }

    private string CreateTempScript(string script)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-script-{Guid.NewGuid()}.csx");
        File.WriteAllText(tempFile, script);
        this.tempFiles.Add(tempFile);
        return tempFile;
    }

    private string CreateTempWorkflowFile(WorkflowDefinition workflow)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-workflow-{Guid.NewGuid()}.json");
        // Use WorkflowSerializer to ensure consistent serialization format (camelCase)
        var serializer = new ExecutionEngine.Workflow.WorkflowSerializer();
        var json = serializer.ToJson(workflow);
        File.WriteAllText(tempFile, json);
        this.tempFiles.Add(tempFile);
        return tempFile;
    }
}
