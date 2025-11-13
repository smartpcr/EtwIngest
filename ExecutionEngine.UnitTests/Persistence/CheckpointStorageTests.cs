// -----------------------------------------------------------------------
// <copyright file="CheckpointStorageTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Persistence;

using ExecutionEngine.Enums;
using ExecutionEngine.Persistence;
using FluentAssertions;

[TestClass]
public class InMemoryCheckpointStorageTests
{
    [TestMethod]
    public async Task SaveCheckpointAsync_NewCheckpoint_ShouldSaveSuccessfully()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var checkpoint = CreateTestCheckpoint();

        // Act
        await storage.SaveCheckpointAsync(checkpoint);

        // Assert
        var loaded = await storage.LoadCheckpointAsync(checkpoint.WorkflowInstanceId);
        loaded.Should().NotBeNull();
        loaded!.WorkflowInstanceId.Should().Be(checkpoint.WorkflowInstanceId);
        loaded.WorkflowId.Should().Be(checkpoint.WorkflowId);
        loaded.Status.Should().Be(checkpoint.Status);
    }

    [TestMethod]
    public async Task SaveCheckpointAsync_UpdateExisting_ShouldOverwrite()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var checkpoint = CreateTestCheckpoint();
        await storage.SaveCheckpointAsync(checkpoint);

        // Act - Update the checkpoint
        checkpoint.Status = WorkflowExecutionStatus.Paused;
        await storage.SaveCheckpointAsync(checkpoint);

        // Assert
        var loaded = await storage.LoadCheckpointAsync(checkpoint.WorkflowInstanceId);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(WorkflowExecutionStatus.Paused);
    }

    [TestMethod]
    public async Task LoadCheckpointAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var nonExistentId = Guid.NewGuid();

        // Act
        var loaded = await storage.LoadCheckpointAsync(nonExistentId);

        // Assert
        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task DeleteCheckpointAsync_ExistingCheckpoint_ShouldReturnTrue()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var checkpoint = CreateTestCheckpoint();
        await storage.SaveCheckpointAsync(checkpoint);

        // Act
        var deleted = await storage.DeleteCheckpointAsync(checkpoint.WorkflowInstanceId);

        // Assert
        deleted.Should().BeTrue();
        var loaded = await storage.LoadCheckpointAsync(checkpoint.WorkflowInstanceId);
        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task DeleteCheckpointAsync_NonExistent_ShouldReturnFalse()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var nonExistentId = Guid.NewGuid();

        // Act
        var deleted = await storage.DeleteCheckpointAsync(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    [TestMethod]
    public async Task ListCheckpointsAsync_EmptyStorage_ShouldReturnEmptyList()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();

        // Act
        var checkpoints = await storage.ListCheckpointsAsync();

        // Assert
        checkpoints.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListCheckpointsAsync_MultipleCheckpoints_ShouldReturnAll()
    {
        // Arrange
        var storage = new InMemoryCheckpointStorage();
        var checkpoint1 = CreateTestCheckpoint();
        var checkpoint2 = CreateTestCheckpoint();
        var checkpoint3 = CreateTestCheckpoint();

        await storage.SaveCheckpointAsync(checkpoint1);
        await storage.SaveCheckpointAsync(checkpoint2);
        await storage.SaveCheckpointAsync(checkpoint3);

        // Act
        var checkpoints = await storage.ListCheckpointsAsync();

        // Assert
        checkpoints.Should().HaveCount(3);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint1.WorkflowInstanceId);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint2.WorkflowInstanceId);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint3.WorkflowInstanceId);
    }

    private static WorkflowCheckpoint CreateTestCheckpoint()
    {
        return new WorkflowCheckpoint
        {
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowId = "test-workflow",
            WorkflowDefinitionJson = "{}",
            Status = WorkflowExecutionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            CheckpointTime = DateTime.UtcNow,
            Variables = new Dictionary<string, object>
            {
                { "testVar", "testValue" }
            },
            NodeStates = new Dictionary<string, NodeInstanceState>
            {
                {
                    "node1", new NodeInstanceState
                    {
                        NodeId = "node1",
                        Status = NodeExecutionStatus.Completed,
                        StartTime = DateTime.UtcNow.AddMinutes(-4),
                        EndTime = DateTime.UtcNow.AddMinutes(-3)
                    }
                }
            }
        };
    }
}

[TestClass]
public class FileCheckpointStorageTests
{
    private string testDirectory = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        // Create a temporary directory for test checkpoints
        testDirectory = Path.Combine(Path.GetTempPath(), $"checkpoint-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Delete the test directory
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveCheckpointAsync_NewCheckpoint_ShouldCreateFile()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var checkpoint = CreateTestCheckpoint();

        // Act
        await storage.SaveCheckpointAsync(checkpoint);

        // Assert
        var filePath = Path.Combine(testDirectory, $"{checkpoint.WorkflowInstanceId}.json");
        File.Exists(filePath).Should().BeTrue();
    }

    [TestMethod]
    public async Task SaveCheckpointAsync_UpdateExisting_ShouldOverwriteFile()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var checkpoint = CreateTestCheckpoint();
        await storage.SaveCheckpointAsync(checkpoint);

        // Act - Update the checkpoint
        checkpoint.Status = WorkflowExecutionStatus.Paused;
        await storage.SaveCheckpointAsync(checkpoint);

        // Assert
        var loaded = await storage.LoadCheckpointAsync(checkpoint.WorkflowInstanceId);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(WorkflowExecutionStatus.Paused);
    }

    [TestMethod]
    public async Task LoadCheckpointAsync_ExistingCheckpoint_ShouldDeserializeCorrectly()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var checkpoint = CreateTestCheckpoint();
        await storage.SaveCheckpointAsync(checkpoint);

        // Act
        var loaded = await storage.LoadCheckpointAsync(checkpoint.WorkflowInstanceId);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.WorkflowInstanceId.Should().Be(checkpoint.WorkflowInstanceId);
        loaded.WorkflowId.Should().Be(checkpoint.WorkflowId);
        loaded.Status.Should().Be(checkpoint.Status);
        loaded.Variables.Should().ContainKey("testVar");
        loaded.NodeStates.Should().ContainKey("node1");
    }

    [TestMethod]
    public async Task LoadCheckpointAsync_NonExistent_ShouldReturnNull()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var nonExistentId = Guid.NewGuid();

        // Act
        var loaded = await storage.LoadCheckpointAsync(nonExistentId);

        // Assert
        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task DeleteCheckpointAsync_ExistingCheckpoint_ShouldDeleteFile()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var checkpoint = CreateTestCheckpoint();
        await storage.SaveCheckpointAsync(checkpoint);

        // Act
        var deleted = await storage.DeleteCheckpointAsync(checkpoint.WorkflowInstanceId);

        // Assert
        deleted.Should().BeTrue();
        var filePath = Path.Combine(testDirectory, $"{checkpoint.WorkflowInstanceId}.json");
        File.Exists(filePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task ListCheckpointsAsync_MultipleCheckpoints_ShouldReturnAll()
    {
        // Arrange
        var storage = new FileCheckpointStorage(testDirectory);
        var checkpoint1 = CreateTestCheckpoint();
        var checkpoint2 = CreateTestCheckpoint();
        var checkpoint3 = CreateTestCheckpoint();

        await storage.SaveCheckpointAsync(checkpoint1);
        await storage.SaveCheckpointAsync(checkpoint2);
        await storage.SaveCheckpointAsync(checkpoint3);

        // Act
        var checkpoints = await storage.ListCheckpointsAsync();

        // Assert
        checkpoints.Should().HaveCount(3);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint1.WorkflowInstanceId);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint2.WorkflowInstanceId);
        checkpoints.Should().Contain(c => c.WorkflowInstanceId == checkpoint3.WorkflowInstanceId);
    }

    private static WorkflowCheckpoint CreateTestCheckpoint()
    {
        return new WorkflowCheckpoint
        {
            WorkflowInstanceId = Guid.NewGuid(),
            WorkflowId = "test-workflow",
            WorkflowDefinitionJson = "{}",
            Status = WorkflowExecutionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            CheckpointTime = DateTime.UtcNow,
            Variables = new Dictionary<string, object>
            {
                { "testVar", "testValue" }
            },
            NodeStates = new Dictionary<string, NodeInstanceState>
            {
                {
                    "node1", new NodeInstanceState
                    {
                        NodeId = "node1",
                        Status = NodeExecutionStatus.Completed,
                        StartTime = DateTime.UtcNow.AddMinutes(-4),
                        EndTime = DateTime.UtcNow.AddMinutes(-3)
                    }
                }
            }
        };
    }
}
