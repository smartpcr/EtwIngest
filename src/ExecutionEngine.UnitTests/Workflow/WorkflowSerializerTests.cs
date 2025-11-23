// -----------------------------------------------------------------------
// <copyright file="WorkflowSerializerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowSerializerTests
{
    private readonly WorkflowSerializer serializer = new WorkflowSerializer();
    private readonly HashSet<string> testDirectories = new HashSet<string>();

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"WorkflowSerializerTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDirectory);
        this.testDirectories.Add(testDirectory);
        this.TestContext.Properties["TestDirectory"] = testDirectory;
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach(var testDirectory in this.testDirectories)
        {
            var retryCount = 0;
            while (Directory.Exists(testDirectory) && retryCount < 3)
            {
                try
                {
                    Directory.Delete(testDirectory, true);
                }
                catch (IOException)
                {
                    // Wait and retry
                    Thread.Sleep(100);
                    retryCount++;
                }
                catch (UnauthorizedAccessException)
                {
                    // Wait and retry
                    Thread.Sleep(100);
                    retryCount++;
                }
            }
        }
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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.json");

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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.yaml");

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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.yml");

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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.txt");

        // Act
        var act = () => this.serializer.SaveToFile(workflow, filePath);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported*");
    }

    [TestMethod]
    public void SaveToFile_WithNullWorkflow_ShouldThrowException()
    {
        // Arrange
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.json");

        // Act
        var act = () => this.serializer.SaveToFile(null!, filePath);

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
        var act = () => this.serializer.SaveToFile(workflow, string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("filePath");
    }

    [TestMethod]
    public void SaveToFile_WithNonExistentDirectory_ShouldCreateDirectory()
    {
        // Arrange
        var workflow = this.CreateSampleWorkflow();
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var subdirectory = Path.Combine(testDirectory, "subdir1", "subdir2");
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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.json");
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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.yaml");
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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "nonexistent.json");

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
        var testDirectory = this.TestContext.Properties["TestDirectory"]!.ToString()!;
        var filePath = Path.Combine(testDirectory, "test.txt");
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
        var containerNode = workflow.Nodes[0] as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        containerNode!.NodeId.Should().Be("container1");
        containerNode.NodeName.Should().Be("Test Container");
        containerNode.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.ExecutionMode.Should().Be(ExecutionMode.Sequential);

        // Assert - ChildNodes
        var childNodes = containerNode.ChildNodes;
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
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull("ChildConnections should be present in Configuration");
        childConnections.Should().HaveCount(1, "Container should have 1 child connection");
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
        var containerNode = workflow.Nodes[0] as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        var nodeDefList = containerNode!.ChildNodes;
        nodeDefList.Should().HaveCount(1);
        nodeDefList![0].NodeId.Should().Be("task1");
        nodeDefList[0].NodeName.Should().Be("Task 1");
        nodeDefList[0].RuntimeType.Should().Be(RuntimeType.CSharpTask);
        Console.WriteLine("SUCCESS: ChildNodes is List<NodeDefinition>");
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

        var containerNode = workflow.Nodes[0] as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);

        var childNodes = containerNode.ChildNodes;
        childNodes.Should().NotBeNull();

        // After deserialization, ChildNodes should be properly typed as List<NodeDefinition>
        childNodes.Should().BeOfType<List<NodeDefinition>>("ChildNodes should be deserialized as List<NodeDefinition>");

        var childList = childNodes as List<NodeDefinition>;
        childList.Should().HaveCount(2, "Should have 2 child nodes");

        // Verify first child node (check-network)
        var firstChild = childList![0] as CSharpNodeDefinition;
        firstChild.Should().NotBeNull();
        firstChild!.NodeId.Should().Be("check-network");
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
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull();
        childConnections.Should().BeOfType<List<NodeConnection>>("ChildConnections should be deserialized as List<NodeConnection>");
        childConnections.Should().HaveCount(1, "Should have 1 child connection");

        var conn = childConnections![0];
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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "parallel-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull("Container node should exist");
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.NodeName.Should().Be("Parallel Container");
        containerNode.ExecutionMode.Should().Be(ExecutionMode.Parallel);

        // Assert - Child nodes
        var childNodes = containerNode.ChildNodes;
        childNodes.Should().NotBeNull();
        childNodes.Should().HaveCount(3, "Container should have 3 child nodes");
        foreach (var child in childNodes!)
        {
            child.Should().NotBeNull();
            // YAML deserializes to dictionaries, not NodeDefinition objects
            Console.WriteLine($"Child type: {child.GetType().FullName}");
        }

        // Assert - Child connections (empty for parallel execution)
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull();
        childConnections.Should().BeEmpty("Parallel execution has no child connections");

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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sequential-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.ExecutionMode.Should().Be(ExecutionMode.Sequential);

        // Assert - Child nodes
        var childNodes = containerNode.ChildNodes;
        childNodes.Should().NotBeNull();
        childNodes.Should().HaveCount(3, "Should have 3 sequential steps");

        // Child nodes from YAML are dictionaries, not NodeDefinition objects
        // They get converted by ContainerNode.ParseChildNodes during initialization
        foreach (var child in childNodes!)
        {
            child.Should().NotBeNull();
            Console.WriteLine($"Child type: {child.GetType().FullName}");
        }

        // Assert - Child connections
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull();
        childConnections.Should().HaveCount(2, "Sequential execution should have 2 connections");

        // Verify connections are NodeConnection objects
        foreach (var conn in childConnections)
        {
            conn.TriggerMessageType.Should().Be(MessageType.Complete);
            conn.IsEnabled.Should().BeTrue();
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
        var subflow1 = workflow.Nodes.FirstOrDefault(n => n.NodeId == "subflow1") as SubflowNodeDefinition;
        var subflow2 = workflow.Nodes.FirstOrDefault(n => n.NodeId == "subflow2") as SubflowNodeDefinition;

        subflow1.Should().NotBeNull();
        subflow2.Should().NotBeNull();

        subflow1!.RuntimeType.Should().Be(RuntimeType.Subflow);
        subflow2!.RuntimeType.Should().Be(RuntimeType.Subflow);

        // Assert - Subflow configuration
        subflow1.WorkflowFilePath.Should().NotBeNullOrEmpty();
        subflow1.InputMappings.Should().NotBeNullOrEmpty();
        subflow1.OutputMappings.Should().NotBeNullOrEmpty();

        subflow1.WorkflowFilePath.Should().Be("./subworkflow1.yaml");
        subflow2.WorkflowFilePath.Should().Be("./subworkflow2.yaml");

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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "deployment-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        containerNode!.RuntimeType.Should().Be(RuntimeType.Container);
        containerNode.ExecutionMode.Should().Be(ExecutionMode.Parallel);

        // Assert - Child nodes (Subflows)
        var childNodes = containerNode.ChildNodes;
        childNodes.Should().NotBeNull();
        childNodes.Should().HaveCount(3, "Should have 3 subflow children");

        // Child nodes from YAML are dictionaries, not NodeDefinition objects
        // They get converted by ContainerNode.ParseChildNodes during initialization
        foreach (var child in childNodes!)
        {
            child.Should().NotBeNull();
            Console.WriteLine($"Child type: {child.GetType().FullName}");
        }

        // Assert - Child connections (empty for parallel)
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull();
        childConnections.Should().BeEmpty("Parallel execution has no child connections");
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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "parallel-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();

        var childList = containerNode!.ChildNodes;
        childList.Should().NotBeNull();
        childList.Should().BeOfType<List<NodeDefinition>>("ChildNodes should be deserialized as List<NodeDefinition>");
        childList.Should().HaveCount(3, "Container should have 3 child nodes");

        // Verify each child is a properly typed NodeDefinition
        childList![0].NodeId.Should().Be("child1");
        childList[0].NodeName.Should().Be("Child Task 1");
        childList[0].RuntimeType.Should().Be(RuntimeType.CSharpTask);
        var csharpNode1 = childList[0] as CSharpTaskNodeDefinition;
        csharpNode1.Should().NotBeNull();
        csharpNode1!.ScriptContent.Should().NotBeNullOrEmpty();

        childList[1].NodeId.Should().Be("child2");
        childList[1].NodeName.Should().Be("Child Task 2");

        childList[2].NodeId.Should().Be("child3");
        childList[2].NodeName.Should().Be("Child Task 3");

        Console.WriteLine($"All 3 child nodes properly deserialized as NodeDefinition objects");

        // Verify ChildConnections are also properly typed
        var childConnections = containerNode.ChildConnections;
        childConnections.Should().NotBeNull();
        childConnections.Should().BeOfType<List<NodeConnection>>("ChildConnections should be deserialized as List<NodeConnection>");
        childConnections.Should().BeEmpty("Parallel container has no child connections");
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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sequential-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();
        var childConnections = containerNode!.ChildConnections;
        childConnections.Should().HaveCount(2, "Sequential container should have 2 child connections");
        foreach (var conn in childConnections!)
        {
            conn.Should().NotBeNull();
            conn.SourceNodeId.Should().NotBeNullOrEmpty();
            conn.TargetNodeId.Should().NotBeNullOrEmpty();
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
        var containerNode = workflow.Nodes.FirstOrDefault(n => n.NodeId == "deployment-container") as ContainerNodeDefinition;
        containerNode.Should().NotBeNull();

        var childNodes = containerNode!.ChildNodes;
        childNodes.Should().NotBeNull();
        childNodes.Should().HaveCount(3, "Container should have 3 subflow children");

        // Check first subflow child structure
        var firstChild = childNodes![0] as SubflowNodeDefinition;
        firstChild.Should().NotBeNull();
        firstChild!.NodeId.Should().Be("deploy-region1");
        firstChild.NodeName.Should().Be("Deploy Region 1");
        firstChild.RuntimeType.Should().Be(RuntimeType.Subflow, "Child nodes should be Subflow type");
        firstChild.WorkflowFilePath.Should().NotBeNullOrEmpty();
        firstChild.InputMappings.Should().NotBeNullOrEmpty();
        firstChild.OutputMappings.Should().NotBeNullOrEmpty();
        firstChild.WorkflowFilePath.Should().Be("./deploy-region.yaml");
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
        var preDeploymentContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "pre-deployment-checks") as ContainerNodeDefinition;
        preDeploymentContainer.Should().NotBeNull();
        preDeploymentContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        preDeploymentContainer.NodeName.Should().Be("Pre-Deployment Checks");
        preDeploymentContainer.ExecutionMode.Should().Be(ExecutionMode.Sequential);

        var preDeploymentChildren = preDeploymentContainer.ChildNodes;
        preDeploymentChildren.Should().NotBeNull();
        preDeploymentChildren.Should().HaveCount(3, "Pre-deployment should have 3 checks");

        var preDeploymentConnections = preDeploymentContainer.ChildConnections;
        preDeploymentConnections.Should().NotBeNull();
        preDeploymentConnections.Should().HaveCount(2, "Sequential execution should have 2 child connections");

        // Assert - Phase 2: Node Deployments Container (Parallel Subflows)
        var nodeDeploymentsContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "node-deployments") as ContainerNodeDefinition;
        nodeDeploymentsContainer.Should().NotBeNull();
        nodeDeploymentsContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        nodeDeploymentsContainer.NodeName.Should().Be("Node Deployments");
        nodeDeploymentsContainer.ExecutionMode.Should().Be(ExecutionMode.Parallel);

        var nodeDeploymentChildren = nodeDeploymentsContainer.ChildNodes;
        nodeDeploymentChildren.Should().NotBeNull();
        nodeDeploymentChildren.Should().HaveCount(3, "Should have 3 parallel subflow deployments");

        var nodeDeploymentConnections = nodeDeploymentsContainer.ChildConnections;
        nodeDeploymentConnections.Should().NotBeNull();
        nodeDeploymentConnections.Should().BeEmpty("Parallel execution should have no child connections");

        // Assert - Phase 3: Post-deployment Health Checks Container
        var healthChecksContainer = workflow.Nodes.FirstOrDefault(n => n.NodeId == "health-checks") as ContainerNodeDefinition;
        healthChecksContainer.Should().NotBeNull();
        healthChecksContainer!.RuntimeType.Should().Be(RuntimeType.Container);
        healthChecksContainer.NodeName.Should().Be("Post-Deployment Health Checks");
        healthChecksContainer.ExecutionMode.Should().Be(ExecutionMode.Sequential);

        var healthCheckChildren = healthChecksContainer.ChildNodes;
        healthCheckChildren.Should().NotBeNull();
        healthCheckChildren.Should().HaveCount(4, "Health checks should have 4 service checks");

        var healthCheckConnections = healthChecksContainer.ChildConnections;
        healthCheckConnections.Should().NotBeNull();
        healthCheckConnections.Should().HaveCount(3, "Sequential execution should have 3 child connections");

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
        var osUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "os-update") as CSharpTaskNodeDefinition;
        osUpdate.Should().NotBeNull();
        osUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        osUpdate.NodeName.Should().Be("OS Update");
        osUpdate.ScriptContent.Should().NotBeNullOrEmpty();

        // Assert - Node 2: Stamp Update
        var stampUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "stamp-update") as CSharpTaskNodeDefinition;
        stampUpdate.Should().NotBeNull();
        stampUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        stampUpdate.NodeName.Should().Be("Stamp Update");
        stampUpdate.ScriptContent.Should().NotBeNullOrEmpty();

        // Assert - Node 3: SBE Update
        var sbeUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "sbe-update") as CSharpTaskNodeDefinition;
        sbeUpdate.Should().NotBeNull();
        sbeUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        sbeUpdate.NodeName.Should().Be("SBE Update");
        sbeUpdate.ScriptContent.Should().NotBeNullOrEmpty();

        // Assert - Node 4: MocArc Update
        var mocarcUpdate = workflow.Nodes.FirstOrDefault(n => n.NodeId == "mocarc-update") as CSharpTaskNodeDefinition;
        mocarcUpdate.Should().NotBeNull();
        mocarcUpdate!.RuntimeType.Should().Be(RuntimeType.CSharpTask);
        mocarcUpdate.NodeName.Should().Be("MocArc Update");
        mocarcUpdate.ScriptContent.Should().NotBeNullOrEmpty();

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
                new CSharpScriptNodeDefinition
                {
                    NodeId = "node-1",
                    NodeName = "First Node",
                    ScriptPath = "script1.csx"
                },
                new PowerShellScriptNodeDefinition
                {
                    NodeId = "node-2",
                    NodeName = "Second Node",
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

}
