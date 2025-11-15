// -----------------------------------------------------------------------
// <copyright file="WorkflowPauseResumeTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Engine;

using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Persistence;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowPauseResumeTests
{
    [TestMethod]
    public async Task PauseAsync_WithoutCheckpointStorage_ShouldThrowException()
    {
        // Arrange
        var engine = new WorkflowEngine(); // No checkpoint storage
        var instanceId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await engine.PauseAsync(instanceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*checkpoint storage not configured*");
    }

    [TestMethod]
    public async Task PauseAsync_NonExistentWorkflow_ShouldThrowException()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var engine = new WorkflowEngine(storage);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await engine.PauseAsync(nonExistentId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Workflow {nonExistentId} not found*");
    }

    [TestMethod]
    public async Task ResumeAsync_WithoutCheckpointStorage_ShouldThrowException()
    {
        // Arrange
        var engine = new WorkflowEngine(); // No checkpoint storage
        var instanceId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await engine.ResumeAsync(instanceId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*checkpoint storage not configured*");
    }

    [TestMethod]
    public async Task ResumeAsync_NonExistentCheckpoint_ShouldThrowException()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var engine = new WorkflowEngine(storage);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await engine.ResumeAsync(nonExistentId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Checkpoint not found for workflow {nonExistentId}*");
    }

    [TestMethod]
    public async Task RecoverIncompleteWorkflowsAsync_WithCompletedWorkflows_ShouldSkipThem()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();

        // Create completed checkpoint - should be skipped by recovery
        var checkpoint = CreateTestCheckpoint(WorkflowExecutionStatus.Completed);
        await storage.SaveCheckpointAsync(checkpoint);

        var engine = new WorkflowEngine(storage);

        // Act
        var recoveredIds = await engine.RecoverIncompleteWorkflowsAsync();

        // Assert - Completed workflows should not be recovered
        recoveredIds.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RecoverIncompleteWorkflowsAsync_NoCheckpointStorage_ShouldReturnEmpty()
    {
        // Arrange
        var engine = new WorkflowEngine(); // No checkpoint storage

        // Act
        var recoveredIds = await engine.RecoverIncompleteWorkflowsAsync();

        // Assert
        recoveredIds.Should().BeEmpty();
    }

    private static WorkflowDefinition CreateSimpleWorkflow()
    {
        return new WorkflowDefinition
        {
            WorkflowId = "test-workflow",
            WorkflowName = "Test Workflow",
            Nodes = new List<NodeDefinition>
            {
                new NodeDefinition
                {
                    NodeId = "node1",
                    NodeName = "Test Node",
                    RuntimeType = ExecutionEngine.Enums.RuntimeType.CSharpScript,
                    Configuration = new Dictionary<string, object>
                    {
                        { "Script", "return \"Hello World\";" }
                    }
                }
            },
            Connections = new List<NodeConnection>(),
            EntryPointNodeId = "node1"
        };
    }

    private static WorkflowCheckpoint CreateTestCheckpoint(WorkflowExecutionStatus status)
    {
        return new WorkflowCheckpoint
        {
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowId = "test-workflow",
            WorkflowDefinitionJson = System.Text.Json.JsonSerializer.Serialize(CreateSimpleWorkflow()),
            Status = status,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            CheckpointTime = DateTime.UtcNow,
            Variables = new Dictionary<string, object>(),
            NodeStates = new Dictionary<string, NodeInstanceState>(),
            MessageQueues = new Dictionary<string, List<SerializedMessage>>()
        };
    }
}
