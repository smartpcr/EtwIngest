//-----------------------------------------------------------------------
// <copyright file="ProgressTreeManagerTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace ProgressTree.UnitTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for hierarchical ProgressTreeManager.
    /// </summary>
    [TestClass]
    public class ProgressTreeManagerTests
    {
        /// <summary>
        /// Tests that manager can be created successfully.
        /// </summary>
        [TestMethod]
        public void Constructor_CreatesInstance()
        {
            // Arrange & Act
            using var manager = new ProgressTreeManager();

            // Assert
            Assert.IsNotNull(manager);
            Assert.AreEqual(0, manager.TaskCount);
            Assert.AreEqual(0, manager.CompletedTaskCount);
            Assert.AreEqual(0, manager.OverallProgress);
        }

        /// <summary>
        /// Tests that RootTask is null before RunAsync.
        /// </summary>
        [TestMethod]
        public void RootTask_BeforeRunAsync_IsNull()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act & Assert
            Assert.IsNull(manager.RootTask);
        }

        /// <summary>
        /// Tests basic root task creation and properties.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_CreatesRootTask()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Test Root", ExecutionMode.Sequential, (root) =>
            {
                Assert.IsNotNull(root);
                Assert.AreEqual("__root__", root.Id);
                Assert.AreEqual(0, root.Depth);
                Assert.IsNull(root.Parent);
                Assert.AreEqual(TaskType.Job, root.TaskType);
                Assert.AreEqual(ExecutionMode.Sequential, root.ExecutionMode);
                return Task.CompletedTask;
            });

            // Assert
            Assert.IsNotNull(manager.RootTask);
        }

        /// <summary>
        /// Tests adding child tasks.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithChildren_CreatesHierarchy()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child1 = root.AddChild("child1", "Child 1");
                var child2 = root.AddChild("child2", "Child 2");

                // Assert
                Assert.AreEqual(2, root.Children.Count);
                Assert.AreEqual(root, child1.Parent);
                Assert.AreEqual(root, child2.Parent);
                Assert.AreEqual(1, child1.Depth);
                Assert.AreEqual(1, child2.Depth);

                return Task.CompletedTask;
            });

            // Root + 2 children = 3 total
            Assert.AreEqual(3, manager.TaskCount);
        }

        /// <summary>
        /// Tests nested hierarchy.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithNestedChildren_CreatesDeepHierarchy()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child = root.AddChild("child", "Child");
                var grandchild = child.AddChild("grandchild", "Grandchild");
                var greatgrandchild = grandchild.AddChild("greatgrandchild", "Great Grandchild");

                // Assert depths
                Assert.AreEqual(0, root.Depth);
                Assert.AreEqual(1, child.Depth);
                Assert.AreEqual(2, grandchild.Depth);
                Assert.AreEqual(3, greatgrandchild.Depth);

                // Assert parents
                Assert.IsNull(root.Parent);
                Assert.AreEqual(root, child.Parent);
                Assert.AreEqual(child, grandchild.Parent);
                Assert.AreEqual(grandchild, greatgrandchild.Parent);

                return Task.CompletedTask;
            });

            // Root + child + grandchild + great-grandchild = 4 total
            Assert.AreEqual(4, manager.TaskCount);
        }

        /// <summary>
        /// Tests task progress tracking.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_ProgressTracking_Works()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                var child = root.AddChild("child", "Child", workFunc: async (node, ct) =>
                {
                    Assert.AreEqual(0, node.Value);

                    node.ReportProgress(50);
                    Assert.AreEqual(50, node.Value);
                    Assert.IsFalse(node.IsCompleted);
                    await Task.CompletedTask;
                });

                // ExecuteAsync will automatically complete the child after work function finishes
                await child.ExecuteAsync();
                Assert.IsTrue(child.IsCompleted);
                Assert.AreEqual(100, child.Value);
            });

            Assert.AreEqual(2, manager.CompletedTaskCount); // Root + completed child
        }

        /// <summary>
        /// Tests parent progress calculation from children.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_ParentProgress_CalculatedFromChildren()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                var child1 = root.AddChild("child1", "Child 1", workFunc: async (node, ct) => await Task.CompletedTask);
                var child2 = root.AddChild("child2", "Child 2", workFunc: async (node, ct) => await Task.CompletedTask);

                // Initially both at 0
                Assert.AreEqual(0, root.Value);

                // Complete first child - root should be 50%
                await child1.ExecuteAsync();
                Assert.AreEqual(50, root.Value, 0.1);

                // Complete second child - root should be 100%
                await child2.ExecuteAsync();
                Assert.AreEqual(100, root.Value, 0.1);
            });

            Assert.AreEqual(3, manager.CompletedTaskCount);
        }

        /// <summary>
        /// Tests task value updates and capping at MaxValue.
        /// </summary>
        [TestMethod]
        public async Task Task_ValueUpdate_CapsAtMaxValue()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child = root.AddChild("child", "Child");
                Assert.AreEqual(0, child.Value);

                child.ReportProgress(25);
                Assert.AreEqual(25, child.Value);

                child.ReportProgress(55);
                Assert.AreEqual(55, child.Value);

                child.ReportProgress(150); // Should cap at 100
                Assert.AreEqual(100, child.Value);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests task failure functionality via ExecuteAsync exception handling.
        /// </summary>
        [TestMethod]
        public async Task Task_ExecuteAsync_WithException_TriggersOnFailEvent()
        {
            // Arrange
            using var manager = new ProgressTreeManager();
            var failEventFired = false;
            Exception? caughtException = null;

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                var child = root.AddChild("child", "Child", workFunc: async (node, ct) =>
                {
                    node.ReportProgress(50);
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Something went wrong");
                });

                // Hook up OnFail event
                child.OnFail += (node, error) =>
                {
                    failEventFired = true;
                    caughtException = error;
                };

                // Execute should catch exception and call OnFail
                try
                {
                    await child.ExecuteAsync();
                    Assert.Fail("Expected exception was not thrown");
                }
                catch (InvalidOperationException ex)
                {
                    // Expected - ExecuteAsync re-throws after calling Fail()
                    Assert.AreEqual("Something went wrong", ex.Message);
                }
            });

            // Assert
            Assert.IsTrue(failEventFired, "OnFail event should have been triggered");
            Assert.IsNotNull(caughtException);
            Assert.AreEqual("Something went wrong", caughtException.Message);
        }

        /// <summary>
        /// Tests TaskType property.
        /// </summary>
        [TestMethod]
        public async Task AddChild_WithTaskType_SetsCorrectly()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var job = root.AddChild("job", "Job Task", TaskType.Job);
                var stage = root.AddChild("stage", "Stage Task", TaskType.Stage);

                // Assert
                Assert.AreEqual(TaskType.Job, job.TaskType);
                Assert.AreEqual(TaskType.Stage, stage.TaskType);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests ExecutionMode property.
        /// </summary>
        [TestMethod]
        public async Task AddChild_WithExecutionMode_SetsCorrectly()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var sequential = root.AddChild("seq", "Sequential", TaskType.Job, ExecutionMode.Sequential);
                var parallel = root.AddChild("par", "Parallel", TaskType.Job, ExecutionMode.Parallel);

                // Assert
                Assert.AreEqual(ExecutionMode.Sequential, sequential.ExecutionMode);
                Assert.AreEqual(ExecutionMode.Parallel, parallel.ExecutionMode);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests parallel task execution.
        /// </summary>
        [TestMethod]
        public async Task RunAsync_WithParallelTasks_TracksCorrectly()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Parallel, async (root) =>
            {
                var task1 = root.AddChild("task1", "Task 1");
                var task2 = root.AddChild("task2", "Task 2");
                var task3 = root.AddChild("task3", "Task 3");

                // Simulate parallel work
                await Task.WhenAll(
                    SimulateWork(task1, 10),
                    SimulateWork(task2, 10),
                    SimulateWork(task3, 10));

                Assert.AreEqual(3, root.Children.Count);
            });

            Assert.AreEqual(4, manager.CompletedTaskCount); // Root + 3 children
        }

        /// <summary>
        /// Tests task with custom max value.
        /// </summary>
        [TestMethod]
        public async Task AddChild_WithCustomMaxValue_TracksCorrectly()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child = root.AddChild("child", "Child", maxValue: 50);
                Assert.AreEqual(50, child.MaxValue);

                child.ReportProgress(25);
                Assert.IsFalse(child.IsCompleted);

                child.ReportProgress(50);
                Assert.IsTrue(child.IsCompleted);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests description update.
        /// </summary>
        [TestMethod]
        public async Task Task_DescriptionUpdate_Works()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child = root.AddChild("child", "Initial Description");

                // Update description
                child.Description = "[yellow]Updated Description[/]";
                Assert.IsTrue(child.Description.Contains("Updated Description"));

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests Dispose clears tasks.
        /// </summary>
        [TestMethod]
        public async Task Dispose_ClearsTasks()
        {
            // Arrange
            var manager = new ProgressTreeManager();

            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                root.AddChild("child1", "Child 1");
                root.AddChild("child2", "Child 2");
                Assert.AreEqual(3, manager.TaskCount); // Root + 2 children
                return Task.CompletedTask;
            });

            // Act
            manager.Dispose();

            // Assert - After dispose, root should be null
            Assert.IsNull(manager.RootTask);
            Assert.AreEqual(0, manager.TaskCount);
        }

        /// <summary>
        /// Tests that in-progress tasks are properly tracked.
        /// </summary>
        [TestMethod]
        public async Task Task_InProgress_IsTracked()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                var child = root.AddChild("child", "Processing");

                // Start task and wait a bit
                child.ReportProgress(25);
                await Task.Delay(100);
                child.ReportProgress(50);

                // Task should be started but not completed
                Assert.IsTrue(child.IsStarted, "Task should be started");
                Assert.IsFalse(child.IsCompleted, "Task should not be completed yet");
                Assert.AreEqual(50, child.Value);

                return;
            });
        }

        /// <summary>
        /// Tests that completed tasks are properly marked.
        /// </summary>
        [TestMethod]
        public async Task Task_Completed_IsMarkedComplete()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                var child = root.AddChild("child", "Processing", workFunc: async (node, ct) =>
                {
                    node.ReportProgress(50);
                    await Task.Delay(100, ct);
                });

                // ExecuteAsync will automatically mark as complete after work function finishes
                await child.ExecuteAsync();

                // Task should be started and completed
                Assert.IsTrue(child.IsStarted, "Task should be started");
                Assert.IsTrue(child.IsCompleted, "Task should be completed");
                Assert.AreEqual(100, child.Value);
            });
        }

        /// <summary>
        /// Tests progressive indentation for grandchildren.
        /// </summary>
        [TestMethod]
        public async Task AddChild_Grandchildren_HaveMoreIndentation()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child = root.AddChild("child", "Child");
                var grandchild = child.AddChild("grandchild", "Grandchild");

                // Verify hierarchy
                Assert.AreEqual(0, root.Depth);
                Assert.AreEqual(1, child.Depth);
                Assert.AreEqual(2, grandchild.Depth);

                // Grandchild description should have more indentation than child
                // Child depth 1 gets 2 spaces, grandchild depth 2 gets 5 spaces
                Assert.IsTrue(grandchild.Description.Length > child.Description.Length ||
                             grandchild.Description.Contains("Grandchild"),
                             "Grandchild should have proper indentation");

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests deep hierarchy indentation (great-grandchildren).
        /// </summary>
        [TestMethod]
        public async Task AddChild_DeepHierarchy_HasProgressiveIndentation()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var level1 = root.AddChild("l1", "Level 1");
                var level2 = level1.AddChild("l2", "Level 2");
                var level3 = level2.AddChild("l3", "Level 3");

                // Verify depths
                Assert.AreEqual(1, level1.Depth);
                Assert.AreEqual(2, level2.Depth);
                Assert.AreEqual(3, level3.Depth);

                // Each level should exist
                Assert.IsNotNull(level1);
                Assert.IsNotNull(level2);
                Assert.IsNotNull(level3);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests that child tasks have default weight of 1.0.
        /// </summary>
        [TestMethod]
        public async Task AddChild_DefaultWeight_IsOne()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, (root) =>
            {
                var child1 = root.AddChild("child1", "Child 1");
                var child2 = root.AddChild("child2", "Child 2");

                // Assert
                Assert.AreEqual(1.0, child1.Weight);
                Assert.AreEqual(1.0, child2.Weight);

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Tests that weighted children calculate parent progress correctly.
        /// </summary>
        [TestMethod]
        public async Task AddChild_WithWeights_CalculatesParentProgressCorrectly()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                // Add children with different weights
                // Child1: weight 1 (10% of total weight)
                // Child2: weight 9 (90% of total weight)
                var child1 = root.AddChild("child1", "Child 1", weight: 1.0, workFunc: async (node, ct) => await Task.CompletedTask);
                var child2 = root.AddChild("child2", "Child 2", weight: 9.0, workFunc: async (node, ct) => await Task.CompletedTask);

                // Initially both at 0
                Assert.AreEqual(0, root.Value);

                // Complete first child (weight 1) - should contribute 10% to root
                await child1.ExecuteAsync();
                Assert.AreEqual(10, root.Value, 0.1);

                // Complete second child (weight 9) - root should be 100%
                await child2.ExecuteAsync();
                Assert.AreEqual(100, root.Value, 0.1);
            });
        }

        /// <summary>
        /// Tests weighted progress with partial completion.
        /// </summary>
        [TestMethod]
        public async Task AddChild_WithWeights_PartialCompletion()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                // Pre-deployment: 10%, Deployment: 80%, Post-deployment: 10%
                var preDeploy = root.AddChild("pre", "Pre-deployment", weight: 1.0, workFunc: async (node, ct) => await Task.CompletedTask);
                var deploy = root.AddChild("deploy", "Deployment", weight: 8.0, workFunc: async (node, ct) =>
                {
                    // Half-complete in work function
                    node.ReportProgress(50);
                    await Task.CompletedTask;
                });
                var postDeploy = root.AddChild("post", "Post-deployment", weight: 1.0, workFunc: async (node, ct) => await Task.CompletedTask);

                // Complete pre-deployment
                await preDeploy.ExecuteAsync();
                Assert.AreEqual(10, root.Value, 0.1);

                // Half-complete deployment (80% * 50% = 40% contribution)
                await deploy.ExecuteAsync();
                // After ExecuteAsync completes, it auto-sets to 100
                Assert.AreEqual(90, root.Value, 0.1); // 10% + 80% = 90%

                // Complete post-deployment
                await postDeploy.ExecuteAsync();
                Assert.AreEqual(100, root.Value, 0.1);
            });
        }

        /// <summary>
        /// Tests that weights work with equal-weighted children (same as default).
        /// </summary>
        [TestMethod]
        public async Task AddChild_EqualWeights_SameAsDefault()
        {
            // Arrange
            using var manager = new ProgressTreeManager();

            // Act
            await manager.RunAsync("Root", ExecutionMode.Sequential, async (root) =>
            {
                // Add three children with equal weights
                var child1 = root.AddChild("child1", "Child 1", weight: 2.0, workFunc: async (node, ct) => await Task.CompletedTask);
                var child2 = root.AddChild("child2", "Child 2", weight: 2.0, workFunc: async (node, ct) => await Task.CompletedTask);
                var child3 = root.AddChild("child3", "Child 3", weight: 2.0, workFunc: async (node, ct) => await Task.CompletedTask);

                // Complete first child - should be 33.33%
                await child1.ExecuteAsync();
                Assert.AreEqual(33.33, root.Value, 0.1);

                // Complete second child - should be 66.67%
                await child2.ExecuteAsync();
                Assert.AreEqual(66.67, root.Value, 0.1);

                // Complete third child - should be 100%
                await child3.ExecuteAsync();
                Assert.AreEqual(100, root.Value, 0.1);
            });
        }

        /// <summary>
        /// Simulates async work with progress updates.
        /// </summary>
        /// <param name="task">Task to update.</param>
        /// <param name="steps">Number of steps.</param>
        /// <returns>Task.</returns>
        private static async Task SimulateWork(IProgressNode task, int steps)
        {
            for (var i = 1; i <= steps; i++)
            {
                await Task.Delay(1);
                task.ReportProgress(i * 100.0 / steps);
            }
        }
    }
}
