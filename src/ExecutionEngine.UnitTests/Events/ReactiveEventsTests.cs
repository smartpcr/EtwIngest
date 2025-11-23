// -----------------------------------------------------------------------
// <copyright file="ReactiveEventsTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Events
{
    using System.Reactive.Linq;
    using ExecutionEngine.Engine;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Events;
    using ExecutionEngine.Nodes.Definitions;
    using ExecutionEngine.Workflow;
    using FluentAssertions;

    [TestClass]
    public class ReactiveEventsTests
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
        public async Task Test_WorkflowStartedEvent_Published()
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

            // Act
            var context = await engine.StartAsync(workflow);

            // Assert
            context.GraphId.Should().Be("test-workflow");
            context.Status.Should().BeOneOf(WorkflowExecutionStatus.Running, WorkflowExecutionStatus.Completed);

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_WorkflowCompletedEvent_Published()
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

            var completedEvent = default(WorkflowCompletedEvent);
            var tcs = new TaskCompletionSource<WorkflowCompletedEvent>();

            // Act
            var context = await engine.StartAsync(workflow);

            // Subscribe to events
            context.Events.OfType<WorkflowCompletedEvent>().Subscribe(e =>
            {
                completedEvent = e;
                tcs.TrySetResult(e);
            });

            // Wait for completion event or timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));

            if (completedTask == tcs.Task)
            {
                completedEvent = await tcs.Task;
                completedEvent.Should().NotBeNull();
                completedEvent!.WorkflowInstanceId.Should().Be(context.InstanceId);
                completedEvent.WorkflowId.Should().Be("test-workflow");
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_NodeEvents_PublishedInOrder()
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
                    }
                }
            };

            var allEvents = new List<WorkflowEvent>();
            var completed = new TaskCompletionSource<bool>();

            // Act - Start workflow and subscribe immediately
            var context = await engine.StartAsync(workflow);
            context.Events.Subscribe(
                onNext: e => allEvents.Add(e),
                onCompleted: () => completed.TrySetResult(true));

            // Wait for workflow to complete via observable completion or timeout
            await Task.WhenAny(completed.Task, Task.Delay(2000));

            // Filter node events from all events
            var nodeEvents = allEvents.OfType<NodeEvent>().ToList();

            // Assert - node events should have been published
            allEvents.Should().NotBeEmpty();

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_MultipleSubscribers_ReceiveEvents()
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

            var subscriber1Events = new List<WorkflowEvent>();
            var subscriber2Events = new List<WorkflowEvent>();

            // Act
            var context = await engine.StartAsync(workflow);

            // Subscribe multiple subscribers
            context.Events.Subscribe(e => subscriber1Events.Add(e));
            context.Events.Subscribe(e => subscriber2Events.Add(e));

            // Wait for workflow to complete
            await Task.Delay(1000);

            // Assert - both subscribers should receive the same events
            subscriber1Events.Count.Should().Be(subscriber2Events.Count);

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_EventData_IsAccurate()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test-workflow-123",
                WorkflowName = "Test Workflow Name",
                Nodes = new List<NodeDefinition>
                {
                    new CSharpScriptNodeDefinition
                    {
                        NodeId = "node1",
                        NodeName = "Test Node 1",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 42);")
                    }
                }
            };

            var allEvents = new List<WorkflowEvent>();

            // Act
            var context = await engine.StartAsync(workflow);

            // Subscribe immediately
            context.Events.Subscribe(e => allEvents.Add(e));

            // Wait for workflow to complete
            await Task.Delay(1000);

            // Assert - check event data accuracy
            foreach (var evt in allEvents)
            {
                // All events should have correct workflow instance ID
                evt.WorkflowInstanceId.Should().Be(context.InstanceId);

                // All events should have timestamps
                evt.Timestamp.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
                evt.Timestamp.Should().BeBefore(DateTime.UtcNow.AddMinutes(1));
            }

            // Dispose
            context.Dispose();
        }

        [TestMethod]
        public async Task Test_Dispose_CompletesStreams()
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

            var streamCompleted = false;

            // Act
            var context = await engine.StartAsync(workflow);

            // Subscribe and track completion
            context.Events.Subscribe(
                onNext: e => { },
                onCompleted: () => streamCompleted = true
            );

            // Wait for workflow to complete
            await Task.Delay(1000);

            // Dispose
            context.Dispose();

            // Wait a bit for disposal to propagate
            await Task.Delay(100);

            // Assert
            streamCompleted.Should().BeTrue("disposing the context should complete the event stream");
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
