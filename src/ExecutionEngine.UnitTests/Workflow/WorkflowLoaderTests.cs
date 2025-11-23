// -----------------------------------------------------------------------
// <copyright file="WorkflowLoaderTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowLoaderTests
{
    private WorkflowLoader loader = null!;
    private string testDirectory = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        this.loader = new WorkflowLoader();
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"WorkflowLoaderTests_{Guid.NewGuid()}");
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

    private WorkflowDefinition CreateValidWorkflow()
    {
        return new WorkflowDefinition
        {
            WorkflowId = "valid-workflow",
            WorkflowName = "Valid Workflow",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "node-1",
                    ScriptPath = @"TestData\scripts\script.csx"
                }
            }
        };
    }

    private WorkflowDefinition CreateInvalidWorkflow()
    {
        return new WorkflowDefinition
        {
            WorkflowId = string.Empty, // Invalid - empty ID
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "node-1",
                }
            }
        };
    }

    [TestMethod]
    public void Load_WithValidWorkflow_ShouldLoadAndValidate()
    {
        // Arrange
        var workflow = this.CreateValidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "valid.json");
        this.loader.Save(workflow, filePath, validateBeforeSave: false);

        // Act
        var loaded = this.loader.Load(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded.WorkflowId.Should().Be("valid-workflow");
    }

    [TestMethod]
    public void Load_WithInvalidWorkflow_WhenValidationEnabled_ShouldThrowException()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "invalid.json");
        this.loader.Save(workflow, filePath, validateBeforeSave: false);

        // Act
        Action act = () => this.loader.Load(filePath, validateOnLoad: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [TestMethod]
    public void Load_WithInvalidWorkflow_WhenValidationDisabled_ShouldLoad()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "invalid.json");
        this.loader.Save(workflow, filePath, validateBeforeSave: false);

        // Act
        var loaded = this.loader.Load(filePath, validateOnLoad: false);

        // Assert
        loaded.Should().NotBeNull();
    }

    [TestMethod]
    public void Save_WithValidWorkflow_ShouldSave()
    {
        // Arrange
        var workflow = this.CreateValidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "output.json");

        // Act
        this.loader.Save(workflow, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
    }

    [TestMethod]
    public void Save_WithInvalidWorkflow_WhenValidationEnabled_ShouldThrowException()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "output.json");

        // Act
        var act = () => this.loader.Save(workflow, filePath, validateBeforeSave: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [TestMethod]
    public void Save_WithInvalidWorkflow_WhenValidationDisabled_ShouldSave()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "output.json");

        // Act
        this.loader.Save(workflow, filePath, validateBeforeSave: false);

        // Assert
        File.Exists(filePath).Should().BeTrue();
    }

    [TestMethod]
    public void Validate_WithValidWorkflow_ShouldReturnValid()
    {
        // Arrange
        var workflow = this.CreateValidWorkflow();

        // Act
        var result = this.loader.Validate(workflow);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void Validate_WithInvalidWorkflow_ShouldReturnInvalid()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();

        // Act
        var result = this.loader.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [TestMethod]
    public void LoadWithoutValidation_ShouldLoadWorkflow()
    {
        // Arrange
        var workflow = this.CreateInvalidWorkflow();
        var filePath = Path.Combine(this.testDirectory, "test.json");
        this.loader.Save(workflow, filePath, validateBeforeSave: false);

        // Act
        var loaded = this.loader.LoadWithoutValidation(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded.WorkflowId.Should().Be(workflow.WorkflowId);
    }

    [TestMethod]
    public void Load_JsonAndYamlRoundTrip_ShouldBeEquivalent()
    {
        // Arrange
        var original = this.CreateValidWorkflow();
        var jsonPath = Path.Combine(this.testDirectory, "test.json");
        var yamlPath = Path.Combine(this.testDirectory, "test.yaml");

        // Act
        this.loader.Save(original, jsonPath);
        this.loader.Save(original, yamlPath);

        var fromJson = this.loader.Load(jsonPath);
        var fromYaml = this.loader.Load(yamlPath);

        // Assert
        fromJson.WorkflowId.Should().Be(fromYaml.WorkflowId);
        fromJson.WorkflowName.Should().Be(fromYaml.WorkflowName);
        fromJson.Nodes.Should().HaveCount(fromYaml.Nodes.Count);
    }

    [TestMethod]
    public void Load_WithSampleJsonFile_ShouldLoadSuccessfully()
    {
        // Arrange - Use the sample file from Samples directory
        var samplesPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
             "TestData", "WorkflowDefinitions", "simple-workflow.json"));

        // Only run test if sample file exists
        if (!File.Exists(samplesPath))
        {
            Assert.Inconclusive($"Sample file not found at: {samplesPath}");
            return;
        }

        // Act
        var workflow = this.loader.LoadWithoutValidation(samplesPath);

        // Assert
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("simple-workflow");
        workflow.Nodes.Should().NotBeEmpty();
        workflow.Connections.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Load_WithSampleYamlFile_ShouldLoadSuccessfully()
    {
        // Arrange - Use the sample file from Samples directory
        var samplesPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "TestData", "WorkflowDefinitions", "simple-workflow.yaml"));

        // Only run test if sample file exists
        if (!File.Exists(samplesPath))
        {
            Assert.Inconclusive($"Sample file not found at: {samplesPath}");
            return;
        }

        // Act
        var workflow = this.loader.LoadWithoutValidation(samplesPath);

        // Assert
        workflow.Should().NotBeNull();
        workflow.WorkflowId.Should().Be("simple-workflow");
        workflow.Nodes.Should().NotBeEmpty();
        workflow.Connections.Should().NotBeEmpty();
    }
}
