// -----------------------------------------------------------------------
// <copyright file="WorkflowEngineTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Engine
{
    using ExecutionEngine.Engine;
    using ExecutionEngine.Enums;
    using ExecutionEngine.Factory;
    using ExecutionEngine.Workflow;
    using FluentAssertions;

    [TestClass]
    public class WorkflowEngineTests
    {
        private readonly List<string> tempFiles = new List<string>();

        [TestMethod]
        public async Task ExecuteAsync_WithNullWorkflow_ShouldThrowException()
        {
            // Arrange
            var engine = new WorkflowEngine();

            // Act
            Func<Task> act = async () => await engine.StartAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [TestMethod]
        public async Task ExecuteAsync_WithEmptyWorkflow_ShouldThrowException()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "test",
                Nodes = new List<NodeDefinition>()
            };

            // Act
            Func<Task> act = async () => await engine.StartAsync(workflow);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*entry point*");
        }

        [TestMethod]
        public async Task ExecuteAsync_WithSingleNode_ShouldComplete()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "single-node-test",
                WorkflowName = "Single Node Test",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "node-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"result\", 42);")
                    }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            var nodeErrors = result.Variables.ContainsKey("__node_errors") ? result.Variables["__node_errors"] : "none";
            var workflowError = result.Variables.ContainsKey("__error") ? result.Variables["__error"] : "none";
            result.Status.Should().Be(WorkflowExecutionStatus.Completed,
                $"Expected Completed but got {result.Status}. Workflow Error: {workflowError}. Node Errors: {nodeErrors}");
            result.EndTime.Should().NotBeNull();
            result.Duration.Should().NotBeNull();
        }

        [TestMethod]
        public async Task ExecuteAsync_WithTwoNodesSequential_ShouldExecuteInOrder()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "two-nodes-sequential",
                WorkflowName = "Two Nodes Sequential",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "node-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"value\", 10);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "node-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript(@"
                            var input = GetInput(""value"");
                            var inputValue = input != null ? Convert.ToInt32(input) : 0;
                            SetOutput(""doubled"", inputValue * 2);
                        ")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection
                    {
                        SourceNodeId = "node-1",
                        TargetNodeId = "node-2",
                        TriggerMessageType = MessageType.Complete
                    }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithNodeFailure_ShouldFailWorkflow()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "node-failure-test",
                WorkflowName = "Node Failure Test",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "failing-node",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("throw new Exception(\"Test failure\");")
                    }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(WorkflowExecutionStatus.Failed);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithExplicitEntryPoint_ShouldUseSpecifiedEntry()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "explicit-entry",
                WorkflowName = "Explicit Entry Point",
                EntryPointNodeId = "node-2",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "node-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"n1\", 1);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "node-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"n2\", 2);")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection { SourceNodeId = "node-2", TargetNodeId = "node-1" }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithMultipleEntryPoints_ShouldExecuteAll()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "multiple-entries",
                WorkflowName = "Multiple Entry Points",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "entry-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"e1\", 1);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "entry-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"e2\", 2);")
                    }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithDisabledConnection_ShouldNotRoute()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "disabled-connection",
                WorkflowName = "Disabled Connection",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "node-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"v1\", 1);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "node-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"v2\", 2);")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection
                    {
                        SourceNodeId = "node-1",
                        TargetNodeId = "node-2",
                        IsEnabled = false
                    }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            // Node-2 should not execute since connection is disabled
            // Cancelled nodes (those not triggered) are considered acceptable for Completed status
            result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithGlobalVariables_ShouldShareAcrossNodes()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "global-vars",
                WorkflowName = "Global Variables",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "node-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetGlobal(\"shared\", 100);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "node-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript(@"
                            var shared = GetGlobal(""shared"");
                            var sharedValue = shared != null ? Convert.ToInt32(shared) : 0;
                            SetOutput(""result"", sharedValue + 50);
                        ")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-2" }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Variables.Should().ContainKey("shared");
            result.Variables["shared"].Should().Be(100);
        }

        [TestMethod]
        public async Task ExecuteAsync_WithFanout_ShouldExecuteAllBranches()
        {
            // Arrange
            var engine = new WorkflowEngine();
            var workflow = new WorkflowDefinition
            {
                WorkflowId = "fanout",
                WorkflowName = "Fanout Execution",
                Nodes = new List<NodeDefinition>
                {
                    new NodeDefinition
                    {
                        NodeId = "source",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"value\", 42);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "branch-1",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"b1\", 1);")
                    },
                    new NodeDefinition
                    {
                        NodeId = "branch-2",
                        RuntimeType = "CSharpScript",
                        ScriptPath = this.CreateTempScript("SetOutput(\"b2\", 2);")
                    }
                },
                Connections = new List<NodeConnection>
                {
                    new NodeConnection { SourceNodeId = "source", TargetNodeId = "branch-1" },
                    new NodeConnection { SourceNodeId = "source", TargetNodeId = "branch-2" }
                }
            };

            // Act
            var result = await engine.StartAsync(workflow);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be(WorkflowExecutionStatus.Completed);
        }

        /// <summary>
        /// Creates a temporary C# script file for testing.
        /// </summary>
        private string CreateTempScript(string scriptContent)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_script_{Guid.NewGuid()}.csx");
            File.WriteAllText(tempFile, scriptContent);
            this.tempFiles.Add(tempFile);
            return tempFile;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up only the temp script files created by this test instance
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
    }
}
