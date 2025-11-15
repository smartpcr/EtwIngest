// -----------------------------------------------------------------------
// <copyright file="WhileNodeIntegrationTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Integration;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using ExecutionEngine.Workflow;
using FluentAssertions;

/// <summary>
/// Integration tests for While node with WorkflowEngine.
/// Tests verify proper interaction between While nodes and child nodes,
/// including condition re-evaluation and routing behavior.
/// </summary>
[TestClass]
public class WhileNodeIntegrationTests
{
    private readonly List<string> tempFiles = new List<string>();

    [TestMethod]
    public async Task WhileWithChildNode_ShouldCreateMultipleInstances()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-multi-instance-test",
            WorkflowName = "While Multiple Instance Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""counter"", 0);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "(int)GetGlobal(\"counter\") < 5" },
                        { "MaxIterations", 100 }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "loop-body",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var counter = (int)GetGlobal(""counter"");
                        SetGlobal(""counter"", counter + 1);
                        SetOutput(""iteration"", counter);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "loop-body",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "loop-body",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        // Verify that multiple instances of loop-body were created (one per iteration)
        var allInstances = engine.GetNodeInstances(result.InstanceId);
        var loopBodyInstances = allInstances.Where(i => i.NodeId == "loop-body").ToList();

        loopBodyInstances.Should().HaveCount(5, "While should create 5 loop-body instances for counter 0-4");
        loopBodyInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify While node completed
        var whileInstances = allInstances.Where(i => i.NodeId == "while-1").ToList();
        whileInstances.Should().HaveCount(1);
        whileInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        whileInstances[0].ExecutionContext?.OutputData["IterationCount"].Should().Be(5);

        // Verify final counter value
        result.Variables["counter"].Should().Be(5);
    }

    [TestMethod]
    public async Task WhileComplete_ShouldExecuteOnCompleteNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-complete-test",
            WorkflowName = "While OnComplete Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""sum"", 0);
                        SetGlobal(""iteration"", 0);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "(int)GetGlobal(\"iteration\") < 3" },
                        { "MaxIterations", 100 }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "loop-body-handler",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var iteration = (int)GetGlobal(""iteration"");
                        var sum = (int)GetGlobal(""sum"");
                        SetGlobal(""sum"", sum + iteration);
                        SetGlobal(""iteration"", iteration + 1);
                        SetOutput(""currentSum"", sum + iteration);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "aggregate-results",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var iterationCount = GetInput(""IterationCount"");
                        var finalSum = GetGlobal(""sum"");
                        SetOutput(""summary"", $""Completed {iterationCount} iterations, sum={finalSum}"");
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "loop-body-handler",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "loop-body-handler",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "aggregate-results",
                    TriggerMessageType = MessageType.Complete,
                    SourcePort = WhileNode.LoopBodyPort
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed);

        var allInstances = engine.GetNodeInstances(result.InstanceId);

        // Verify loop-body-handler executed 3 times (inside loop)
        var loopBodyInstances = allInstances.Where(i => i.NodeId == "loop-body-handler").ToList();
        loopBodyInstances.Should().HaveCount(3, "Loop body should execute 3 times");
        loopBodyInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify aggregate-results executed once (outside loop, after completion)
        var aggregateInstances = allInstances.Where(i => i.NodeId == "aggregate-results").ToList();
        aggregateInstances.Should().HaveCount(1, "Aggregate node should execute once after loop completes");
        aggregateInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        aggregateInstances[0].ExecutionContext?.OutputData["summary"].Should().Be("Completed 3 iterations, sum=3");
    }

    [TestMethod]
    public async Task WhileFail_ShouldExecuteOnFailNode()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-fail-test",
            WorkflowName = "While OnFail Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        // Invalid condition expression to trigger failure
                        { "Condition", "this is not valid C#" },
                        { "MaxIterations", 100 }
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
                    SourceNodeId = "while-1",
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

        // Verify While node failed
        var whileInstances = allInstances.Where(i => i.NodeId == "while-1").ToList();
        whileInstances.Should().HaveCount(1);
        whileInstances[0].Status.Should().Be(NodeExecutionStatus.Failed);
        whileInstances[0].ErrorMessage.Should().Contain("compilation failed");

        // Verify error-handler executed once
        var errorHandlerInstances = allInstances.Where(i => i.NodeId == "error-handler").ToList();
        errorHandlerInstances.Should().HaveCount(1, "Error handler should execute once on failure");
        errorHandlerInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        errorHandlerInstances[0].ExecutionContext?.OutputData["handled"].Should().Be(true);
    }

    [TestMethod]
    public async Task WhileMaxIterations_ShouldFail()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-maxiter-test",
            WorkflowName = "While Max Iterations Test",
            EntryPointNodeId = "while-1",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "true" },  // Always true - would be infinite
                        { "MaxIterations", 10 }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "loop-body",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        // This would run forever without max iterations
                        SetOutput(""tick"", true);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "error-handler",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetOutput(""maxIterReached"", true);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "loop-body",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "loop-body",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
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

        // Verify While node failed due to max iterations
        var whileInstances = allInstances.Where(i => i.NodeId == "while-1").ToList();
        whileInstances.Should().HaveCount(1);
        whileInstances[0].Status.Should().Be(NodeExecutionStatus.Failed);
        whileInstances[0].ErrorMessage.Should().Contain("Maximum iterations");

        // Verify loop body executed exactly 10 times before failure
        var loopBodyInstances = allInstances.Where(i => i.NodeId == "loop-body").ToList();
        loopBodyInstances.Should().HaveCount(10, "Loop body should execute max iterations before failing");

        // Verify error handler was triggered
        var errorHandlerInstances = allInstances.Where(i => i.NodeId == "error-handler").ToList();
        errorHandlerInstances.Should().HaveCount(1);
        errorHandlerInstances[0].ExecutionContext?.OutputData["maxIterReached"].Should().Be(true);
    }

    [TestMethod]
    public async Task WhileWithConditionFalse_ShouldNotExecuteBody()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-no-exec-test",
            WorkflowName = "While No Execution Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        SetGlobal(""shouldRun"", false);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "(bool)GetGlobal(\"shouldRun\")" },
                        { "MaxIterations", 100 }
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
                        var iterationCount = GetInput(""IterationCount"");
                        SetOutput(""count"", iterationCount);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "should-not-run",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
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

        // Verify loop body was NOT executed (condition was false from start)
        var bodyInstances = allInstances.Where(i => i.NodeId == "should-not-run").ToList();
        bodyInstances.Should().BeEmpty("Loop body should not execute when condition is false");

        // Verify While completed with 0 iterations
        var whileInstances = allInstances.Where(i => i.NodeId == "while-1").ToList();
        whileInstances.Should().HaveCount(1);
        whileInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        whileInstances[0].ExecutionContext?.OutputData["IterationCount"].Should().Be(0);

        // Verify completion handler executed
        var completionInstances = allInstances.Where(i => i.NodeId == "completion-handler").ToList();
        completionInstances.Should().HaveCount(1);
        completionInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        completionInstances[0].ExecutionContext?.OutputData["count"].Should().Be(0);
    }

    [TestMethod]
    public async Task WhileWithDynamicCondition_ShouldReEvaluate()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "while-dynamic-test",
            WorkflowName = "While Dynamic Condition Test",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "setup",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var items = new List<string> { ""a"", ""b"", ""c"" };
                        SetGlobal(""items"", items);
                    ")
                },
                new NodeDefinition
                {
                    NodeId = "while-1",
                    RuntimeType = RuntimeType.While,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "((List<string>)GetGlobal(\"items\")).Count > 0" },
                        { "MaxIterations", 100 }
                    }
                },
                new NodeDefinition
                {
                    NodeId = "processor",
                    RuntimeType = RuntimeType.CSharpScript,
                    ScriptPath = this.CreateTempScript(@"
                        var items = (List<string>)GetGlobal(""items"");
                        var item = items[0];
                        items.RemoveAt(0);
                        SetOutput(""processed"", item);
                    ")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "setup",
                    TargetNodeId = "while-1",
                    TriggerMessageType = MessageType.Complete
                },
                new NodeConnection
                {
                    SourceNodeId = "while-1",
                    TargetNodeId = "processor",
                    TriggerMessageType = MessageType.Next
                },
                new NodeConnection
                {
                    SourceNodeId = "processor",
                    TargetNodeId = "while-1",
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

        // Verify processor executed 3 times (once per item in list)
        var processorInstances = allInstances.Where(i => i.NodeId == "processor").ToList();
        processorInstances.Should().HaveCount(3, "Processor should execute once per item");
        processorInstances.Should().OnlyContain(i => i.Status == NodeExecutionStatus.Completed);

        // Verify While completed with 3 iterations
        var whileInstances = allInstances.Where(i => i.NodeId == "while-1").ToList();
        whileInstances.Should().HaveCount(1);
        whileInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        whileInstances[0].ExecutionContext?.OutputData["IterationCount"].Should().Be(3);

        // Verify list is now empty (all items processed)
        var items = (List<string>)result.Variables["items"];
        items.Should().BeEmpty("All items should have been removed during loop");
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
