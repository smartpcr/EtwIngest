// -----------------------------------------------------------------------
// <copyright file="NodeFactoryTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Factory;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using FluentAssertions;

[TestClass]
public class NodeFactoryTests
{
    [TestMethod]
    public void CreateNode_WithNullDefinition_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();

        // Act
        Action act = () => factory.CreateNode(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CreateNode_WithNullOrEmptyNodeId_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "",
            RuntimeType = "CSharp"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void CreateNode_WithNullOrEmptyRuntimeType_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = ""
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void CreateNode_WithUnsupportedRuntimeType_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "Python"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Python*");
    }

    [TestMethod]
    public void CreateNode_CSharpWithoutAssemblyPath_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            TypeName = "MyNamespace.MyNode"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AssemblyPath*");
    }

    [TestMethod]
    public void CreateNode_CSharpWithoutTypeName_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = "/path/to/assembly.dll"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TypeName*");
    }

    [TestMethod]
    public void CreateNode_CSharpWithNonExistentAssembly_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = "/non/existent/assembly.dll",
            TypeName = "MyNamespace.MyNode"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    public void CreateNode_CSharpScriptWithoutScriptPath_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharpScript"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScriptPath*");
    }

    [TestMethod]
    public void CreateNode_CSharpScript_ShouldReturnCSharpScriptNode()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "script-node",
            NodeName = "Script Node",
            RuntimeType = "CSharpScript",
            ScriptPath = "/path/to/script.csx"
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<CSharpScriptNode>();
        node.NodeId.Should().Be("script-node");
        node.NodeName.Should().Be("Script Node");
    }

    [TestMethod]
    public void CreateNode_PowerShellWithoutScriptPath_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "PowerShell"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScriptPath*");
    }

    [TestMethod]
    public void CreateNode_PowerShell_ShouldReturnPowerShellScriptNode()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "ps-node",
            NodeName = "PowerShell Node",
            RuntimeType = "PowerShell",
            ScriptPath = "/path/to/script.ps1"
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<PowerShellScriptNode>();
        node.NodeId.Should().Be("ps-node");
        node.NodeName.Should().Be("PowerShell Node");
    }

    [TestMethod]
    public void CreateNode_CaseInsensitiveRuntimeType_ShouldWork()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSHARPSCRIPT", // Uppercase
            ScriptPath = "/path/to/script.csx"
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().BeOfType<CSharpScriptNode>();
    }

    [TestMethod]
    public void CreateNode_CSharp_ShouldLoadFromAssemblyAndCache()
    {
        // Arrange
        var factory = new NodeFactory();
        var assemblyPath = typeof(TestAssemblyNode).Assembly.Location;
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            NodeName = "Test Node",
            RuntimeType = "CSharp",
            AssemblyPath = assemblyPath,
            TypeName = typeof(TestAssemblyNode).FullName!
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<TestAssemblyNode>();
        node.NodeId.Should().Be("test-node");
        node.NodeName.Should().Be("Test Node");
        factory.CachedNodeCount.Should().Be(1);
    }

    [TestMethod]
    public void CreateNode_CSharp_SecondCallShouldUseCachedType()
    {
        // Arrange
        var factory = new NodeFactory();
        var assemblyPath = typeof(TestAssemblyNode).Assembly.Location;
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = assemblyPath,
            TypeName = typeof(TestAssemblyNode).FullName!
        };

        // Act
        var node1 = factory.CreateNode(definition);
        var node2 = factory.CreateNode(definition);

        // Assert
        node1.Should().NotBeNull();
        node2.Should().NotBeNull();
        factory.CachedNodeCount.Should().Be(1); // Only one cached type
    }

    [TestMethod]
    public void CreateNode_CSharpWithInvalidTypeName_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var assemblyPath = typeof(TestAssemblyNode).Assembly.Location;
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = assemblyPath,
            TypeName = "NonExistent.TypeName"
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<TypeLoadException>()
            .WithMessage("*not found in assembly*");
    }

    [TestMethod]
    public void CreateNode_CSharpWithNonINodeType_ShouldThrowException()
    {
        // Arrange
        var factory = new NodeFactory();
        var assemblyPath = typeof(NonNodeType).Assembly.Location;
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = assemblyPath,
            TypeName = typeof(NonNodeType).FullName!
        };

        // Act
        Action act = () => factory.CreateNode(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not implement INode*");
    }

    [TestMethod]
    public void ClearCache_ShouldResetCachedNodeCount()
    {
        // Arrange
        var factory = new NodeFactory();
        var assemblyPath = typeof(TestAssemblyNode).Assembly.Location;
        var definition = new NodeDefinition
        {
            NodeId = "test-node",
            RuntimeType = "CSharp",
            AssemblyPath = assemblyPath,
            TypeName = typeof(TestAssemblyNode).FullName!
        };
        factory.CreateNode(definition);

        // Act
        factory.ClearCache();

        // Assert
        factory.CachedNodeCount.Should().Be(0);
    }

    [TestMethod]
    public void CachedNodeCount_InitiallyZero()
    {
        // Arrange & Act
        var factory = new NodeFactory();

        // Assert
        factory.CachedNodeCount.Should().Be(0);
    }
}

/// <summary>
/// Test node for assembly loading tests.
/// </summary>
public class TestAssemblyNode : ExecutableNodeBase
{
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        return new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = this.NodeId,
            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Completed,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Test class that does not implement INode interface.
/// </summary>
public class NonNodeType
{
    public string Name { get; set; } = "NotANode";
}
