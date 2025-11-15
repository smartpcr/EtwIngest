// -----------------------------------------------------------------------
// <copyright file="FilePersistenceProviderTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Persistence;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class FilePersistenceProviderTests
{
    private string testDirectory = string.Empty;
    private FilePersistenceProvider? provider;

    [TestInitialize]
    public void Setup()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"checkpoint-tests-{Guid.NewGuid()}");
        this.provider = new FilePersistenceProvider(this.testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(this.testDirectory))
        {
            Directory.Delete(this.testDirectory, true);
        }
    }

    #region State Serialization Tests

    [TestMethod]
    public async Task SerializeWorkflowExecutionContext_WithVariablesAndQueues_CreatesJsonFile()
    {
        // Arrange
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "test-workflow",
            Status = WorkflowExecutionStatus.Running
        };
        context.Variables["x"] = 10;
        context.Variables["name"] = "test";

        var checkpointId = "checkpoint-1";
        var workflowInstanceId = context.InstanceId;
        var nodeInstances = new List<NodeInstance>();

        // Act
        var metadata = await this.provider!.SaveCheckpointAsync(
            checkpointId,
            workflowInstanceId,
            context,
            nodeInstances);

        // Assert
        var filePath = Path.Combine(this.testDirectory, $"{checkpointId}.checkpoint.json");
        File.Exists(filePath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(filePath);
        json.Should().Contain("test-workflow");
        json.Should().Contain("\"x\"");
        json.Should().Contain("10");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"test\"");
    }

    [TestMethod]
    public async Task DeserializeWorkflowExecutionContext_FromFile_RestoresAllVariables()
    {
        // Arrange
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "test-workflow",
            Status = WorkflowExecutionStatus.Running
        };
        context.Variables["x"] = 10;
        context.Variables["name"] = "test";
        context.Variables["flag"] = true;

        var checkpointId = "checkpoint-2";
        await this.provider!.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            new List<NodeInstance>());

        // Act
        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);

        // Assert
        restoredCheckpoint.Context.WorkflowId.Should().Be("test-workflow");
        restoredCheckpoint.Context.Variables.Should().ContainKey("x");
        restoredCheckpoint.Context.Variables.Should().ContainKey("name");
        restoredCheckpoint.Context.Variables.Should().ContainKey("flag");
    }

    [TestMethod]
    public async Task SerializeNodeInstanceStates_WithMultipleStates_CapturesAllCorrectly()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "test" };
        var nodeInstances = new List<NodeInstance>
        {
            new NodeInstance
            {
                NodeInstanceId = Guid.NewGuid(),
                NodeId = "node1",
                Status = NodeExecutionStatus.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(-4)
            },
            new NodeInstance
            {
                NodeInstanceId = Guid.NewGuid(),
                NodeId = "node2",
                Status = NodeExecutionStatus.Running,
                StartTime = DateTime.UtcNow.AddMinutes(-3)
            },
            new NodeInstance
            {
                NodeInstanceId = Guid.NewGuid(),
                NodeId = "node3",
                Status = NodeExecutionStatus.Pending
            }
        };

        var checkpointId = "checkpoint-3";

        // Act
        await this.provider!.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            nodeInstances);

        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);

        // Assert
        restoredCheckpoint.NodeInstances.Should().HaveCount(3);
        restoredCheckpoint.NodeInstances[0].Status.Should().Be(NodeExecutionStatus.Completed);
        restoredCheckpoint.NodeInstances[1].Status.Should().Be(NodeExecutionStatus.Running);
        restoredCheckpoint.NodeInstances[2].Status.Should().Be(NodeExecutionStatus.Pending);
    }

    [TestMethod]
    public async Task HandleLargeContext_With1000Variables_SerializesSuccessfully()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "large-test" };
        for (var i = 0; i < 1000; i++)
        {
            context.Variables[$"var{i}"] = $"value{i}";
        }

        var checkpointId = "checkpoint-large";

        // Act
        var metadata = await this.provider!.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            new List<NodeInstance>());

        // Assert
        metadata.Should().NotBeNull();
        metadata.SizeBytes.Should().BeGreaterThan(0);
        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);
        restoredCheckpoint.Context.Variables.Should().HaveCount(1000);
    }

    #endregion

    #region Checkpoint Operations Tests

    [TestMethod]
    public async Task CreateCheckpoint_DuringExecution_CreatesStateFile()
    {
        // Arrange
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "test",
            Status = WorkflowExecutionStatus.Running
        };
        var nodeInstances = Enumerable.Range(0, 5).Select(i => new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = $"node{i}",
            Status = i < 3 ? NodeExecutionStatus.Completed : NodeExecutionStatus.Pending
        }).ToList();

        var checkpointId = "checkpoint-exec";

        // Act
        var metadata = await this.provider!.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            nodeInstances);

        // Assert
        var filePath = Path.Combine(this.testDirectory, $"{checkpointId}.checkpoint.json");
        File.Exists(filePath).Should().BeTrue();
        metadata.CompletedNodes.Should().Be(3);
        metadata.PendingNodes.Should().Be(2);
    }

    [TestMethod]
    public async Task CheckpointMetadata_IncludesTimestamp_AndWorkflowId()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "metadata-test" };
        var checkpointId = "checkpoint-meta";
        var beforeSave = DateTime.UtcNow;

        // Act
        var metadata = await this.provider!.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            new List<NodeInstance>());

        var afterSave = DateTime.UtcNow;

        // Assert
        metadata.CheckpointId.Should().Be(checkpointId);
        metadata.WorkflowId.Should().Be("metadata-test");
        metadata.WorkflowInstanceId.Should().Be(context.InstanceId);
        metadata.Timestamp.Should().BeOnOrAfter(beforeSave).And.BeOnOrBefore(afterSave);
    }

    [TestMethod]
    public async Task MultipleCheckpoints_CreatesAllAccessible()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "multi-test" };
        var workflowInstanceId = context.InstanceId;

        // Act
        await this.provider!.SaveCheckpointAsync("checkpoint-1", workflowInstanceId, context, new List<NodeInstance>());
        await Task.Delay(10); // Ensure different timestamps
        await this.provider.SaveCheckpointAsync("checkpoint-2", workflowInstanceId, context, new List<NodeInstance>());
        await Task.Delay(10);
        await this.provider.SaveCheckpointAsync("checkpoint-3", workflowInstanceId, context, new List<NodeInstance>());

        // Assert
        var checkpoints = await this.provider.ListCheckpointsAsync(workflowInstanceId);
        checkpoints.Should().HaveCount(3);
        var checkpointList = checkpoints.ToList();
        checkpointList[0].CheckpointId.Should().Be("checkpoint-3"); // Most recent first
        checkpointList[1].CheckpointId.Should().Be("checkpoint-2");
        checkpointList[2].CheckpointId.Should().Be("checkpoint-1");
    }

    [TestMethod]
    public async Task Checkpoint_WithValidation_ThrowsOnInvalidInput()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "validation-test" };

        // Act & Assert - null checkpoint ID
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
        {
            await this.provider!.SaveCheckpointAsync(string.Empty, context.InstanceId, context, new List<NodeInstance>());
        });

        // Act & Assert - null context
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
        {
            await this.provider!.SaveCheckpointAsync("test", context.InstanceId, null!, new List<NodeInstance>());
        });

        // Act & Assert - null node instances
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
        {
            await this.provider!.SaveCheckpointAsync("test", context.InstanceId, context, null!);
        });
    }

    #endregion

    #region State Restoration Tests

    [TestMethod]
    public async Task RestoreFromCheckpoint_ResumesFromCorrectPosition()
    {
        // Arrange
        var context = new WorkflowExecutionContext
        {
            WorkflowId = "restore-test",
            Status = WorkflowExecutionStatus.Running
        };
        context.Variables["progress"] = 50;

        var nodeInstances = new List<NodeInstance>
        {
            new NodeInstance { NodeId = "node1", Status = NodeExecutionStatus.Completed },
            new NodeInstance { NodeId = "node2", Status = NodeExecutionStatus.Completed },
            new NodeInstance { NodeId = "node3", Status = NodeExecutionStatus.Running }
        };

        var checkpointId = "checkpoint-restore";
        await this.provider!.SaveCheckpointAsync(checkpointId, context.InstanceId, context, nodeInstances);

        // Act
        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);

        // Assert
        restoredCheckpoint.Context.Variables["progress"].Should().Be(50);
        restoredCheckpoint.Metadata.CompletedNodes.Should().Be(2);
        restoredCheckpoint.NodeInstances.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task RestoreVariables_PreservesValues()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "var-restore" };
        context.Variables["x"] = 10;
        context.Variables["name"] = "test";
        context.Variables["flag"] = true;
        context.Variables["data"] = new List<string> { "a", "b", "c" };

        var checkpointId = "checkpoint-vars";
        await this.provider!.SaveCheckpointAsync(checkpointId, context.InstanceId, context, new List<NodeInstance>());

        // Act
        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);

        // Assert
        restoredCheckpoint.Context.Variables["x"].Should().Be(10);
        restoredCheckpoint.Context.Variables["name"].Should().Be("test");
        restoredCheckpoint.Context.Variables["flag"].Should().Be(true);
    }

    [TestMethod]
    public async Task RestoreNodeStates_MatchesCheckpoint()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "node-state-restore" };
        var nodeInstances = new List<NodeInstance>
        {
            new NodeInstance
            {
                NodeId = "completed1",
                Status = NodeExecutionStatus.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EndTime = DateTime.UtcNow.AddMinutes(-5)
            },
            new NodeInstance
            {
                NodeId = "completed2",
                Status = NodeExecutionStatus.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(-2)
            },
            new NodeInstance { NodeId = "pending1", Status = NodeExecutionStatus.Pending },
            new NodeInstance { NodeId = "pending2", Status = NodeExecutionStatus.Pending }
        };

        var checkpointId = "checkpoint-states";
        await this.provider!.SaveCheckpointAsync(checkpointId, context.InstanceId, context, nodeInstances);

        // Act
        var restoredCheckpoint = await this.provider.LoadCheckpointAsync(checkpointId);

        // Assert
        var completed = restoredCheckpoint.NodeInstances.Where(n => n.Status == NodeExecutionStatus.Completed).ToList();
        var pending = restoredCheckpoint.NodeInstances.Where(n => n.Status == NodeExecutionStatus.Pending).ToList();

        completed.Should().HaveCount(2);
        pending.Should().HaveCount(2);
        completed[0].NodeId.Should().Be("completed1");
        completed[1].NodeId.Should().Be("completed2");
    }

    [TestMethod]
    public async Task HandleCorruptCheckpointFile_ThrowsException()
    {
        // Arrange
        var checkpointId = "corrupt-checkpoint";
        var filePath = Path.Combine(this.testDirectory, $"{checkpointId}.checkpoint.json");
        await File.WriteAllTextAsync(filePath, "{ this is not valid JSON }");

        // Act & Assert
        await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(async () =>
        {
            await this.provider!.LoadCheckpointAsync(checkpointId);
        });
    }

    #endregion

    #region IStatePersistence Interface Tests

    [TestMethod]
    public async Task FileBasedPersistence_SaveAndLoad_WorksCorrectly()
    {
        // Arrange
        IStatePersistence persistence = new FilePersistenceProvider(this.testDirectory);
        var context = new WorkflowExecutionContext { WorkflowId = "interface-test" };
        context.Variables["test"] = "value";

        var checkpointId = "interface-checkpoint";

        // Act
        var metadata = await persistence.SaveCheckpointAsync(
            checkpointId,
            context.InstanceId,
            context,
            new List<NodeInstance>());

        var restoredCheckpoint = await persistence.LoadCheckpointAsync(checkpointId);

        // Assert
        metadata.Should().NotBeNull();
        restoredCheckpoint.Should().NotBeNull();
        restoredCheckpoint.Context.Variables["test"].Should().Be("value");
    }

    [TestMethod]
    public async Task PersistencePathConfiguration_SavesToCustomLocation()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), $"custom-checkpoints-{Guid.NewGuid()}");
        var customProvider = new FilePersistenceProvider(customDir);
        var context = new WorkflowExecutionContext { WorkflowId = "custom-path-test" };
        var checkpointId = "custom-checkpoint";

        try
        {
            // Act
            await customProvider.SaveCheckpointAsync(
                checkpointId,
                context.InstanceId,
                context,
                new List<NodeInstance>());

            // Assert
            var expectedPath = Path.Combine(customDir, $"{checkpointId}.checkpoint.json");
            File.Exists(expectedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(customDir))
            {
                Directory.Delete(customDir, true);
            }
        }
    }

    [TestMethod]
    public async Task DeleteCheckpoint_RemovesFile()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "delete-test" };
        var checkpointId = "to-delete";
        await this.provider!.SaveCheckpointAsync(checkpointId, context.InstanceId, context, new List<NodeInstance>());

        var filePath = Path.Combine(this.testDirectory, $"{checkpointId}.checkpoint.json");
        File.Exists(filePath).Should().BeTrue();

        // Act
        await this.provider.DeleteCheckpointAsync(checkpointId);

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [TestMethod]
    public async Task DeleteAllCheckpoints_RemovesAllForWorkflow()
    {
        // Arrange
        var context1 = new WorkflowExecutionContext { WorkflowId = "workflow1" };
        var instanceId1 = context1.InstanceId;
        var context2 = new WorkflowExecutionContext { WorkflowId = "workflow2" };
        var instanceId2 = context2.InstanceId;

        // Create 3 checkpoints for workflow1 and 2 for workflow2
        await this.provider!.SaveCheckpointAsync("w1-c1", instanceId1, context1, new List<NodeInstance>());
        await this.provider.SaveCheckpointAsync("w1-c2", instanceId1, context1, new List<NodeInstance>());
        await this.provider.SaveCheckpointAsync("w1-c3", instanceId1, context1, new List<NodeInstance>());
        await this.provider.SaveCheckpointAsync("w2-c1", instanceId2, context2, new List<NodeInstance>());
        await this.provider.SaveCheckpointAsync("w2-c2", instanceId2, context2, new List<NodeInstance>());

        // Act
        await this.provider.DeleteAllCheckpointsAsync(instanceId1);

        // Assert
        var checkpoints1 = await this.provider.ListCheckpointsAsync(instanceId1);
        var checkpoints2 = await this.provider.ListCheckpointsAsync(instanceId2);

        checkpoints1.Should().BeEmpty();
        checkpoints2.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task ListCheckpoints_FiltersAndOrdersCorrectly()
    {
        // Arrange
        var context = new WorkflowExecutionContext { WorkflowId = "list-test" };
        var instanceId = context.InstanceId;

        // Create checkpoints with delays to ensure different timestamps
        await this.provider!.SaveCheckpointAsync("checkpoint-old", instanceId, context, new List<NodeInstance>());
        await Task.Delay(50);
        await this.provider.SaveCheckpointAsync("checkpoint-mid", instanceId, context, new List<NodeInstance>());
        await Task.Delay(50);
        await this.provider.SaveCheckpointAsync("checkpoint-new", instanceId, context, new List<NodeInstance>());

        // Act
        var checkpoints = await this.provider.ListCheckpointsAsync(instanceId);
        var checkpointList = checkpoints.ToList();

        // Assert
        checkpointList.Should().HaveCount(3);
        checkpointList[0].CheckpointId.Should().Be("checkpoint-new"); // Most recent first
        checkpointList[1].CheckpointId.Should().Be("checkpoint-mid");
        checkpointList[2].CheckpointId.Should().Be("checkpoint-old");
    }

    [TestMethod]
    public async Task LoadNonExistentCheckpoint_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
        {
            await this.provider!.LoadCheckpointAsync("does-not-exist");
        });
    }

    [TestMethod]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var newDir = Path.Combine(Path.GetTempPath(), $"new-dir-{Guid.NewGuid()}");
        Directory.Exists(newDir).Should().BeFalse();

        try
        {
            // Act
            var newProvider = new FilePersistenceProvider(newDir);

            // Assert
            Directory.Exists(newDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(newDir))
            {
                Directory.Delete(newDir, true);
            }
        }
    }

    #endregion
}
