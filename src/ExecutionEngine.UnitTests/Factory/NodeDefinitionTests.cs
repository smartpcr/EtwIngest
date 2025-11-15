// -----------------------------------------------------------------------
// <copyright file="NodeDefinitionTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Factory;

using ExecutionEngine.Factory;
using FluentAssertions;

[TestClass]
public class NodeDefinitionTests
{
    [TestMethod]
    public void NodeDefinition_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var definition = new NodeDefinition
        {
            NodeId = "node-1",
            NodeName = "Test Node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
            AssemblyPath = "/path/to/assembly.dll",
            TypeName = "MyNamespace.MyNode",
            ScriptPath = "/path/to/script.ps1",
            RequiredModules = new List<string> { "Module1", "Module2" },
            ModulePaths = new Dictionary<string, string> { { "Module1", "/path/to/module1" } },
            Configuration = new Dictionary<string, object> { { "timeout", 30 } }
        };

        // Assert
        definition.NodeId.Should().Be("node-1");
        definition.NodeName.Should().Be("Test Node");
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.CSharp);
        definition.AssemblyPath.Should().Be("/path/to/assembly.dll");
        definition.TypeName.Should().Be("MyNamespace.MyNode");
        definition.ScriptPath.Should().Be("/path/to/script.ps1");
        definition.RequiredModules.Should().HaveCount(2);
        definition.RequiredModules.Should().Contain("Module1");
        definition.ModulePaths.Should().ContainKey("Module1");
        definition.Configuration.Should().ContainKey("timeout");
    }

    [TestMethod]
    public void NodeDefinition_DefaultValues_ShouldBeEmpty()
    {
        // Arrange & Act
        var definition = new NodeDefinition();

        // Assert
        definition.NodeId.Should().BeEmpty();
        definition.NodeName.Should().BeEmpty();
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.CSharpScript);
        definition.AssemblyPath.Should().BeNull();
        definition.TypeName.Should().BeNull();
        definition.ScriptPath.Should().BeNull();
        definition.RequiredModules.Should().BeNull();
        definition.ModulePaths.Should().BeNull();
        definition.Configuration.Should().BeNull();
    }

    [TestMethod]
    public void NodeDefinition_ForCSharpNode_ShouldHaveAssemblyAndTypeName()
    {
        // Arrange & Act
        var definition = new NodeDefinition
        {
            NodeId = "csharp-node",
            NodeName = "C# Task Node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharp,
            AssemblyPath = "/assemblies/MyTasks.dll",
            TypeName = "MyTasks.CustomTask"
        };

        // Assert
        definition.RuntimeType.Should().Be(ExecutionEngine.Enums.RuntimeType.CSharp);
        definition.AssemblyPath.Should().NotBeNullOrEmpty();
        definition.TypeName.Should().NotBeNullOrEmpty();
        definition.ScriptPath.Should().BeNull();
    }

    [TestMethod]
    public void NodeDefinition_ForPowerShellNode_ShouldHaveScriptPath()
    {
        // Arrange & Act
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            NodeName = "PowerShell Task Node",
            RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
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
        var definition = new NodeDefinition
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
