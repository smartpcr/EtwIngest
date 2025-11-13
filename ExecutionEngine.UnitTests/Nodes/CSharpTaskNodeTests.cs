// -----------------------------------------------------------------------
// <copyright file="CSharpTaskNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using FluentAssertions;

[TestClass]
public class CSharpTaskNodeTests
{
    [TestMethod]
    public async Task ExecuteAsync_InlineScript_ExecutesSuccessfully()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "SetOutput(\"result\", 42); return new Dictionary<string, object>();" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be(42);
    }

    [TestMethod]
    public async Task ExecuteAsync_InlineScriptReturningDictionary_MergesIntOutput()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "SetOutput(\"fromSet\", 10); return new Dictionary<string, object> { { \"fromReturn\", 20 } };" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["fromSet"].Should().Be(10);
        nodeContext.OutputData["fromReturn"].Should().Be(20);
    }

    [TestMethod]
    public async Task ExecuteAsync_AccessInputData_ReadsFromPreviousNode()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "var count = (int)GetInput(\"count\"); SetOutput(\"doubled\", count * 2);" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["count"] = 10;

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["doubled"].Should().Be(20);
    }

    [TestMethod]
    public async Task ExecuteAsync_AccessGlobalVariables_ReadsFromWorkflow()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "var path = GetGlobal(\"basePath\"); SetOutput(\"fullPath\", path + \"/file.txt\");" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        workflowContext.Variables["basePath"] = "/data";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["fullPath"].Should().Be("/data/file.txt");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScriptThrowsException_MarksNodeAsFailed()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "throw new InvalidOperationException(\"Test error\");" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Test error");
    }

    [TestMethod]
    public async Task ExecuteAsync_CompiledNode_ExecutesViaDelegate()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node"
        });

        node.SetExecutor(async (state, ct) =>
        {
            state.SetOutput("result", "from-compiled");
            return new Dictionary<string, object> { { "status", "ok" } };
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be("from-compiled");
        nodeContext.OutputData["status"].Should().Be("ok");
    }

    [TestMethod]
    public async Task ExecuteAsync_CompiledNodeReturnsNull_DoesNotThrow()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node"
        });

        node.SetExecutor(async (state, ct) =>
        {
            state.SetOutput("result", "done");
            return null; // No return dictionary
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be("done");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoScriptOrExecutor_ThrowsException()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node"
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("must have either ScriptContent or compiled executor");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScriptCompilationError_MarksAsFailed()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "this is not valid C# code! syntax error here" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("compilation failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_AsyncScript_ExecutesSuccessfully()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "await Task.Delay(10); SetOutput(\"result\", \"async-completed\");" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be("async-completed");
    }

    [TestMethod]
    public async Task ExecuteAsync_ScriptAccessesLocalVariables_WorksCorrectly()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Local[\"temp\"] = 100; var val = (int)Local[\"temp\"]; SetOutput(\"result\", val * 2);" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be(200);
        nodeContext.LocalVariables["temp"].Should().Be(100);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScriptSetsGlobalVariables_UpdatesWorkflowContext()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "SetGlobal(\"sharedCounter\", 42); SetOutput(\"done\", true);" }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        workflowContext.Variables["sharedCounter"].Should().Be(42);
    }

    [TestMethod]
    public void Initialize_WithScriptInConfiguration_SetsScriptContent()
    {
        // Arrange
        var node = new CSharpTaskNode();
        var definition = new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "SetOutput(\"test\", 1);" }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.ScriptContent.Should().Be("SetOutput(\"test\", 1);");
    }

    [TestMethod]
    public void SetExecutor_WithNullExecutor_ThrowsArgumentNullException()
    {
        // Arrange
        var node = new CSharpTaskNode();

        // Act
        var act = () => node.SetExecutor(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public async Task ExecuteAsync_CompiledExecutorThrowsException_MarksAsFailed()
    {
        // Arrange
        var node = new CSharpTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node"
        });

        node.SetExecutor(async (state, ct) =>
        {
            throw new InvalidOperationException("Compiled executor error");
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Compiled executor error");
    }
}
