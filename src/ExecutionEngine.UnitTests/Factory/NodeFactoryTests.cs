// -----------------------------------------------------------------------
// <copyright file="NodeFactoryTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Factory;

using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using FluentAssertions;

[TestClass]
public class NodeFactoryTests
{
    private NodeFactory factory = null!;

    [TestInitialize]
    public void Setup()
    {
        this.factory = new NodeFactory(AssemblyInitialize.ServiceProvider);
    }

    [TestMethod]
    public void CreateNode_WithRelativeAssemblyPath_ShouldResolveToCurrentDirectory()
    {
        // Arrange - Use the actual ExecutionEngine.Example.dll with a relative path
        var currentDir = Directory.GetCurrentDirectory();
        var relativeAssemblyPath = "ExecutionEngine.UnitTests.dll";
        var expectedAbsolutePath = Path.Combine(currentDir, relativeAssemblyPath);

        // Verify the file exists at the expected location
        var actualDllPath = Path.Combine(
            Path.GetDirectoryName(typeof(NodeFactoryTests).Assembly.Location)!,
            relativeAssemblyPath);

        if (!File.Exists(actualDllPath))
        {
            Assert.Inconclusive($"Test assembly not found at {actualDllPath}. Cannot test relative path resolution.");
            return;
        }

        var definition = new CSharpNodeDefinition
        {
            NodeId = "test-node",
            AssemblyPath = relativeAssemblyPath,
            TypeName = "ExecutionEngine.UnitTests.Factory.AzureStackPreCheckNode"
        };

        // Act
        var node = this.factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.NodeId.Should().Be("test-node");
    }

    [TestMethod]
    public void CreateNode_WithAbsoluteAssemblyPath_ShouldUseAsIs()
    {
        // Arrange - Use absolute path
        var assemblyLocation = typeof(NodeFactoryTests).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
        var absoluteAssemblyPath = Path.Combine(assemblyDir, "ExecutionEngine.UnitTests.dll");

        if (!File.Exists(absoluteAssemblyPath))
        {
            Assert.Inconclusive($"Test assembly not found at {absoluteAssemblyPath}");
            return;
        }

        var definition = new CSharpNodeDefinition
        {
            NodeId = "test-node-abs",
            AssemblyPath = absoluteAssemblyPath,
            TypeName = "ExecutionEngine.UnitTests.Factory.AzureStackPreCheckNode"
        };

        // Act
        var node = this.factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.NodeId.Should().Be("test-node-abs");
    }

    [TestMethod]
    public void CreateNode_WithNonExistentRelativePath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var definition = new CSharpNodeDefinition
        {
            NodeId = "test-node",
            AssemblyPath = "NonExistent.dll",
            TypeName = "Some.Type.Name"
        };

        // Act
        Action act = () => this.factory.CreateNode(definition);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*NonExistent.dll*");
    }

    [TestMethod]
    public void CreateNode_CSharp_WithNullAssemblyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var definition = new CSharpNodeDefinition
        {
            NodeId = "test-node",
            AssemblyPath = null,
            TypeName = "Some.Type"
        };

        // Act
        Action act = () => this.factory.CreateNode(definition);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Value cannot be null*");
    }

    [TestMethod]
    public void CreateNode_CSharp_WithNullTypeName_ShouldThrowArgumentException()
    {
        // Arrange
        var definition = new CSharpNodeDefinition
        {
            NodeId = "test-node",
            AssemblyPath = "Some.dll",
            TypeName = null
        };

        // Act
        Action act = () => this.factory.CreateNode(definition);

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage("Could not load file or assembly*");
    }

    [TestMethod]
    public void ClearCache_ShouldRemoveAllCachedTypes()
    {
        // Arrange - Load a type to cache it
        var assemblyLocation = typeof(NodeFactoryTests).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation)!;
        var absoluteAssemblyPath = Path.Combine(assemblyDir, "ExecutionEngine.UnitTests.dll");

        if (!File.Exists(absoluteAssemblyPath))
        {
            Assert.Inconclusive($"Test assembly not found at {absoluteAssemblyPath}");
            return;
        }

        var definition = new CSharpNodeDefinition
        {
            NodeId = "cached-node",
            AssemblyPath = absoluteAssemblyPath,
            TypeName = "ExecutionEngine.UnitTests.Factory.AzureStackPreCheckNode"
        };

        this.factory.CreateNode(definition);
        var cachedCount = this.factory.CachedNodeCount;
        cachedCount.Should().BeGreaterThan(0);

        // Act
        this.factory.ClearCache();

        // Assert
        this.factory.CachedNodeCount.Should().Be(0);
    }
}
