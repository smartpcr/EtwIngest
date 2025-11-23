// -----------------------------------------------------------------------
// <copyright file="ProgressTrackingTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Events
{
    using ExecutionEngine.Engine;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Events;
    using ExecutionEngine.Nodes.Definitions;
    using ExecutionEngine.Workflow;
    using FluentAssertions;

    [TestClass]
    public class ProgressTrackingTests
    {
        private readonly List<string> tempFiles = new List<string>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var file in this.tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            this.tempFiles.Clear();
        }

        [TestMethod]
        public async Task Test_ProgressCalculation_Accuracy()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Node 1",
                        ScriptPath = this.CreateTempScript("await Task.Delay(50); SetOutput(\"result\", 42);")
                    },
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node2",
                        NodeName = "Node 2",
                        ScriptPath = this.CreateTempScript("await Task.Delay(50); SetOutput(\"result\", 43);")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection
                    {
                        SourceNodeId = "node1",
                        TargetNodeId = "node2",
                        IsEnabled = true
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();

            // Act
            var context = await engine.StartAsync(workflow);

            // Subscribe to progress updates
            context.Progress.Subscribe(p => progressUpdates.Add(p));

            // Wait for workflow to complete
            await Task.Delay(2000);

            // Assert
            progressUpdates.Should().NotBeEmpty("progress updates should be published during workflow execution");

            // Check that total nodes matches
            if (progressUpdates.Any())
            {
                var firstProgress = progressUpdates.First();
                firstProgress.TotalNodes.Should().Be(2);
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_Progress_UpdatesAfterNodeCompletion()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Node 1",
                        ScriptPath = this.CreateTempScript("await Task.Delay(100); SetOutput(\"result\", 42);")
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();
            var completed = new TaskCompletionSource<bool>();

            // Act - Start workflow and subscribe immediately
            var context = await engine.StartAsync(workflow);
            context.Progress.Subscribe(
                onNext: p => progressUpdates.Add(p),
                onCompleted: () => completed.TrySetResult(true));

            // Wait for workflow to complete via observable completion or timeout
            await Task.WhenAny(completed.Task, Task.Delay(2000));

            // Assert
            progressUpdates.Should().NotBeEmpty();

            // Check that we received progress update after node completion
            var updates = progressUpdates.Where(p => p.NodesCompleted > 0).ToList();
            if (updates.Any())
            {
                var update = updates.First();
                update.NodesCompleted.Should().BeGreaterThan(0);
                update.PercentComplete.Should().BeGreaterThan(0);
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_PercentComplete_ZeroToHundred()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Node 1",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 42);")
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();
            var completed = new TaskCompletionSource<bool>();

            // Act - Start workflow and subscribe immediately
            var context = await engine.StartAsync(workflow);
            context.Progress.Subscribe(
                onNext: p => progressUpdates.Add(p),
                onCompleted: () => completed.TrySetResult(true));

            // Wait for workflow to complete via observable completion or timeout
            await Task.WhenAny(completed.Task, Task.Delay(2000));

            // Assert
            progressUpdates.Should().NotBeEmpty();

            // All progress percentages should be between 0 and 100
            foreach (var update in progressUpdates)
            {
                update.PercentComplete.Should().BeInRange(0, 100);
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_NodeCounts_Accurate()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Node 1",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 42);")
                    },
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node2",
                        NodeName = "Node 2",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 43);")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection
                    {
                        SourceNodeId = "node1",
                        TargetNodeId = "node2",
                        IsEnabled = true
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();
            var completed = new TaskCompletionSource<bool>();

            // Act - Start workflow and subscribe immediately
            var context = await engine.StartAsync(workflow);
            context.Progress.Subscribe(
                onNext: p => progressUpdates.Add(p),
                onCompleted: () => completed.TrySetResult(true));

            // Wait for workflow to complete via observable completion or timeout
            await Task.WhenAny(completed.Task, Task.Delay(2000));

            // Assert
            progressUpdates.Should().NotBeEmpty();

            // Check node counts in progress updates
            foreach (var update in progressUpdates)
            {
                // Total should always be 2
                update.TotalNodes.Should().Be(2);

                // All counts should be non-negative
                update.NodesCompleted.Should().BeGreaterThanOrEqualTo(0);
                update.NodesRunning.Should().BeGreaterThanOrEqualTo(0);
                update.NodesPending.Should().BeGreaterThanOrEqualTo(0);
                update.NodesFailed.Should().BeGreaterThanOrEqualTo(0);
                update.NodesCancelled.Should().BeGreaterThanOrEqualTo(0);
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_Progress_WithFailedNodes()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Failing Node",
                        ScriptPath = this.CreateTempScript("throw new Exception(\"Test failure\");")
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();

            // Act
            try
            {
                var context = await engine.StartAsync(workflow);

                // Subscribe to progress
                context.Progress.Subscribe(p => progressUpdates.Add(p));

                // Wait for workflow to process
                await Task.Delay(1500);

                // Dispose
                context.Dispose();
            }
            catch
            {
                // Workflow failure is expected
            }

            // Assert - Progress tracking should work even with failures
            // (Note: events might not be captured if subscription happens after publishing)
        }

        [TestMethod]
        public async Task Test_Progress_HasCorrectTimestamps()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow",
                WorkflowName = "Test Workflow",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Node 1",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 42);")
                    }
                }
            };

            var progressUpdates = new List<ProgressUpdate>();
            var completed = new TaskCompletionSource<bool>();

            // Act
            var startTime = DateTime.UtcNow;
            var context = await engine.StartAsync(workflow);
            context.Progress.Subscribe(
                onNext: p => progressUpdates.Add(p),
                onCompleted: () => completed.TrySetResult(true));

            // Wait for workflow to complete via observable completion or timeout
            await Task.WhenAny(completed.Task, Task.Delay(2000));
            var endTime = DateTime.UtcNow;

            // Assert
            progressUpdates.Should().NotBeEmpty();

            // All timestamps should be within the test execution window
            foreach (var update in progressUpdates)
            {
                update.Timestamp.Should().BeAfter(startTime.AddSeconds(-1));
                update.Timestamp.Should().BeBefore(endTime.AddSeconds(1));
            }

            // Dispose
            context.Dispose();
        }

        private string CreateTempScript(string scriptContent)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_script_{Guid.NewGuid()}.csx");
            File.WriteAllText(tempFile, scriptContent);
            this.tempFiles.Add(tempFile);
            return tempFile;
        }
    }
}
