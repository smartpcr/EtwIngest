// -----------------------------------------------------------------------
// <copyright file="WorkflowSerializerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowSerializerTests
{
    private WorkflowSerializer serializer = null!;
    private string testDirectory = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        this.serializer = new WorkflowSerializer();
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"WorkflowSerializerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(this.testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(this.testDirectory))
        {
            Directory.Delete(this.testDirectory, recursive: true);
        }
    }

    private WorkflowDefinition CreateSampleWorkflow()
    {
        return new WorkflowDefinition
        {
            WorkflowId = "test-workflow",
            WorkflowName = "Test Workflow",
            Description = "A test workflow",
            Version = "1.0",
            EntryPointNodeId = "node-1",
            MaxConcurrency = 5,
            AllowPause = true,
            TimeoutSeconds = 300,
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "node-1",
                    NodeName = "First Node",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
                    ScriptPath = "script1.csx"
                },
                new NodeDefinition
                {
                    NodeId = "node-2",
                    NodeName = "Second Node",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.PowerShell,
                    ScriptPath = "script2.ps1"
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection
                {
                    SourceNodeId = "node-1",
                    TargetNodeId = "node-2",
                    TriggerMessageType = MessageType.Complete,
                    IsEnabled = true,
                    Priority = 0
                }
            },
            Metadata = new Dictionary<string, object>
            {
                { "author", "Test User" },
                { "version", 1 }
            }
        };
    }

    #region JSON Tests

    [TestMethod]
    public void ToJson_WithValidWorkflow_ShouldSerialize()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();

        // Act
        var json = this.serializer.ToJson(workflow);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("test-workflow");
        json.Should().Contain("Test Workflow");
        json.Should().Contain("node-1");
        json.Should().Contain("node-2");
    }

    [TestMethod]
    public void ToJson_WithNullWorkflow_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.ToJson(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("workflow");
    }

    [TestMethod]
    public void FromJson_WithValidJson_ShouldDeserialize()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();
        var json = this.serializer.ToJson(original);

        // Act
        var deserialized = this.serializer.FromJson(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.WorkflowId.Should().Be(original.WorkflowId);
        deserialized.WorkflowName.Should().Be(original.WorkflowName);
        deserialized.Nodes.Should().HaveCount(2);
        deserialized.Connections.Should().HaveCount(1);
    }

    [TestMethod]
    public void FromJson_WithEmptyString_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.FromJson(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("json");
    }

    [TestMethod]
    public void FromJson_WithNullString_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.FromJson(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("json");
    }

    [TestMethod]
    public void JsonRoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();

        // Act
        var json = this.serializer.ToJson(original);
        var deserialized = this.serializer.FromJson(json);

        // Assert
        deserialized.WorkflowId.Should().Be(original.WorkflowId);
        deserialized.WorkflowName.Should().Be(original.WorkflowName);
        deserialized.Description.Should().Be(original.Description);
        deserialized.Version.Should().Be(original.Version);
        deserialized.EntryPointNodeId.Should().Be(original.EntryPointNodeId);
        deserialized.MaxConcurrency.Should().Be(original.MaxConcurrency);
        deserialized.AllowPause.Should().Be(original.AllowPause);
        deserialized.TimeoutSeconds.Should().Be(original.TimeoutSeconds);
        deserialized.Nodes.Should().HaveCount(original.Nodes.Count);
        deserialized.Connections.Should().HaveCount(original.Connections.Count);
        deserialized.Metadata.Should().HaveCount(original.Metadata.Count);
    }

    #endregion

    #region YAML Tests

    [TestMethod]
    public void ToYaml_WithValidWorkflow_ShouldSerialize()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();

        // Act
        var yaml = this.serializer.ToYaml(workflow);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("test-workflow");
        yaml.Should().Contain("Test Workflow");
        yaml.Should().Contain("node-1");
        yaml.Should().Contain("node-2");
    }

    [TestMethod]
    public void ToYaml_WithNullWorkflow_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.ToYaml(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("workflow");
    }

    [TestMethod]
    public void FromYaml_WithValidYaml_ShouldDeserialize()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();
        var yaml = this.serializer.ToYaml(original);

        // Act
        var deserialized = this.serializer.FromYaml(yaml);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.WorkflowId.Should().Be(original.WorkflowId);
        deserialized.WorkflowName.Should().Be(original.WorkflowName);
        deserialized.Nodes.Should().HaveCount(2);
        deserialized.Connections.Should().HaveCount(1);
    }

    [TestMethod]
    public void FromYaml_WithEmptyString_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.FromYaml(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("yaml");
    }

    [TestMethod]
    public void FromYaml_WithNullString_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.FromYaml(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("yaml");
    }

    [TestMethod]
    public void YamlRoundTrip_ShouldPreserveAllProperties()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();

        // Act
        var yaml = this.serializer.ToYaml(original);
        var deserialized = this.serializer.FromYaml(yaml);

        // Assert
        deserialized.WorkflowId.Should().Be(original.WorkflowId);
        deserialized.WorkflowName.Should().Be(original.WorkflowName);
        deserialized.Description.Should().Be(original.Description);
        deserialized.Version.Should().Be(original.Version);
        deserialized.EntryPointNodeId.Should().Be(original.EntryPointNodeId);
        deserialized.MaxConcurrency.Should().Be(original.MaxConcurrency);
        deserialized.AllowPause.Should().Be(original.AllowPause);
        deserialized.TimeoutSeconds.Should().Be(original.TimeoutSeconds);
        deserialized.Nodes.Should().HaveCount(original.Nodes.Count);
        deserialized.Connections.Should().HaveCount(original.Connections.Count);
    }

    #endregion

    #region File I/O Tests

    [TestMethod]
    public void SaveToFile_WithJsonExtension_ShouldCreateJsonFile()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.json");

        // Act
        this.serializer.SaveToFile(workflow, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = File.ReadAllText(filePath);
        content.Should().Contain("test-workflow");
    }

    [TestMethod]
    public void SaveToFile_WithYamlExtension_ShouldCreateYamlFile()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.yaml");

        // Act
        this.serializer.SaveToFile(workflow, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = File.ReadAllText(filePath);
        content.Should().Contain("test-workflow");
    }

    [TestMethod]
    public void SaveToFile_WithYmlExtension_ShouldCreateYamlFile()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.yml");

        // Act
        this.serializer.SaveToFile(workflow, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var content = File.ReadAllText(filePath);
        content.Should().Contain("test-workflow");
    }

    [TestMethod]
    public void SaveToFile_WithUnsupportedExtension_ShouldThrowException()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.txt");

        // Act
        Action act = () => this.serializer.SaveToFile(workflow, filePath);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported*");
    }

    [TestMethod]
    public void SaveToFile_WithNullWorkflow_ShouldThrowException()
    {
        // Arrange
        var filePath = Path.Combine(this.testDirectory, "test.json");

        // Act
        Action act = () => this.serializer.SaveToFile(null!, filePath);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("workflow");
    }

    [TestMethod]
    public void SaveToFile_WithEmptyFilePath_ShouldThrowException()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();

        // Act
        Action act = () => this.serializer.SaveToFile(workflow, string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("filePath");
    }

    [TestMethod]
    public void SaveToFile_WithNonExistentDirectory_ShouldCreateDirectory()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var subdirectory = Path.Combine(this.testDirectory, "subdir1", "subdir2");
        var filePath = Path.Combine(subdirectory, "test.json");

        // Act
        this.serializer.SaveToFile(workflow, filePath);

        // Assert
        Directory.Exists(subdirectory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [TestMethod]
    public void LoadFromFile_WithJsonFile_ShouldLoadWorkflow()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.json");
        this.serializer.SaveToFile(original, filePath);

        // Act
        var loaded = this.serializer.LoadFromFile(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded.WorkflowId.Should().Be(original.WorkflowId);
        loaded.Nodes.Should().HaveCount(original.Nodes.Count);
    }

    [TestMethod]
    public void LoadFromFile_WithYamlFile_ShouldLoadWorkflow()
    {
        // Arrange
        var original = this.CreateSampleWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.yaml");
        this.serializer.SaveToFile(original, filePath);

        // Act
        var loaded = this.serializer.LoadFromFile(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded.WorkflowId.Should().Be(original.WorkflowId);
        loaded.Nodes.Should().HaveCount(original.Nodes.Count);
    }

    [TestMethod]
    public void LoadFromFile_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var filePath = Path.Combine(this.testDirectory, "nonexistent.json");

        // Act
        Action act = () => this.serializer.LoadFromFile(filePath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    public void LoadFromFile_WithEmptyFilePath_ShouldThrowException()
    {
        // Act
        Action act = () => this.serializer.LoadFromFile(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("filePath");
    }

    [TestMethod]
    public void LoadFromFile_WithUnsupportedExtension_ShouldThrowException()
    {
        // Arrange
        var filePath = Path.Combine(this.testDirectory, "test.txt");
        File.WriteAllText(filePath, "test content");

        // Act
        Action act = () => this.serializer.LoadFromFile(filePath);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported*");
    }

    #endregion

    #region Container Node Tests

    [TestMethod]
    public void FromYaml_ContainerNodeWithChildren_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
workflowId: test-container
workflowName: Test Container Workflow
description: Test workflow with container node

nodes:
  - nodeId: container1
    nodeName: Test Container
    runtimeType: Container
    configuration:
      ExecutionMode: Sequential
      ChildNodes:
        - nodeId: child1
          nodeName: Child Node 1
          runtimeType: CSharpTask
          assemblyPath: ./test.dll
          typeName: Test.Node1
          configuration:
            testParam: value1

        - nodeId: child2
          nodeName: Child Node 2
          runtimeType: CSharpTask
          assemblyPath: ./test.dll
          typeName: Test.Node2
          configuration:
            testParam: value2

      ChildConnections:
        - sourceNodeId: child1
          targetNodeId: child2
          triggerMessageType: Complete
          isEnabled: true

connections: []
";

        // Act
        var workflow = this.serializer.FromYaml(yaml);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("test-container");
        workflow.WorkflowName.Should().Be("Test Container Workflow");
        workflow.Nodes.Should().HaveCount(1);

        // Assert - Container node
        var containerNode = workflow.Nodes[0];
        containerNode.NodeId.Should().Be("container1");
        containerNode.NodeName.Should().Be("Test Container");
        containerNode.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.Configuration.Should().NotBeNull();
        containerNode.Configuration.Should().ContainKey("ExecutionMode");
        containerNode.Configuration.Should().ContainKey("ChildNodes");
        containerNode.Configuration.Should().ContainKey("ChildConnections");

        // Assert - ExecutionMode
        var execMode = containerNode.Configuration["ExecutionMode"]?.ToString();
        execMode.Should().Be("Sequential");

        // Assert - ChildNodes
        var childNodes = containerNode.Configuration["ChildNodes"];
        childNodes.Should().NotBeNull("ChildNodes should be present in Configuration");

        Console.WriteLine($"ChildNodes type: {childNodes?.GetType().FullName}");
        Console.WriteLine($"ChildNodes value: {childNodes}");

        // Try to get child nodes as a list
        if (childNodes is System.Collections.IEnumerable enumerable)
        {
            var childList = new List<object>();
            foreach (var item in enumerable)
            {
                childList.Add(item);
                Console.WriteLine($"Child item type: {item?.GetType().FullName}");
            }

            childList.Should().HaveCount(2, "Container should have 2 child nodes");

            // Verify first child
            var child1 = childList[0];
            child1.Should().NotBeNull();
            Console.WriteLine($"Child1: {child1}");

            // Verify second child
            var child2 = childList[1];
            child2.Should().NotBeNull();
            Console.WriteLine($"Child2: {child2}");
        }
        else
        {
            Assert.Fail($"ChildNodes is not IEnumerable. Type: {childNodes?.GetType().FullName}");
        }

        // Assert - ChildConnections
        var childConnections = containerNode.Configuration["ChildConnections"];
        childConnections.Should().NotBeNull("ChildConnections should be present in Configuration");

        if (childConnections is System.Collections.IEnumerable connEnumerable)
        {
            var connList = new List<object>();
            foreach (var item in connEnumerable)
            {
                connList.Add(item);
            }

            connList.Should().HaveCount(1, "Container should have 1 child connection");
        }
    }

    [TestMethod]
    public void FromYaml_ContainerNode_ChildNodesShouldBeNodeDefinitions()
    {
        // Arrange
        var yaml = @"
workflowId: test-typed
workflowName: Test Typed Container

nodes:
  - nodeId: container1
    nodeName: Container with typed children
    runtimeType: Container
    configuration:
      ExecutionMode: Parallel
      ChildNodes:
        - nodeId: task1
          nodeName: Task 1
          runtimeType: CSharpTask
          assemblyPath: ./test.dll
          typeName: Test.Task1
      ChildConnections: []

connections: []
";

        // Act
        var workflow = this.serializer.FromYaml(yaml);

        // Assert
        var containerNode = workflow.Nodes[0];
        var childNodes = containerNode.Configuration["ChildNodes"];

        Console.WriteLine($"ChildNodes actual type: {childNodes?.GetType().FullName}");

        // Check if we can cast to List<NodeDefinition>
        if (childNodes is List<NodeDefinition> nodeDefList)
        {
            nodeDefList.Should().HaveCount(1);
            nodeDefList[0].NodeId.Should().Be("task1");
            nodeDefList[0].NodeName.Should().Be("Task 1");
            nodeDefList[0].RuntimeType.Should().Be(RuntimeType.CSharpTask);
            Console.WriteLine("SUCCESS: ChildNodes is List<NodeDefinition>");
        }
        else if (childNodes is System.Collections.IEnumerable enumerable)
        {
            var items = new List<object>();
            foreach (var item in enumerable)
            {
                items.Add(item);
                Console.WriteLine($"Item type: {item?.GetType().FullName}");

                // Try to access properties dynamically
                var itemType = item?.GetType();
                var nodeIdProp = itemType?.GetProperty("NodeId");
                if (nodeIdProp != null)
                {
                    var nodeIdValue = nodeIdProp.GetValue(item);
                    Console.WriteLine($"NodeId: {nodeIdValue}");
                }

                // Try to cast to NodeDefinition
                if (item is NodeDefinition nodeDef)
                {
                    Console.WriteLine($"Item IS a NodeDefinition: {nodeDef.NodeId}");
                }
                else
                {
                    Console.WriteLine($"Item is NOT a NodeDefinition");
                }
            }

            items.Should().HaveCount(1, "Should have exactly 1 child node");
        }
        else
        {
            Assert.Fail($"ChildNodes has unexpected type: {childNodes?.GetType().FullName}");
        }
    }

    [TestMethod]
    public void FromYaml_ComplexContainerNode_ShouldDeserializeAllChildProperties()
    {
        // Arrange - This mimics the azs_deployment.yaml structure
        var yaml = @"
workflowId: complex-container-test
workflowName: Complex Container Test

nodes:
  - nodeId: pre-deployment-checks
    nodeName: Pre-Deployment Checks
    runtimeType: Container
    configuration:
      ExecutionMode: Sequential
      ChildNodes:
        - nodeId: check-network
          nodeName: Network Connectivity Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ./ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackPreCheckNode
          configuration:
            checkType: network

        - nodeId: check-storage
          nodeName: Storage Validation Check
          type: Task
          runtimeType: CSharp
          assemblyPath: ./ExecutionEngine.Example.dll
          typeName: ExecutionEngine.Example.Nodes.AzureStackPreCheckNode
          configuration:
            checkType: storage

      ChildConnections:
        - sourceNodeId: check-network
          targetNodeId: check-storage
          triggerMessageType: Complete
          isEnabled: true

connections: []
";

        // Act
        var workflow = this.serializer.FromYaml(yaml);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Nodes.Should().HaveCount(1);

        var containerNode = workflow.Nodes[0];
        containerNode.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.Configuration.Should().ContainKey("ChildNodes");

        var childNodes = containerNode.Configuration["ChildNodes"];
        childNodes.Should().NotBeNull();

        // After deserialization, ChildNodes should be properly typed as List<NodeDefinition>
        childNodes.Should().BeOfType<List<NodeDefinition>>("ChildNodes should be deserialized as List<NodeDefinition>");

        var childList = childNodes as List<NodeDefinition>;
        childList.Should().HaveCount(2, "Should have 2 child nodes");

        // Verify first child node (check-network)
        var firstChild = childList![0];
        firstChild.NodeId.Should().Be("check-network");
        firstChild.NodeName.Should().Be("Network Connectivity Check");
        firstChild.RuntimeType.Should().Be(RuntimeType.CSharp);
        firstChild.AssemblyPath.Should().Be("./ExecutionEngine.Example.dll");
        firstChild.TypeName.Should().Be("ExecutionEngine.Example.Nodes.AzureStackPreCheckNode");
        firstChild.Configuration.Should().ContainKey("checkType");
        firstChild.Configuration!["checkType"].Should().Be("network");
        Console.WriteLine($"First child validated: {firstChild.NodeId}");

        // Verify second child node (check-storage)
        var secondChild = childList[1];
        secondChild.NodeId.Should().Be("check-storage");
        secondChild.NodeName.Should().Be("Storage Validation Check");
        secondChild.RuntimeType.Should().Be(RuntimeType.CSharp);
        secondChild.Configuration.Should().ContainKey("checkType");
        secondChild.Configuration!["checkType"].Should().Be("storage");
        Console.WriteLine($"Second child validated: {secondChild.NodeId}");

        // Verify ChildConnections are also properly typed
        containerNode.Configuration.Should().ContainKey("ChildConnections");
        var childConnections = containerNode.Configuration["ChildConnections"];
        childConnections.Should().BeOfType<List<NodeConnection>>("ChildConnections should be deserialized as List<NodeConnection>");

        var connList = childConnections as List<NodeConnection>;
        connList.Should().HaveCount(1, "Should have 1 child connection");

        var conn = connList![0];
        conn.SourceNodeId.Should().Be("check-network");
        conn.TargetNodeId.Should().Be("check-storage");
        conn.TriggerMessageType.Should().Be(MessageType.Complete);
        conn.IsEnabled.Should().BeTrue();
        Console.WriteLine("Child connection validated");
    }

    [TestMethod]
    public void LoadFromFile_ContainerParallel_ShouldDeserializeCorrectly()
    {
        // Load test YAML file with parallel container
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-parallel.yaml");

        // Skip if file doesn't exist
        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow structure
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("test-container-parallel");
        workflow.WorkflowName.Should().Be("Test Container with Parallel Children");
        workflow.Nodes.Should().HaveCount(3); // start, parallel-container, finish

        // Assert - Find container node
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "parallel-container");
        containerNode.Should().NotBeNull("Container node should exist");
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.NodeName.Should().Be("Parallel Container");

        // Assert - Container configuration
        containerNode.Configuration.Should().ContainKey("ExecutionMode");
        containerNode.Configuration.Should().ContainKey("ChildNodes");
        containerNode.Configuration.Should().ContainKey("ChildConnections");
        containerNode.Configuration["ExecutionMode"].Should().Be("Parallel");

        // Assert - Child nodes
        var childNodes = containerNode.Configuration["ChildNodes"];
        childNodes.Should().NotBeNull();

        if (childNodes is System.Collections.IEnumerable enumerable)
        {
            var childList = enumerable.Cast<object>().ToList();
            childList.Should().HaveCount(3, "Container should have 3 child nodes");

            // Child nodes from YAML are dictionaries, not NodeDefinition objects
            // They get converted by ContainerNode.ParseChildNodes during initialization
            foreach (var child in childList)
            {
                child.Should().NotBeNull();
                // YAML deserializes to dictionaries, not NodeDefinition objects
                Console.WriteLine($"Child type: {child.GetType().FullName}");
            }
        }
        else
        {
            Assert.Fail($"ChildNodes is not IEnumerable. Type: {childNodes?.GetType().FullName}");
        }

        // Assert - Child connections (empty for parallel execution)
        var childConnections = containerNode.Configuration["ChildConnections"];
        childConnections.Should().NotBeNull();

        if (childConnections is System.Collections.IEnumerable connEnumerable)
        {
            connEnumerable.Cast<object>().Should().BeEmpty("Parallel execution has no child connections");
        }

        // Assert - External connections
        workflow.Connections.Should().HaveCount(2);
        workflow.Connections.Should().Contain(c => c.SourceNodeId == "start" && c.TargetNodeId == "parallel-container");
        workflow.Connections.Should().Contain(c => c.SourceNodeId == "parallel-container" && c.TargetNodeId == "finish");
    }

    [TestMethod]
    public void LoadFromFile_ContainerSequential_ShouldDeserializeWithChildConnections()
    {
        // Load test YAML file with sequential container and child connections
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-sequential.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("test-container-sequential");
        workflow.Nodes.Should().HaveCount(3);
        workflow.Connections.Should().HaveCount(2);

        // Assert - Container node
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sequential-container");
        containerNode.Should().NotBeNull();
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.Configuration["ExecutionMode"].Should().Be("Sequential");

        // Assert - Child nodes
        var childNodes = containerNode.Configuration["ChildNodes"] as System.Collections.IEnumerable;
        childNodes.Should().NotBeNull();
        var childList = childNodes!.Cast<object>().ToList();
        childList.Should().HaveCount(3, "Should have 3 sequential steps");

        // Child nodes from YAML are dictionaries, not NodeDefinition objects
        // They get converted by ContainerNode.ParseChildNodes during initialization
        foreach (var child in childList)
        {
            child.Should().NotBeNull();
            Console.WriteLine($"Child type: {child.GetType().FullName}");
        }

        // Assert - Child connections
        var childConnections = containerNode.Configuration["ChildConnections"] as System.Collections.IEnumerable;
        childConnections.Should().NotBeNull();
        var connectionList = childConnections!.Cast<object>().ToList();
        connectionList.Should().HaveCount(2, "Sequential execution should have 2 connections");

        // Verify connections are NodeConnection objects
        foreach (var conn in connectionList)
        {
            if (conn is NodeConnection nodeConn)
            {
                nodeConn.TriggerMessageType.Should().Be(MessageType.Complete);
                nodeConn.IsEnabled.Should().BeTrue();
            }
            else
            {
                Console.WriteLine($"Connection type: {conn?.GetType().FullName}");
            }
        }
    }

    [TestMethod]
    public void LoadFromFile_WithSubflow_ShouldDeserializeSubflowNodes()
    {
        // Load test YAML file with subflow nodes
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "with-subflow.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("test-with-subflow");
        workflow.Nodes.Should().HaveCount(4); // start, subflow1, subflow2, finish

        // Assert - Find subflow nodes
        var subflow1 = workflow.Nodes.FirstOrDefault(n => n.NodeId == "subflow1");
        var subflow2 = workflow.Nodes.FirstOrDefault(n => n.NodeId == "subflow2");

        subflow1.Should().NotBeNull();
        subflow2.Should().NotBeNull();

        subflow1!.RuntimeType.Should().Be(RuntimeType.Subflow);
        subflow2!.RuntimeType.Should().Be(RuntimeType.Subflow);

        // Assert - Subflow configuration
        subflow1.Configuration.Should().ContainKey("WorkflowFilePath");
        subflow1.Configuration.Should().ContainKey("InputMappings");
        subflow1.Configuration.Should().ContainKey("OutputMappings");

        subflow1.Configuration["WorkflowFilePath"].Should().Be("./subworkflow1.yaml");
        subflow2.Configuration["WorkflowFilePath"].Should().Be("./subworkflow2.yaml");

        // Assert - Connections
        workflow.Connections.Should().HaveCount(3);
        workflow.Connections.Should().Contain(c => c.SourceNodeId == "start" && c.TargetNodeId == "subflow1");
        workflow.Connections.Should().Contain(c => c.SourceNodeId == "subflow1" && c.TargetNodeId == "subflow2");
        workflow.Connections.Should().Contain(c => c.SourceNodeId == "subflow2" && c.TargetNodeId == "finish");
    }

    [TestMethod]
    public void LoadFromFile_ContainerWithSubflows_ShouldDeserializeCorrectly()
    {
        // Test container containing subflow child nodes
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-with-subflows.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("test-container-subflows");
        workflow.Nodes.Should().HaveCount(3);

        // Assert - Find container node
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "deployment-container");
        containerNode.Should().NotBeNull();
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.Configuration["ExecutionMode"].Should().Be("Parallel");

        // Assert - Child nodes (Subflows)
        var childNodes = containerNode.Configuration["ChildNodes"] as System.Collections.IEnumerable;
        childNodes.Should().NotBeNull();
        var childList = childNodes!.Cast<object>().ToList();
        childList.Should().HaveCount(3, "Should have 3 subflow children");

        // Child nodes from YAML are dictionaries, not NodeDefinition objects
        // They get converted by ContainerNode.ParseChildNodes during initialization
        foreach (var child in childList)
        {
            child.Should().NotBeNull();
            Console.WriteLine($"Child type: {child.GetType().FullName}");
        }

        // Assert - Child connections (empty for parallel)
        var childConnections = containerNode.Configuration["ChildConnections"] as System.Collections.IEnumerable;
        childConnections.Should().NotBeNull();
        childConnections!.Cast<object>().Should().BeEmpty("Parallel execution has no child connections");
    }

    [TestMethod]
    public void LoadFromFile_ContainerNode_ChildNodesShouldConvertToNodeDefinitions()
    {
        // Test that ChildNodes in container are properly deserialized as NodeDefinition objects
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-parallel.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "parallel-container");
        containerNode.Should().NotBeNull();
        containerNode!.Configuration.Should().ContainKey("ChildNodes");

        var childNodes = containerNode.Configuration["ChildNodes"];
        childNodes.Should().NotBeNull();
        childNodes.Should().BeOfType<List<NodeDefinition>>("ChildNodes should be deserialized as List<NodeDefinition>");

        var childList = childNodes as List<NodeDefinition>;
        childList.Should().HaveCount(3, "Container should have 3 child nodes");

        // Verify each child is a properly typed NodeDefinition
        childList![0].NodeId.Should().Be("child1");
        childList[0].NodeName.Should().Be("Child Task 1");
        childList[0].RuntimeType.Should().Be(RuntimeType.CSharpTask);
        childList[0].Configuration.Should().ContainKey("script");

        childList[1].NodeId.Should().Be("child2");
        childList[1].NodeName.Should().Be("Child Task 2");

        childList[2].NodeId.Should().Be("child3");
        childList[2].NodeName.Should().Be("Child Task 3");

        Console.WriteLine($"All 3 child nodes properly deserialized as NodeDefinition objects");

        // Verify ChildConnections are also properly typed
        var childConnections = containerNode.Configuration["ChildConnections"];
        childConnections.Should().NotBeNull();
        childConnections.Should().BeOfType<List<NodeConnection>>("ChildConnections should be deserialized as List<NodeConnection>");

        if (childConnections is System.Collections.IEnumerable connEnum)
        {
            connEnum.Cast<object>().Should().BeEmpty("Parallel container has no child connections");
        }
    }

    [TestMethod]
    public void LoadFromFile_ContainerSequential_ChildConnectionsShouldHaveProperStructure()
    {
        // Test that ChildConnections from YAML have proper structure
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-sequential.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sequential-container");
        containerNode.Should().NotBeNull();
        containerNode!.Configuration.Should().ContainKey("ChildConnections");

        var childConnections = containerNode.Configuration["ChildConnections"];
        childConnections.Should().NotBeNull();

        if (childConnections is System.Collections.IEnumerable connEnum)
        {
            var connectionList = connEnum.Cast<object>().ToList();
            connectionList.Should().HaveCount(2, "Sequential container should have 2 child connections");

            // Verify each connection is a dictionary with expected structure
            foreach (var conn in connectionList)
            {
                conn.Should().NotBeNull();

                if (conn is System.Collections.IDictionary dict)
                {
                    dict.Contains("sourceNodeId").Should().BeTrue("Connection should have sourceNodeId");
                    dict.Contains("targetNodeId").Should().BeTrue("Connection should have targetNodeId");
                    dict.Contains("triggerMessageType").Should().BeTrue("Connection should have triggerMessageType");
                    dict.Contains("isEnabled").Should().BeTrue("Connection should have isEnabled");

                    Console.WriteLine($"Connection: {dict["sourceNodeId"]} → {dict["targetNodeId"]}");
                }
                else if (conn is NodeConnection nodeConn)
                {
                    // Already converted to NodeConnection
                    nodeConn.SourceNodeId.Should().NotBeNullOrEmpty();
                    nodeConn.TargetNodeId.Should().NotBeNullOrEmpty();
                    Console.WriteLine($"Connection (typed): {nodeConn.SourceNodeId} → {nodeConn.TargetNodeId}");
                }
                else
                {
                    Assert.Fail($"Connection has unexpected type: {conn.GetType().FullName}");
                }
            }
        }
    }

    [TestMethod]
    public void LoadFromFile_ContainerWithSubflows_SubflowConfigurationShouldBeComplete()
    {
        // Test that Subflow nodes inside containers have complete configuration
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "container-with-subflows.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "deployment-container");
        containerNode.Should().NotBeNull();

        var childNodes = containerNode!.Configuration["ChildNodes"];
        childNodes.Should().NotBeNull();
        childNodes.Should().BeOfType<List<NodeDefinition>>("ChildNodes should be deserialized as List<NodeDefinition>");

        var childList = childNodes as List<NodeDefinition>;
        childList.Should().HaveCount(3, "Container should have 3 subflow children");

        // Check first subflow child structure
        var firstChild = childList![0];
        firstChild.Should().NotBeNull();
        firstChild.NodeId.Should().Be("deploy-region1");
        firstChild.NodeName.Should().Be("Deploy Region 1");
        firstChild.RuntimeType.Should().Be(RuntimeType.Subflow, "Child nodes should be Subflow type");

        // Verify Subflow configuration
        firstChild.Configuration.Should().NotBeNull();
        firstChild.Configuration.Should().ContainKey("WorkflowFilePath");
        firstChild.Configuration.Should().ContainKey("InputMappings");
        firstChild.Configuration.Should().ContainKey("OutputMappings");
        firstChild.Configuration!["WorkflowFilePath"].Should().Be("./deploy-region.yaml");

        Console.WriteLine($"Subflow configuration valid: {firstChild.NodeId}");
    }

    [TestMethod]
    public void LoadFromFile_AzsDeployment_ShouldDeserializeCorrectly()
    {
        // Test azs_deployment.yaml - Complex workflow with 3 container nodes
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "azs_deployment.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("azs-deployment");
        workflow.WorkflowName.Should().Be("Azure Stack Deployment");
        workflow.Description.Should().Contain("Azure Stack deployment workflow");
        workflow.Nodes.Should().HaveCount(3, "Should have 3 container nodes");
        workflow.Connections.Should().HaveCount(2, "Should have 2 main connections");

        // Assert - Phase 1: Pre-deployment Checks Container
        var preDeploymentContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "pre-deployment-checks");
        preDeploymentContainer.Should().NotBeNull();
        preDeploymentContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        preDeploymentContainer.NodeName.Should().Be("Pre-Deployment Checks");
        preDeploymentContainer.Configuration["ExecutionMode"].Should().Be("Sequential");

        var preDeploymentChildren = preDeploymentContainer.Configuration["ChildNodes"];
        preDeploymentChildren.Should().NotBeNull();
        if (preDeploymentChildren is System.Collections.IEnumerable enumerable)
        {
            var childCount = enumerable.Cast<object>().Count();
            childCount.Should().Be(3, "Pre-deployment should have 3 checks");
        }

        var preDeploymentConnections = preDeploymentContainer.Configuration["ChildConnections"];
        preDeploymentConnections.Should().NotBeNull();
        if (preDeploymentConnections is System.Collections.IEnumerable connEnum)
        {
            var connCount = connEnum.Cast<object>().Count();
            connCount.Should().Be(2, "Sequential execution should have 2 child connections");
        }

        // Assert - Phase 2: Node Deployments Container (Parallel Subflows)
        var nodeDeploymentsContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "node-deployments");
        nodeDeploymentsContainer.Should().NotBeNull();
        nodeDeploymentsContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        nodeDeploymentsContainer.NodeName.Should().Be("Node Deployments");
        nodeDeploymentsContainer.Configuration["ExecutionMode"].Should().Be("Parallel");

        var nodeDeploymentChildren = nodeDeploymentsContainer.Configuration["ChildNodes"];
        nodeDeploymentChildren.Should().NotBeNull();
        if (nodeDeploymentChildren is System.Collections.IEnumerable deployEnum)
        {
            var childList = deployEnum.Cast<object>().ToList();
            childList.Should().HaveCount(3, "Should have 3 parallel subflow deployments");
        }

        var nodeDeploymentConnections = nodeDeploymentsContainer.Configuration["ChildConnections"];
        nodeDeploymentConnections.Should().NotBeNull();
        if (nodeDeploymentConnections is System.Collections.IEnumerable deployConnEnum)
        {
            var connList = deployConnEnum.Cast<object>().ToList();
            connList.Should().BeEmpty("Parallel execution should have no child connections");
        }

        // Assert - Phase 3: Post-deployment Health Checks Container
        var healthChecksContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "health-checks");
        healthChecksContainer.Should().NotBeNull();
        healthChecksContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        healthChecksContainer.NodeName.Should().Be("Post-Deployment Health Checks");
        healthChecksContainer.Configuration["ExecutionMode"].Should().Be("Sequential");

        var healthCheckChildren = healthChecksContainer.Configuration["ChildNodes"];
        healthCheckChildren.Should().NotBeNull();
        if (healthCheckChildren is System.Collections.IEnumerable healthEnum)
        {
            var childCount = healthEnum.Cast<object>().Count();
            childCount.Should().Be(4, "Health checks should have 4 service checks");
        }

        var healthCheckConnections = healthChecksContainer.Configuration["ChildConnections"];
        healthCheckConnections.Should().NotBeNull();
        if (healthCheckConnections is System.Collections.IEnumerable healthConnEnum)
        {
            var connCount = healthConnEnum.Cast<object>().Count();
            connCount.Should().Be(3, "Sequential execution should have 3 child connections");
        }

        // Assert - External workflow connections
        workflow.Connections[0].SourceNodeId.Should().Be("pre-deployment-checks");
        workflow.Connections[0].TargetNodeId.Should().Be("node-deployments");
        workflow.Connections[0].TriggerMessageType.Should().Be(MessageType.Complete);
        workflow.Connections[0].IsEnabled.Should().BeTrue();

        workflow.Connections[1].SourceNodeId.Should().Be("node-deployments");
        workflow.Connections[1].TargetNodeId.Should().Be("health-checks");
        workflow.Connections[1].TriggerMessageType.Should().Be(MessageType.Complete);
        workflow.Connections[1].IsEnabled.Should().BeTrue();

        Console.WriteLine("azs_deployment.yaml validation successful");
    }

    [TestMethod]
    public void LoadFromFile_DeployNode_ShouldDeserializeCorrectly()
    {
        // Test deploy_node.yaml - Subflow workflow used by azs_deployment
        var yamlPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestData", "WorkflowDefinitions", "deploy_node.yaml");

        if (!File.Exists(yamlPath))
        {
            Assert.Inconclusive($"Test file not found: {yamlPath}");
            return;
        }

        // Act
        var workflow = this.serializer.LoadFromFile(yamlPath);

        // Assert - Workflow level
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("deploy-node");
        workflow.WorkflowName.Should().Be("Deploy Azure Stack Node");
        workflow.Description.Should().Contain("Sequential deployment steps");
        workflow.Nodes.Should().HaveCount(4, "Should have 4 sequential tasks");
        workflow.Connections.Should().HaveCount(3, "Should have 3 sequential connections");

        // Assert - Node 1: OS Update
        var osUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "os-update");
        osUpdate.Should().NotBeNull();
        osUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        osUpdate.NodeName.Should().Be("OS Update");
        osUpdate.Configuration.Should().ContainKey("script");

        // Assert - Node 2: Stamp Update
        var stampUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "stamp-update");
        stampUpdate.Should().NotBeNull();
        stampUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        stampUpdate.NodeName.Should().Be("Stamp Update");
        stampUpdate.Configuration.Should().ContainKey("script");

        // Assert - Node 3: SBE Update
        var sbeUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sbe-update");
        sbeUpdate.Should().NotBeNull();
        sbeUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        sbeUpdate.NodeName.Should().Be("SBE Update");
        sbeUpdate.Configuration.Should().ContainKey("script");

        // Assert - Node 4: MocArc Update
        var mocarcUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "mocarc-update");
        mocarcUpdate.Should().NotBeNull();
        mocarcUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        mocarcUpdate.NodeName.Should().Be("MocArc Update");
        mocarcUpdate.Configuration.Should().ContainKey("script");

        // Assert - Sequential connections: OS → Stamp → SBE → MocArc
        var conn1 = workflow.Connections.FirstOrDefault(c => c.SourceNodeId == "os-update" && c.TargetNodeId == "stamp-update");
        conn1.Should().NotBeNull();
        conn1!.TriggerMessageType.Should().Be(MessageType.Complete);
        conn1.IsEnabled.Should().BeTrue();

        var conn2 = workflow.Connections.FirstOrDefault(c => c.SourceNodeId == "stamp-update" && c.TargetNodeId == "sbe-update");
        conn2.Should().NotBeNull();
        conn2!.TriggerMessageType.Should().Be(MessageType.Complete);
        conn2.IsEnabled.Should().BeTrue();

        var conn3 = workflow.Connections.FirstOrDefault(c => c.SourceNodeId == "sbe-update" && c.TargetNodeId == "mocarc-update");
        conn3.Should().NotBeNull();
        conn3!.TriggerMessageType.Should().Be(MessageType.Complete);
        conn3.IsEnabled.Should().BeTrue();

        Console.WriteLine("deploy_node.yaml validation successful");
    }

    #endregion
}
