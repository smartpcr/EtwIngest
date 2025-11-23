// -----------------------------------------------------------------------
// <copyright file="SubflowNodeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Nodes;

using System.Text.Json;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class SubflowNodeTests
{
    [TestMethod]
    public async Task ExecuteAsync_LoadAndExecuteChildWorkflow_ReturnsSuccess()
    {
        // Arrange
        var childWorkflow = this.CreateSimpleChildWorkflow("child-workflow");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        node.ChildWorkflowContext.Should().NotBeNull();
        node.ChildWorkflowContext!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        nodeContext.OutputData["ChildWorkflowId"].Should().Be("child-workflow");
        nodeContext.OutputData["ChildWorkflowStatus"].Should().Be("Completed");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInputMappings_PassesVariablesToChild()
    {
        // Arrange
        var childWorkflow = this.CreateChildWorkflowThatReadsVariable("child-workflow", "childVar");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow,
            InputMappings = new Dictionary<string, string>
            {
                { "parentVar", "childVar" }
            }
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["parentVar"] = "test-value-123";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        node.ChildWorkflowContext.Should().NotBeNull();
        node.ChildWorkflowContext!.Variables["childVar"].Should().Be("test-value-123");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithOutputMappings_CapturesChildResults()
    {
        // Arrange
        var childWorkflow = this.CreateChildWorkflowThatSetsVariable("child-workflow", "childResult", "child-output");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow,
            OutputMappings = new Dictionary<string, string>
            {
                { "childResult", "parentResult" }
            }
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        workflowContext.Variables["parentResult"].Should().Be("child-output");
    }

    [TestMethod]
    public async Task ExecuteAsync_ChildWorkflowFails_PropagatesFailure()
    {
        // Arrange
        var childWorkflow = this.CreateFailingChildWorkflow("child-workflow");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("Child workflow");
        instance.ErrorMessage.Should().Contain("failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextIsolation_ParentVarsNotVisibleToChild()
    {
        // Arrange
        var childWorkflow = this.CreateSimpleChildWorkflow("child-workflow");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
            // Note: No input mappings
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["parentSecret"] = "should-not-be-visible";
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        node.ChildWorkflowContext.Should().NotBeNull();
        node.ChildWorkflowContext!.Variables.Should().NotContainKey("parentSecret");
    }

    [TestMethod]
    public async Task ExecuteAsync_NestedSubflows_ThreeLevelDeep()
    {
        // Arrange - Create three-level nested workflow structure
        var level3Workflow = this.CreateChildWorkflowThatSetsVariable("level3", "level3Result", "level3-value");

        var level2Workflow = new WorkflowDefinition
        {
            WorkflowId = "level2",
            EntryPointNodeId = "subflow-level3",
            Nodes = new List<NodeDefinition>
            {
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-level3",
                    Configuration = new Dictionary<string, object>
                    {
                        { "ChildWorkflowDefinition", level3Workflow },
                        { "OutputMappings", new Dictionary<string, string> { { "level3Result", "level2Result" } } }
                    }
                }
            }
        };

        var level1Workflow = new WorkflowDefinition
        {
            WorkflowId = "level1",
            EntryPointNodeId = "subflow-level2",
            Nodes = new List<NodeDefinition>
            {
                new SubflowNodeDefinition
                {
                    NodeId = "subflow-level2",
                    Configuration = new Dictionary<string, object>
                    {
                        { "ChildWorkflowDefinition", level2Workflow },
                        { "OutputMappings", new Dictionary<string, string> { { "level2Result", "level1Result" } } }
                    }
                }
            }
        };

        var node = new SubflowNode
        {
            ChildWorkflowDefinition = level1Workflow,
            OutputMappings = new Dictionary<string, string>
            {
                { "level1Result", "topLevelResult" }
            }
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-top" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        workflowContext.Variables["topLevelResult"].Should().Be("level3-value");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTimeout_ChildExceedsTimeout()
    {
        // Arrange - Create a child workflow with long-running task
        var childWorkflow = this.CreateLongRunningChildWorkflow("child-workflow", TimeSpan.FromSeconds(5));
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow,
            Timeout = TimeSpan.FromMilliseconds(100) // Very short timeout
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("workflow");
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancellation_CancelsChildWorkflow()
    {
        // Arrange
        var childWorkflow = this.CreateLongRunningChildWorkflow("child-workflow", TimeSpan.FromSeconds(10));
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();
        var cts = new CancellationTokenSource();

        // Act - Cancel after short delay (longer to ensure workflow has started)
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, cts.Token);

        // Assert
        instance.Should().NotBeNull();
        // Should be cancelled or failed (timing-dependent, but error should mention cancellation)
        (instance.Status == NodeExecutionStatus.Cancelled || instance.Status == NodeExecutionStatus.Failed).Should().BeTrue();
        if (instance.Status == NodeExecutionStatus.Cancelled)
        {
            instance.ErrorMessage.Should().Contain("cancelled");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_LoadFromFile_LoadsWorkflowDefinition()
    {
        // Arrange - Create a temporary workflow file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-workflow-{Guid.NewGuid()}.json");
        var childWorkflow = this.CreateSimpleChildWorkflow("file-workflow");
        // Use WorkflowSerializer to ensure consistent serialization format (camelCase)
        var serializer = new WorkflowSerializer();
        var json = serializer.ToJson(childWorkflow);
        await File.WriteAllTextAsync(tempFile, json);

        try
        {
            var node = new SubflowNode
            {
                WorkflowFilePath = tempFile
            };
            var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
            node.Initialize(definition);

            var workflowContext = new WorkflowExecutionContext();
            var nodeContext = new NodeExecutionContext();

            // Act
            var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

            // Assert
            instance.Should().NotBeNull();
            instance.Status.Should().Be(NodeExecutionStatus.Completed);
            node.ChildWorkflowContext.Should().NotBeNull();
            node.ChildWorkflowContext!.GraphId.Should().Be("file-workflow");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FileNotFound_ShouldFail()
    {
        // Arrange
        var node = new SubflowNode
        {
            WorkflowFilePath = "/nonexistent/workflow.json",
            SkipValidation = true // Skip validation to test runtime error
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("not found");
    }

    [TestMethod]
    public async Task ExecuteAsync_NoWorkflowProvided_ShouldFail()
    {
        // Arrange
        var node = new SubflowNode
        {
            // No ChildWorkflowDefinition or WorkflowFilePath
            SkipValidation = true // Skip validation to test runtime error
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Failed);
        instance.ErrorMessage.Should().Contain("not provided");
    }

    [TestMethod]
    public async Task ExecuteAsync_ChildOutputData_AvailableInNodeContext()
    {
        // Arrange
        var childWorkflow = this.CreateChildWorkflowThatSetsVariable("child-workflow", "result", "test-result");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        instance.Should().NotBeNull();
        instance.Status.Should().Be(NodeExecutionStatus.Completed);
        nodeContext.OutputData.Should().ContainKey("ChildWorkflowId");
        nodeContext.OutputData.Should().ContainKey("ChildWorkflowInstanceId");
        nodeContext.OutputData.Should().ContainKey("ChildWorkflowStatus");
        nodeContext.OutputData.Should().ContainKey("ChildOutputData");

        var childOutputData = nodeContext.OutputData["ChildOutputData"] as Dictionary<string, object>;
        childOutputData.Should().NotBeNull();
        childOutputData!["result"].Should().Be("test-result");
    }

    [TestMethod]
    public void Initialize_WithConfiguration_SetsProperties()
    {
        // Arrange
        var node = new SubflowNode
        {
            SkipValidation = true // Skip validation for unit test
        };
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>
            {
                { "WorkflowFilePath", "/path/to/workflow.json" },
                { "InputMappings", new Dictionary<string, string> { { "a", "b" } } },
                { "OutputMappings", new Dictionary<string, string> { { "x", "y" } } },
                { "Timeout", 5000 }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.WorkflowFilePath.Should().Be("/path/to/workflow.json");
        node.InputMappings.Should().ContainKey("a");
        node.OutputMappings.Should().ContainKey("x");
        node.Timeout.Should().Be(TimeSpan.FromMilliseconds(5000));
    }

    [TestMethod]
    public void Initialize_WithObjectDictionary_ConvertsMappings()
    {
        // Arrange
        var node = new SubflowNode
        {
            SkipValidation = true // Skip validation for unit test
        };
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>
            {
                { "InputMappings", new Dictionary<string, object> { { "parent", "child" } } },
                { "OutputMappings", new Dictionary<string, object> { { "child", "parent" } } }
            }
        };

        // Act
        node.Initialize(definition);

        // Assert
        node.InputMappings.Should().ContainKey("parent");
        node.InputMappings["parent"].Should().Be("child");
        node.OutputMappings.Should().ContainKey("child");
        node.OutputMappings["child"].Should().Be("parent");
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRaiseOnStartEvent()
    {
        // Arrange
        var childWorkflow = this.CreateSimpleChildWorkflow("child-workflow");
        var node = new SubflowNode
        {
            ChildWorkflowDefinition = childWorkflow
        };
        var definition = new SubflowNodeDefinition { NodeId = "subflow-1" };
        node.Initialize(definition);

        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var eventRaised = false;
        node.OnStart += (sender, args) => eventRaised = true;

        // Act
        await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [TestMethod]
    public void NodeFactory_CreateSubflowNode_ShouldSucceed()
    {
        // Arrange
        var factory = new NodeFactory();
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>
            {
                { "WorkflowFilePath", "/path/to/workflow.json" },
                { "SkipValidation", true } // Skip validation for unit test
            }
        };

        // Act
        var node = factory.CreateNode(definition);

        // Assert
        node.Should().NotBeNull();
        node.Should().BeOfType<SubflowNode>();
        node.NodeId.Should().Be("subflow-1");
    }

    [TestMethod]
    public void Initialize_WithInvalidFilePath_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var node = new SubflowNode();
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>
            {
                { "WorkflowFilePath", "/nonexistent/workflow.yaml" }
                // SkipValidation not set, so validation will run
            }
        };

        // Act & Assert
        var action = () => node.Initialize(definition);
        action.Should().Throw<FileNotFoundException>()
            .WithMessage("*Child workflow file not found*");
    }

    [TestMethod]
    public void Initialize_WithNoWorkflowProvided_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var node = new SubflowNode();
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>()
            // No WorkflowFilePath or ChildWorkflowDefinition
            // SkipValidation not set, so validation will run
        };

        // Act & Assert
        var action = () => node.Initialize(definition);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Either ChildWorkflowDefinition or WorkflowFilePath must be provided*");
    }

    [TestMethod]
    public void Initialize_WithValidWorkflow_ShouldValidateSuccessfully()
    {
        // Arrange
        var childWorkflow = this.CreateSimpleChildWorkflow("test-child");
        var node = new SubflowNode();
        var definition = new SubflowNodeDefinition
        {
            NodeId = "subflow-1",
            Configuration = new Dictionary<string, object>
            {
                { "ChildWorkflowDefinition", childWorkflow }
                // SkipValidation not set, validation will run and should succeed
            }
        };

        // Act
        var action = () => node.Initialize(definition);

        // Assert
        action.Should().NotThrow();
        node.ChildWorkflowDefinition.Should().NotBeNull();
        node.ChildWorkflowDefinition!.WorkflowId.Should().Be("test-child");
    }

    // Helper methods to create test workflows

    private WorkflowDefinition CreateSimpleChildWorkflow(string workflowId)
    {
        return new WorkflowDefinition
        {
            WorkflowId = workflowId,
            EntryPointNodeId = "task-1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "// Simple task" }
                    }
                }
            }
        };
    }

    private WorkflowDefinition CreateChildWorkflowThatReadsVariable(string workflowId, string variableName)
    {
        return new WorkflowDefinition
        {
            WorkflowId = workflowId,
            EntryPointNodeId = "task-1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", $"var value = GetGlobal(\"{variableName}\");" }
                    }
                }
            }
        };
    }

    private WorkflowDefinition CreateChildWorkflowThatSetsVariable(string workflowId, string variableName, string value)
    {
        return new WorkflowDefinition
        {
            WorkflowId = workflowId,
            EntryPointNodeId = "task-1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", $"SetGlobal(\"{variableName}\", \"{value}\");" }
                    }
                }
            }
        };
    }

    private WorkflowDefinition CreateFailingChildWorkflow(string workflowId)
    {
        return new WorkflowDefinition
        {
            WorkflowId = workflowId,
            EntryPointNodeId = "task-1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", "throw new Exception(\"Child workflow failure\");" }
                    }
                }
            }
        };
    }

    private WorkflowDefinition CreateLongRunningChildWorkflow(string workflowId, TimeSpan duration)
    {
        return new WorkflowDefinition
        {
            WorkflowId = workflowId,
            EntryPointNodeId = "task-1",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-1",
                    Configuration = new Dictionary<string, object>
                    {
                        { "script", $"await Task.Delay({(int)duration.TotalMilliseconds});" }
                    }
                }
            }
        };
    }
}
