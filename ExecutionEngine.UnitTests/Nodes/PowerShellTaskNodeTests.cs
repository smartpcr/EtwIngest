// -----------------------------------------------------------------------
// <copyright file="PowerShellTaskNodeTests.cs" company="Microsoft Corp.">
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
public class PowerShellTaskNodeTests
{
    private static void RequireWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Inconclusive("PowerShell tests require Windows platform. Current platform: " + RuntimeInformation.OSDescription);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_InlineScript_ExecutesSuccessfully()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            RuntimeType = "PowerShellTask",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Output 'result' 42" }
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
    public async Task ExecuteAsync_AccessInputData_UsesGetInputFunction()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "$count = Get-Input 'count'\nSet-Output 'doubled' ($count * 2)" }
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
    public async Task ExecuteAsync_AccessGlobalVariables_UsesGetGlobalFunction()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "$path = Get-Global 'basePath'\nSet-Output 'fullPath' \"$path/file.txt\"" }
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
    public async Task ExecuteAsync_SetGlobalVariables_UsesSetGlobalFunction()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Global 'counter' 100\nSet-Output 'done' $true" }
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
        workflowContext.Variables["counter"].Should().Be(100);
        nodeContext.OutputData["done"].Should().Be(true);
    }

    [TestMethod]
    public async Task ExecuteAsync_ScriptWithError_MarksAsFailed()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "throw 'Test error'" }
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
    public async Task ExecuteAsync_LoadScriptFromFile_ExecutesFileContent()
    {
        RequireWindows();

        // Arrange
        var scriptPath = Path.Combine(Path.GetTempPath(), $"test_script_{Guid.NewGuid()}.ps1");
        await File.WriteAllTextAsync(scriptPath, "Set-Output 'source' 'file'");

        try
        {
            var node = new PowerShellTaskNode();
            node.Initialize(new NodeDefinition
            {
                NodeId = "test-1",
                NodeName = "Test Node",
                RuntimeType = "PowerShellTask",
                ScriptPath = scriptPath
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
            nodeContext.OutputData["source"].Should().Be("file");
        }
        finally
        {
            if (File.Exists(scriptPath))
            {
                File.Delete(scriptPath);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_NonExistentScriptFile_MarksAsFailed()
    {
        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            RuntimeType = "PowerShellTask",
            ScriptPath = "/nonexistent/script.ps1"
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
        instance.ErrorMessage.Should().Contain("not found");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoScriptOrFile_MarksAsFailed()
    {
        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            RuntimeType = "PowerShellTask"
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
        instance.ErrorMessage.Should().Contain("must have either inline script or ScriptPath");
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleOutputs_AllSetCorrectly()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Output 'a' 1\nSet-Output 'b' 2\nSet-Output 'c' 3" }
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
        nodeContext.OutputData["a"].Should().Be(1);
        nodeContext.OutputData["b"].Should().Be(2);
        nodeContext.OutputData["c"].Should().Be(3);
    }

    [TestMethod]
    public async Task ExecuteAsync_ComplexScript_ExecutesSuccessfully()
    {
        RequireWindows();

        // Arrange
        var script = @"
            $input1 = Get-Input 'value1'
            $input2 = Get-Input 'value2'
            $sum = $input1 + $input2
            Set-Output 'sum' $sum
            Set-Output 'product' ($input1 * $input2)
            Set-Global 'lastSum' $sum
        ";

        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", script }
            }
        });

        var workflowContext = new WorkflowExecutionContext
        {
            GraphId = "test-workflow"
        };
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["value1"] = 5;
        nodeContext.InputData["value2"] = 3;

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData["sum"].Should().Be(8);
        nodeContext.OutputData["product"].Should().Be(15);
        workflowContext.Variables["lastSum"].Should().Be(8);
    }

    [TestMethod]
    public void Initialize_WithScriptInConfiguration_SetsScriptContent()
    {
        // Arrange
        var node = new PowerShellTaskNode();
        var definition = new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Output 'test' 1" }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.ScriptContent.Should().Be("Set-Output 'test' 1");
    }

    [TestMethod]
    public async Task ExecuteAsync_StringOutput_HandledCorrectly()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Output 'message' 'Hello, World!'" }
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
        nodeContext.OutputData["message"].Should().Be("Hello, World!");
    }

    [TestMethod]
    public async Task ExecuteAsync_BooleanOutput_HandledCorrectly()
    {
        RequireWindows();

        // Arrange
        var node = new PowerShellTaskNode();
        node.Initialize(new NodeDefinition
        {
            NodeId = "test-1",
            NodeName = "Test Node",
            Configuration = new Dictionary<string, object>
            {
                { "script", "Set-Output 'isValid' $true\nSet-Output 'isEmpty' $false" }
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
        nodeContext.OutputData["isValid"].Should().Be(true);
        nodeContext.OutputData["isEmpty"].Should().Be(false);
    }
}
