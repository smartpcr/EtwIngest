// -----------------------------------------------------------------------
// <copyright file="PowerShellScriptNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using System.Runtime.InteropServices;
using ExecutionEngine.Contexts;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using FluentAssertions;

[TestClass]
public class PowerShellScriptNodeTests
{
    private string testScriptsDirectory = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        // Create temp directory for test scripts
        this.testScriptsDirectory = Path.Combine(Path.GetTempPath(), "PowerShellScriptNodeTests_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(this.testScriptsDirectory);
    }

    private static void RequireWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("PowerShell tests require Windows platform. Current platform: " + RuntimeInformation.OSDescription);
        }
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
        var scriptPath = Path.Combine(this.testScriptsDirectory, "simple.ps1");
        File.WriteAllText(scriptPath, @"
$State.SetOutput('result', 42)
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            NodeName = "Test Script",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
        var scriptPath = Path.Combine(this.testScriptsDirectory, "input_output.ps1");
        File.WriteAllText(scriptPath, @"
$x = [int]$State.GetInput('x')
$y = [int]$State.GetInput('y')
$State.SetOutput('sum', ($x + $y))
$State.SetOutput('product', ($x * $y))
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
        var scriptPath = Path.Combine(this.testScriptsDirectory, "globals.ps1");
        File.WriteAllText(scriptPath, @"
$config = $State.GetGlobal('config')
$State.SetGlobal('processedBy', 'PowerShell')
$State.SetOutput('config', $config)
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
        workflowContext.Variables["processedBy"].Should().Be("PowerShell");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithRuntimeError_ShouldFail()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "error.ps1");
        File.WriteAllText(scriptPath, @"
throw 'Test PowerShell error'
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNonExistentScriptFile_ShouldFail()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "nonexistent.ps1");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
        var node = new PowerShellScriptNode();
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
        var scriptPath = Path.Combine(this.testScriptsDirectory, "simple.ps1");
        File.WriteAllText(scriptPath, @"$State.SetOutput('result', 1)");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        bool eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleExecutions_ShouldReuseLoadedScript()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "reuse.ps1");
        File.WriteAllText(scriptPath, @"
$x = [int]$State.GetInput('value')
$State.SetOutput('doubled', ($x * 2))
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();

        // Act - Execute twice
        var nodeContext1 = new NodeExecutionContext();
        nodeContext1.InputData["value"] = 10;
        var instance1 = await node.ExecuteAsync(workflowContext, nodeContext1, CancellationToken.None);

        // Debug: Print error if execution failed
        if (instance1.Status == NodeExecutionStatus.Failed)
        {
            Console.WriteLine($"[ERROR] First execution failed: {instance1.ErrorMessage}");
            if (instance1.Exception != null)
            {
                Console.WriteLine($"[EXCEPTION] {instance1.Exception}");
            }
        }

        var nodeContext2 = new NodeExecutionContext();
        nodeContext2.InputData["value"] = 20;
        var instance2 = await node.ExecuteAsync(workflowContext, nodeContext2, CancellationToken.None);

        // Debug: Print error if execution failed
        if (instance2.Status == NodeExecutionStatus.Failed)
        {
            Console.WriteLine($"[ERROR] Second execution failed: {instance2.ErrorMessage}");
            if (instance2.Exception != null)
            {
                Console.WriteLine($"[EXCEPTION] {instance2.Exception}");
            }
        }

        // Assert
        instance1.Status.Should().Be(NodeExecutionStatus.Completed,
            $"First execution failed: {instance1.ErrorMessage}");
        nodeContext1.OutputData["doubled"].Should().Be(20);

        instance2.Status.Should().Be(NodeExecutionStatus.Completed,
            $"Second execution failed: {instance2.ErrorMessage}");
        nodeContext2.OutputData["doubled"].Should().Be(40);

        node.ScriptContent.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void ScriptContent_BeforeExecution_ShouldBeNull()
    {
        // Arrange
        var node = new PowerShellScriptNode();

        // Act & Assert
        node.ScriptContent.Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_WithBuiltInCmdlets_ShouldWork()
    {
        // Arrange
        var scriptPath = Path.Combine(this.testScriptsDirectory, "cmdlets.ps1");
        File.WriteAllText(scriptPath, @"
$items = @(1, 2, 3, 4, 5)
$sum = ($items | Measure-Object -Sum).Sum
$State.SetOutput('sum', $sum)
");

        var node = new PowerShellScriptNode();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
            ScriptPath = scriptPath
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["sum"].Should().Be(15);
    }
}
