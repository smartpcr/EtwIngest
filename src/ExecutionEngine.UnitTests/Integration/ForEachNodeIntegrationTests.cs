// -----------------------------------------------------------------------
// <copyright file="ForEachNodeIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Integration;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;
using FluentAssertions;

/// <summary>
/// Integration tests for ForEach node with WorkflowEngine.
/// Tests verify proper interaction between ForEach nodes and child nodes,
/// including multiple instance creation and routing behavior.
/// </summary>
[TestClass]
public class ForEachNodeIntegrationTests
{
    private readonly List<string> tempFiles = new List<string>();

    [TestMethod]
    public async Task ForEachWithChildNode_ShouldCreateMultipleInstances()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "foreach-multi-instance-test",
            WorkflowName = "ForEach Multiple Instance Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""items"", new List<string> { ""apple"", ""banana"", ""cherry"" });
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "foreach-1",
                    RuntimeType = RuntimeType.ForEach,
                    Configuration = new Dictionary<string, object>
                    {
                        { "CollectionExpression", "GetGlobal(\"items\")" },
                        { "ItemVariableName", "currentItem" }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "child-node",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var item = GetInput(""currentItem"");
                        var index = GetInput(""currentItemIndex"");
                        SetOutput(""processed"", $""Item: {item}, Index: {index}"");
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "foreach-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "child-node",
                    TriggerMessageType = MessageType.Next
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify that multiple instances of child-node were created (one per iteration)
        var allInstances = engine.GetNodeInstances(result.InstanceId);
        var childInstances = allInstances.Where(i => i.NodeId == "child-node").ToList();

        childInstances.Should().HaveCount(3, "ForEach should create 3 child instances for 3 items");
        childInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify ForEach node completed
        var foreachInstances = allInstances.Where(i => i.NodeId == "foreach-1").ToList();
        foreachInstances.Should().HaveCount(1);
        foreachInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        foreachInstances[0].ExecutionContext?.OutputData["ItemsProcessed"].Should().Be(3);
    }

    [TestMethod]
    public async Task ForEachComplete_ShouldExecuteOnCompleteNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "foreach-complete-test",
            WorkflowName = "ForEach OnComplete Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""numbers"", new int[] { 1, 2, 3, 4, 5 });
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "foreach-1",
                    RuntimeType = RuntimeType.ForEach,
                    Configuration = new Dictionary<string, object>
                    {
                        { "CollectionExpression", "GetGlobal(\"numbers\")" },
                        { "ItemVariableName", "num" }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "loop-body-handler",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var num = GetInput(""num"");
                        var index = GetInput(""numIndex"");
                        var doubled = (int)num * 2;
                        SetOutput(""result"", doubled);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "aggregate-results",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var itemsProcessed = GetInput(""ItemsProcessed"");
                        SetOutput(""summary"", $""Processed {itemsProcessed} items"");
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "foreach-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "loop-body-handler",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "aggregate-results",
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

        // Verify loop-body-handler executed 5 times (inside loop)
        var loopBodyInstances = allInstances.Where(i => i.NodeId == "loop-body-handler").ToList();
        loopBodyInstances.Should().HaveCount(5, "Loop body should execute once per iteration");
        loopBodyInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify aggregate-results executed once (outside loop, after completion)
        var aggregateInstances = allInstances.Where(i => i.NodeId == "aggregate-results").ToList();
        aggregateInstances.Should().HaveCount(1, "Aggregate node should execute once after loop completes");
        aggregateInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        aggregateInstances[0].ExecutionContext?.OutputData["summary"].Should().Be("Processed 5 items");
    }

    [TestMethod]
    public async Task ForEachFail_ShouldExecuteOnFailNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "foreach-fail-test",
            WorkflowName = "ForEach OnFail Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "foreach-1",
                    RuntimeType = RuntimeType.ForEach,
                    Configuration = new Dictionary<string, object>
                    {
                        // Invalid expression to trigger failure
                        { "CollectionExpression", "this is not valid C#" },
                        { "ItemVariableName", "item" }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "error-handler",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetOutput(""handled"", true);
                        SetOutput(""message"", ""Error was handled"");
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
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

        // Verify ForEach node failed
        var foreachInstances = allInstances.Where(i => i.NodeId == "foreach-1").ToList();
        foreachInstances.Should().HaveCount(1);
        foreachInstances[0].Status.Should().Be(NodeExecutionStatus.Failed);
        foreachInstances[0].ErrorMessage.Should().Contain("compilation failed");

        // Verify error-handler executed once
        var errorHandlerInstances = allInstances.Where(i => i.NodeId == "error-handler").ToList();
        errorHandlerInstances.Should().HaveCount(1, "Error handler should execute once on failure");
        errorHandlerInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        errorHandlerInstances[0].ExecutionContext?.OutputData["handled"].Should().Be(true);
    }

    [TestMethod]
    public async Task ForEachWithIntegerArray_ShouldProcessAllItems()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "foreach-integer-test",
            WorkflowName = "ForEach Integer Array Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""values"", new int[] { 10, 20, 30 });
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "foreach-1",
                    RuntimeType = RuntimeType.ForEach,
                    Configuration = new Dictionary<string, object>
                    {
                        { "CollectionExpression", "GetGlobal(\"values\")" },
                        { "ItemVariableName", "value" }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "processor",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var value = (int)GetInput(""value"");
                        var index = (int)GetInput(""valueIndex"");
                        SetOutput(""squared"", value * value);
                        SetOutput(""index"", index);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "foreach-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "processor",
                    TriggerMessageType = MessageType.Next
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify processor executed 3 times
        var processorInstances = allInstances.Where(i => i.NodeId == "processor").ToList();
        processorInstances.Should().HaveCount(3);

        // Verify each processor instance completed successfully
        processorInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify that global variables were updated correctly
        result.Variables["value"].Should().Be(30, "Last iteration should set value to 30");
        result.Variables["valueIndex"].Should().Be(2, "Last iteration should set index to 2");
    }

    [TestMethod]
    public async Task ForEachWithEmptyCollection_ShouldNotExecuteChild()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "foreach-empty-test",
            WorkflowName = "ForEach Empty Collection Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""emptyList"", new List<string>());
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "foreach-1",
                    RuntimeType = RuntimeType.ForEach,
                    Configuration = new Dictionary<string, object>
                    {
                        { "CollectionExpression", "GetGlobal(\"emptyList\")" },
                        { "ItemVariableName", "item" }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "should-not-run",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetOutput(""executed"", true);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "completion-handler",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var itemsProcessed = GetInput(""ItemsProcessed"");
                        SetOutput(""count"", itemsProcessed);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "foreach-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "should-not-run",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "foreach-1",
                    TargetNodeId = "completion-handler",
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

        // Verify child node was NOT executed (no Next messages sent)
        var childInstances = allInstances.Where(i => i.NodeId == "should-not-run").ToList();
        childInstances.Should().BeEmpty("Child node should not execute for empty collection");

        // Verify ForEach completed successfully
        var foreachInstances = allInstances.Where(i => i.NodeId == "foreach-1").ToList();
        foreachInstances.Should().HaveCount(1);
        foreachInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        foreachInstances[0].ExecutionContext?.OutputData["ItemsProcessed"].Should().Be(0);

        // Verify completion handler executed
        var completionInstances = allInstances.Where(i => i.NodeId == "completion-handler").ToList();
        completionInstances.Should().HaveCount(1);
        completionInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        completionInstances[0].ExecutionContext?.OutputData["count"].Should().Be(0);
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
