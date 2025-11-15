// -----------------------------------------------------------------------
// <copyright file="CSharpScriptNodeTests.cs" company="Microsoft Corp.">
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
public class CSharpScriptNodeTests
{
    private string testScriptsDirectory = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        // Create temp directory for test scripts
        this.testScriptsDirectory = Path.Combine(Path.GetTempPath(), "CSharpScriptNodeTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.testScriptsDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up test scripts
        if (Directory.Exists(this.testScriptsDirectory))
        {
            Directory.Delete(this.testScriptsDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_WithSimpleScript_ShouldSucceed()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "simple.csx");
        File.WriteAllText(scriptPath, @"
SetOutput(""result"", 42);
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            NodeName = "Test Script",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["result"].Should().Be(42);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInputAndOutput_ShouldProcessCorrectly()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "input_output.csx");
        File.WriteAllText(scriptPath, @"
var x = (int)GetInput(""x"");
var y = (int)GetInput(""y"");
SetOutput(""sum"", x + y);
SetOutput(""product"", x * y);
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["x"] = 10;
        nodeContext.InputData["y"] = 5;

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["sum"].Should().Be(15);
        nodeContext.OutputData["product"].Should().Be(50);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithGlobalVariables_ShouldAccessWorkflowState()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "globals.csx");
        File.WriteAllText(scriptPath, @"
var config = (string)GetGlobal(""config"");
SetGlobal(""processedBy"", ""CSharpScript"");
SetOutput(""config"", config);
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["config"] = "production";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["config"].Should().Be("production");
        workflowContext.Variables["processedBy"].Should().Be("CSharpScript");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithCompilationError_ShouldFail()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "error.csx");
        File.WriteAllText(scriptPath, @"
int x = ""not a number""; // Type mismatch
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().NotBeNullOrEmpty();
        instance.Exception.Should().NotBeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_WithRuntimeError_ShouldFail()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "runtime_error.csx");
        File.WriteAllText(scriptPath, @"
throw new System.InvalidOperationException(""Test runtime error"");
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Test runtime error");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNonExistentScriptFile_ShouldFail()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "nonexistent.csx");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("not found");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutInitialize_ShouldFail()
    {
        // Arrange
        var node = new CSharpScriptNode();
        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("not been initialized");
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "simple.csx");
        File.WriteAllText(scriptPath, @"SetOutput(""result"", 1);");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
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
    public async Task ExecuteAsync_MultipleExecutions_ShouldReuseCompiledScript()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "reuse.csx");
        File.WriteAllText(scriptPath, @"
var x = (int)GetInput(""value"");
SetOutput(""doubled"", x * 2);
");

        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();

        // Act - Execute twice
        var nodeContext1 = new NodeExecutionContext();
        nodeContext1.InputData["value"] = 10;
        var instance1 = await node.ExecuteAsync(workflowContext, nodeContext1, CancellationToken.None);

        var nodeContext2 = new NodeExecutionContext();
        nodeContext2.InputData["value"] = 20;
        var instance2 = await node.ExecuteAsync(workflowContext, nodeContext2, CancellationToken.None);

        // Assert
        instance1.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext1.OutputData["doubled"].Should().Be(20);

        instance2.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext2.OutputData["doubled"].Should().Be(40);

        node.ScriptContent.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void ScriptContent_BeforeExecution_ShouldBeNull()
    {
        // Arrange
        var node = new CSharpScriptNode();

        // Act & Assert
        node.ScriptContent.Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_WithEmptyScriptPath_ShouldFail()
    {
        // Arrange
        var node = new CSharpScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
            ScriptPath = string.Empty // Empty path
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("ScriptPath is not defined");
    }
}
