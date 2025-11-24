// -----------------------------------------------------------------------
// <copyright file="WorkflowValidatorTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Workflow;

using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using ExecutionEngine.Workflow;
using FluentAssertions;

[TestClass]
public class WorkflowValidatorTests
{
    private WorkflowValidator validator = null!;

    [TestInitialize]
    public void Setup()
    {
        this.validator = new WorkflowValidator(AssemblyInitialize.ServiceProvider);
    }

    [TestMethod]
    public void Validate_WithNullWorkflow_ShouldReturnError()
    {
        // Act
        var result = this.validator.Validate(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("cannot be null");
    }

    [TestMethod]
    public void Validate_WithEmptyWorkflowId_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = string.Empty,
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("WorkflowId"));
    }

    [TestMethod]
    public void Validate_WithEmptyWorkflowName_ShouldReturnWarning()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            WorkflowName = string.Empty,
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("WorkflowName"));
    }

    [TestMethod]
    public void Validate_WithNoNodes_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>()
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least one node"));
    }

    [TestMethod]
    public void Validate_WithDuplicateNodeIds_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1", },
                new PowerShellScriptNodeDefinition { NodeId = "node-1", }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate node ID") && e.Contains("node-1"));
    }

    [TestMethod]
    public void Validate_WithEmptyNodeId_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = string.Empty, }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("null or empty NodeId"));
    }

    [TestMethod]
    public void Validate_WithInvalidEntryPoint_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            EntryPointNodeId = "non-existent",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1", }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Entry point") && e.Contains("non-existent"));
    }

    [TestMethod]
    public void Validate_WithConnectionToNonExistentSourceNode_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",}
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "non-existent", TargetNodeId = "node-1" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("non-existent source node"));
    }

    [TestMethod]
    public void Validate_WithConnectionToNonExistentTargetNode_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "non-existent" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("non-existent target node"));
    }

    [TestMethod]
    public void Validate_WithEmptySourceNodeId_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = string.Empty, TargetNodeId = "node-1" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("null or empty SourceNodeId"));
    }

    [TestMethod]
    public void Validate_WithEmptyTargetNodeId_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "node-1", TargetNodeId = string.Empty }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("null or empty TargetNodeId"));
    }

    [TestMethod]
    public void Validate_WithSelfReferencingConnection_ShouldReturnWarning()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "node-1", TargetNodeId = "node-1" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("Self-referencing"));
    }

    [TestMethod]
    public void Validate_WithCycle_ShouldReturnError()
    {
        // Arrange - Create a cycle: A -> B -> C -> A
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  },
                new CSharpScriptNodeDefinition { NodeId = "C",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "B" },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "C" },
                new NodeConnection { SourceNodeId = "C", TargetNodeId = "A" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cycle detected"));
    }

    [TestMethod]
    public void Validate_WithComplexCycle_ShouldReturnError()
    {
        // Arrange - Create a complex graph with a cycle: A -> B -> C -> D -> B
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  },
                new CSharpScriptNodeDefinition { NodeId = "C",  },
                new CSharpScriptNodeDefinition { NodeId = "D",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "B" },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "C" },
                new NodeConnection { SourceNodeId = "C", TargetNodeId = "D" },
                new NodeConnection { SourceNodeId = "D", TargetNodeId = "B" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Cycle detected"));
    }

    [TestMethod]
    public void Validate_WithNoCycle_ShouldNotReturnCycleError()
    {
        // Arrange - Linear workflow: A -> B -> C
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  },
                new CSharpScriptNodeDefinition { NodeId = "C",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "B" },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "C" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Errors.Should().NotContain(e => e.Contains("Cycle"));
    }

    [TestMethod]
    public void Validate_WithNoEntryPoints_ShouldReturnError()
    {
        // Arrange - All nodes have incoming connections (cycle)
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "B" },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "A" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Errors.Should().Contain(e => e.Contains("no entry points"));
    }

    [TestMethod]
    public void Validate_WithMultipleEntryPoints_ShouldReturnWarning()
    {
        // Arrange - Two nodes without incoming connections
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  },
                new CSharpScriptNodeDefinition { NodeId = "C",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "C" },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "C" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Warnings.Should().Contain(w => w.Contains("entry points"));
    }

    [TestMethod]
    public void Validate_WithExplicitEntryPoint_ShouldNotWarnAboutMultipleEntryPoints()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            EntryPointNodeId = "A",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Warnings.Should().NotContain(w => w.Contains("entry points"));
    }

    [TestMethod]
    public void Validate_WithNegativeMaxConcurrency_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            MaxConcurrency = -1,
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxConcurrency"));
    }

    [TestMethod]
    public void Validate_WithNegativeTimeoutSeconds_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            TimeoutSeconds = -1,
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "node-1",  }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TimeoutSeconds"));
    }

    [TestMethod]
    public void Validate_WithDisabledConnection_ShouldIgnoreForCycleDetection()
    {
        // Arrange - Cycle exists but one connection is disabled
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition { NodeId = "A",  },
                new CSharpScriptNodeDefinition { NodeId = "B",  },
                new CSharpScriptNodeDefinition { NodeId = "C",  }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "A", TargetNodeId = "B", IsEnabled = true },
                new NodeConnection { SourceNodeId = "B", TargetNodeId = "C", IsEnabled = true },
                new NodeConnection { SourceNodeId = "C", TargetNodeId = "A", IsEnabled = false }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.Errors.Should().NotContain(e => e.Contains("Cycle"));
    }

    [TestMethod]
    public void Validate_ValidWorkflow_ShouldReturnNoErrors()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-valid",
            WorkflowName = "Valid Workflow",
            EntryPointNodeId = "start",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "start",
                    ScriptPath = "TestData/Scripts/start.csx"
                },
                new PowerShellScriptNodeDefinition
                {
                    NodeId = "process",
                    ScriptPath = "process.ps1"
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "end",
                    ScriptPath = "TestData/Scripts/end.csx"
                }
            },
            Connections = new List<NodeConnection>
            {
                new NodeConnection { SourceNodeId = "start", TargetNodeId = "process" },
                new NodeConnection { SourceNodeId = "process", TargetNodeId = "end" }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void ValidationResult_IsValid_ShouldBeFalseWhenErrorsExist()
    {
        // Arrange
        var result = new ValidationResult();
        result.Errors.Add("Test error");

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [TestMethod]
    public void ValidationResult_IsValid_ShouldBeTrueWhenNoErrors()
    {
        // Arrange
        var result = new ValidationResult();
        result.Warnings.Add("Test warning");

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_WithNullConnections_ShouldNotFail()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "node-1",
                    ScriptPath = @"TestData\Scripts\script.csx"
                }
            },
            Connections = null!
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert - Should not crash and should be valid (single node with no connections is a valid workflow)
        result.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_CSharpTaskNode_WithMissingScriptConfig_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-node-1",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Either ScriptContent or ExecutorAssemblyPath/ExecutorTypeName must be provided."));
    }

    [TestMethod]
    public void Validate_CSharpTaskNode_WithValidScriptConfig_ShouldPass()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "task-node-1",
                    ScriptContent = "return \"Hello World\";",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [TestMethod]
    public void Validate_CSharpScriptNode_WithMissingScriptPath_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpScriptNodeDefinition
                {
                    NodeId = "script-node-1",
                    // Missing ScriptPath property
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Script file  or script content not provided"));
    }

    [TestMethod]
    public void Validate_PowerShellTaskNode_WithValidScriptConfig_ShouldPass()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new PowerShellTaskNodeDefinition
                {
                    NodeId = "ps-task-1",
                    ScriptContent = "Write-Output 'Hello World'",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_IfElseNode_WithMissingCondition_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new IfElseNodeDefinition
                {
                    NodeId = "if-node-1",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Condition is required"));
    }

    [TestMethod]
    public void Validate_ForEachNode_WithMissingCollectionExpression_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new ForEachNodeDefinition
                {
                    NodeId = "foreach-node-1",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("CollectionExpression must be provided when ScriptContent is not available"));
    }

    [TestMethod]
    public void Validate_NodeWithNegativeConcurrency_ShouldReturnError()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "node-1",
                    MaxConcurrentExecutions = -1, // Invalid
                    ScriptContent = "return 1;",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxConcurrentExecutions cannot be negative."));
    }

    [TestMethod]
    public void Validate_MultipleNodeConfigurationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var workflow = new WorkflowDefinition
        {
            WorkflowId = "wf-001",
            Nodes = new List<NodeDefinition>
            {
                new CSharpTaskNodeDefinition
                {
                    NodeId = "node-1",
                },
                new CSharpScriptNodeDefinition
                {
                    NodeId = "node-2",
                },
                new IfElseNodeDefinition
                {
                    NodeId = "node-3",
                }
            }
        };

        // Act
        var result = this.validator.Validate(workflow);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(3);
        result.Errors.Should().Contain(e => e.Contains("Either ScriptContent or ExecutorAssemblyPath/ExecutorTypeName must be provided"));
        result.Errors.Should().Contain(e => e.Contains("Script file  or script content not provided."));
        result.Errors.Should().Contain(e => e.Contains("Condition is required."));
    }
}
