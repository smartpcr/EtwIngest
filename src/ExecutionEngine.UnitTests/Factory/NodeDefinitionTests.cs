// -----------------------------------------------------------------------
// <copyright file="NodeDefinitionTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Factory;

using ExecutionEngine.Nodes.Definitions;
using FluentAssertions;

[TestClass]
public class NodeDefinitionTests
{
    [TestMethod]
    public void NodeDefinition_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var definition = new CSharpNodeDefinition
        {
            NodeId = "node-1",
            NodeName = "Test Node",
            AssemblyPath = "/path/to/assembly.dll",
            TypeName = "MyNamespace.MyNode",
            Configuration = new Dictionary<string, object> { { "timeout", 30 } }
        };

        // Assert
        definition.NodeId.Should().Be("node-1");
        definition.NodeName.Should().Be("Test Node");
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.CSharp);
        definition.AssemblyPath.Should().Be("/path/to/assembly.dll");
        definition.TypeName.Should().Be("MyNamespace.MyNode");
        definition.Configuration.Should().ContainKey("timeout");
    }

    [TestMethod]
    public void NodeDefinition_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var definition = new NoopNodeDefinition();

        // Assert
        definition.NodeId.Should().BeEmpty();
        definition.NodeName.Should().BeEmpty();
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.Noop);
        definition.Description.Should().BeNull();
        definition.Configuration.Should().BeNull();
    }

    [TestMethod]
    public void NodeDefinition_ForCSharpNode_ShouldHaveAssemblyAndTypeName()
    {
        // Arrange & Act
        var definition = new CSharpNodeDefinition
        {
            NodeId = "csharp-node",
            NodeName = "C# Task Node",
            AssemblyPath = "/assemblies/MyTasks.dll",
            TypeName = "MyTasks.CustomTask"
        };

        // Assert
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.CSharp);
        definition.AssemblyPath.Should().NotBeNullOrEmpty();
        definition.TypeName.Should().NotBeNullOrEmpty();
        definition.Description.Should().BeNull();
    }

    [TestMethod]
    public void NodeDefinition_ForPowerShellNode_ShouldHaveScriptPath()
    {
        // Arrange & Act
        var definition = new PowerShellScriptNodeDefinition
        {
            NodeId = "ps-node",
            NodeName = "PowerShell Task Node",
            ScriptPath = "/scripts/task.ps1",
            RequiredModules = new List<string> { "ActiveDirectory", "SqlServer" },
            ModulePaths = new Dictionary<string, string>
            {
                { "CustomModule", "/modules/CustomModule" }
            }
        };

        // Assert
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.PowerShell);
        definition.ScriptPath.Should().NotBeNullOrEmpty();
        definition.RequiredModules.Should().HaveCount(2);
        definition.ModulePaths.Should().ContainKey("CustomModule");
    }

    [TestMethod]
    public void NodeDefinition_Configuration_CanStoreArbitraryData()
    {
        // Arrange & Act
        var definition = new NoopNodeDefinition
        {
            NodeId = "config-node",
            Configuration = new Dictionary<string, object>
            {
                { "timeout", 60 },
                { "retryCount", 3 },
                { "enableLogging", true },
                { "endpoint", "https://api.example.com" },
                { "tags", new List<string> { "important", "production" } }
            }
        };

        // Assert
        definition.Configuration.Should().HaveCount(5);
        definition.Configuration["timeout"].Should().Be(60);
        definition.Configuration["retryCount"].Should().Be(3);
        definition.Configuration["enableLogging"].Should().Be(true);
        definition.Configuration["endpoint"].Should().Be("https://api.example.com");
        definition.Configuration["tags"].Should().BeOfType<List<string>>();
    }
}
