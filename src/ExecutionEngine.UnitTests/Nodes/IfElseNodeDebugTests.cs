// -----------------------------------------------------------------------
// <copyright file="IfElseNodeDebugTests.cs" company="Microsoft Corp.">
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
public class IfElseNodeDebugTests
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
    public async Task Debug_SimpleTrue_IfElseAsEntryPoint()
    {
        // Arrange - Simplest possible test: IfElse as entry with hardcoded true condition
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "debug-simple-true",
            WorkflowName = "Debug Simple True",
            Nodes = new List<NodeDefinition>
            {
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "true" }  // Hardcoded true
                    }
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"executed\", \"true-branch\");")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"executed\", \"false-branch\");")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-node",
                    SourcePort = IfElseNode.TrueBranchPort
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-node",
                    SourcePort = IfElseNode.FalseBranchPort
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        var allVars = string.Join(", ", result.Variables.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Variables: {allVars}");

        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed, $"Variables: {allVars}");
        result.Variables.Should().ContainKey("executed", $"Variables: {allVars}");
        result.Variables["executed"].Should().Be("true-branch");
    }

    [TestMethod]
    public async Task Debug_SimpleFalse_IfElseAsEntryPoint()
    {
        // Arrange
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "debug-simple-false",
            WorkflowName = "Debug Simple False",
            Nodes = new List<NodeDefinition>
            {
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "false" }  // Hardcoded false
                    }
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "true-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"executed\", \"true-branch\");")
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "false-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"executed\", \"false-branch\");")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "true-node",
                    SourcePort = IfElseNode.TrueBranchPort
                },
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "false-node",
                    SourcePort = IfElseNode.FalseBranchPort
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        var allVars = string.Join(", ", result.Variables.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Variables: {allVars}");

        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed, $"Variables: {allVars}");
        result.Variables.Should().ContainKey("executed", $"Variables: {allVars}");
        result.Variables["executed"].Should().Be("false-branch");
    }

    [TestMethod]
    public async Task Debug_WithoutSourcePort_BasicRouting()
    {
        // Test if basic routing works when SourcePort is NOT used (no filtering)
        var engine = new WorkflowEngine();
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "debug-no-port",
            WorkflowName = "Debug No Source Port",
            Nodes = new List<NodeDefinition>
            {
                new IfElseNodeDefinition
                {
                    NodeId = "if-node",
                    Configuration = new Dictionary<string, object>
                    {
                        { "Condition", "true" }
                    }
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "next-node",
                    ScriptPath = this.CreateTempScript("SetGlobal(\"executed\", \"next-node\");")
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "if-node",
                    TargetNodeId = "next-node"
                    // No SourcePort specified - should match any output
                }
            }
        };

        // Act
        var result = await engine.StartAsync(workflow);

        // Assert
        var allVars = string.Join(", ", result.Variables.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Variables: {allVars}");

        result.Should().NotBeNull();
        result.Status.Should().Be(WorkflowExecutionStatus.Completed, $"Variables: {allVars}");
        result.Variables.Should().ContainKey("executed", $"Variables: {allVars}");
    }

    [TestMethod]
    public async Task Debug_CheckSourcePortValue()
    {
        // Test to verify what SourcePort value the IfElseNode actually sets
        var node = new IfElseNode
        {
            Condition = "true"
        };

        var definition = new IfElseNodeDefinition
        {
            NodeId = "if-1",
        };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert and debug
        Console.WriteLine($"SourcePort value: '{instance.SourcePort}'");
        Console.WriteLine($"TrueBranchPort constant: '{IfElseNode.TrueBranchPort}'");
        Console.WriteLine($"Are they equal? {instance.SourcePort == IfElseNode.TrueBranchPort}");
        Console.WriteLine($"Are they equal (ignore case)? {string.Equals(instance.SourcePort, IfElseNode.TrueBranchPort, StringComparison.OrdinalIgnoreCase)}");

        instance.SourcePort.Should().NotBeNull();
        instance.SourcePort.Should().Be(IfElseNode.TrueBranchPort);
    }

    private string CreateTempScript(string scriptContent)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_script_{Guid.NewGuid()}.csx");
        File.WriteAllText(tempFile, scriptContent);
        this.tempFiles.Add(tempFile);
        return tempFile;
    }
}
