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
                    RuntimeType = "CSharpScript",
                    ScriptPath = "script1.csx"
                },
                new NodeDefinition
                {
                    NodeId = "node-2",
                    NodeName = "Second Node",
                    RuntimeType = "PowerShell",
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
}
