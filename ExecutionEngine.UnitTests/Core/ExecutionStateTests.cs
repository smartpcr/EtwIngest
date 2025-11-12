// -----------------------------------------------------------------------
// <copyright file="ExecutionStateTests.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.UnitTests.Core;

using System.Collections.Concurrent;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using FluentAssertions;

[TestClass]
public class ExecutionStateTests
{
    [TestMethod]
    public void SetOutput_AddsToOutputDictionary()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        var state = new ExecutionState
        {
            NodeContext = nodeContext,
            Output = nodeContext.OutputData,
            SetOutput = (key, value) => nodeContext.OutputData[key] = value
        };

        // Act
        state.SetOutput("result", 42);

        // Assert
        nodeContext.OutputData["result"].Should().Be(42);
        state.Output["result"].Should().Be(42);
    }

    [TestMethod]
    public void GetInput_RetrievesFromInputDictionary()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        nodeContext.InputData["count"] = 10;

        var state = new ExecutionState
        {
            NodeContext = nodeContext,
            Input = nodeContext.InputData,
            GetInput = (key) => nodeContext.InputData.TryGetValue(key, out var val) ? val : null
        };

        // Act
        var value = state.GetInput("count");

        // Assert
        value.Should().Be(10);
    }

    [TestMethod]
    public void GetInput_ReturnsNullForMissingKey()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        var state = new ExecutionState
        {
            NodeContext = nodeContext,
            Input = nodeContext.InputData,
            GetInput = (key) => nodeContext.InputData.TryGetValue(key, out var val) ? val : null
        };

        // Act
        var value = state.GetInput("nonexistent");

        // Assert
        value.Should().BeNull();
    }

    [TestMethod]
    public void GetGlobal_RetrievesFromWorkflowVariables()
    {
        // Arrange
        var workflowContext = new WorkflowExecutionContext();
        workflowContext.Variables["basePath"] = "/data";

        var state = new ExecutionState
        {
            WorkflowContext = workflowContext,
            GlobalVariables = workflowContext.Variables,
            GetGlobal = (key) => workflowContext.Variables.TryGetValue(key, out var val) ? val : null
        };

        // Act
        var value = state.GetGlobal("basePath");

        // Assert
        value.Should().Be("/data");
    }

    [TestMethod]
    public void SetGlobal_AddsToWorkflowVariables()
    {
        // Arrange
        var workflowContext = new WorkflowExecutionContext();
        var state = new ExecutionState
        {
            WorkflowContext = workflowContext,
            GlobalVariables = workflowContext.Variables,
            SetGlobal = (key, value) => workflowContext.Variables[key] = value
        };

        // Act
        state.SetGlobal("outputPath", "/output");

        // Assert
        workflowContext.Variables["outputPath"].Should().Be("/output");
        state.GlobalVariables["outputPath"].Should().Be("/output");
    }

    [TestMethod]
    public void Local_IsSharedWithNodeContext()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        var state = new ExecutionState
        {
            NodeContext = nodeContext,
            Local = nodeContext.LocalVariables
        };

        // Act
        state.Local["counter"] = 0;

        // Assert
        nodeContext.LocalVariables["counter"].Should().Be(0);
    }

    [TestMethod]
    public void AllProperties_CanBeAssigned()
    {
        // Arrange
        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        // Act
        var state = new ExecutionState
        {
            WorkflowContext = workflowContext,
            NodeContext = nodeContext,
            GlobalVariables = workflowContext.Variables,
            Input = nodeContext.InputData,
            Output = nodeContext.OutputData,
            Local = nodeContext.LocalVariables
        };

        // Assert
        state.WorkflowContext.Should().BeSameAs(workflowContext);
        state.NodeContext.Should().BeSameAs(nodeContext);
        state.GlobalVariables.Should().BeSameAs(workflowContext.Variables);
        state.Input.Should().BeSameAs(nodeContext.InputData);
        state.Output.Should().BeSameAs(nodeContext.OutputData);
        state.Local.Should().BeSameAs(nodeContext.LocalVariables);
    }

    [TestMethod]
    public void HelperFunctions_CanBeAssignedAndInvoked()
    {
        // Arrange
        var nodeContext = new NodeExecutionContext();
        var workflowContext = new WorkflowExecutionContext();

        var state = new ExecutionState
        {
            WorkflowContext = workflowContext,
            NodeContext = nodeContext,
            Input = nodeContext.InputData,
            Output = nodeContext.OutputData,
            GlobalVariables = workflowContext.Variables,
            SetOutput = (key, value) => nodeContext.OutputData[key] = value,
            GetInput = (key) => nodeContext.InputData.TryGetValue(key, out var val) ? val : null,
            GetGlobal = (key) => workflowContext.Variables.TryGetValue(key, out var val) ? val : null,
            SetGlobal = (key, value) => workflowContext.Variables[key] = value
        };

        // Act
        nodeContext.InputData["x"] = 10;
        workflowContext.Variables["multiplier"] = 2;

        var x = (int)state.GetInput("x")!;
        var multiplier = (int)state.GetGlobal("multiplier")!;
        state.SetOutput("result", x * multiplier);
        state.SetGlobal("lastResult", x * multiplier);

        // Assert
        state.Output["result"].Should().Be(20);
        workflowContext.Variables["lastResult"].Should().Be(20);
    }

    [TestMethod]
    public void ExecutionState_SupportsComplexWorkflow()
    {
        // Arrange
        var workflowContext = new WorkflowExecutionContext();
        var nodeContext = new NodeExecutionContext();

        var state = new ExecutionState
        {
            WorkflowContext = workflowContext,
            NodeContext = nodeContext,
            Input = nodeContext.InputData,
            Output = nodeContext.OutputData,
            Local = nodeContext.LocalVariables,
            GlobalVariables = workflowContext.Variables,
            SetOutput = (key, value) => nodeContext.OutputData[key] = value,
            GetInput = (key) => nodeContext.InputData.TryGetValue(key, out var val) ? val : null,
            GetGlobal = (key) => workflowContext.Variables.TryGetValue(key, out var val) ? val : null,
            SetGlobal = (key, value) => workflowContext.Variables[key] = value
        };

        // Simulate workflow: Node receives file list, processes, outputs count
        nodeContext.InputData["files"] = new[] { "file1.txt", "file2.txt", "file3.txt" };
        workflowContext.Variables["basePath"] = "/data";

        // Act
        var files = (string[])state.GetInput("files")!;
        var basePath = (string)state.GetGlobal("basePath")!;

        state.Local["processedCount"] = 0;
        foreach (var file in files)
        {
            state.Local["processedCount"] = (int)state.Local["processedCount"] + 1;
        }

        state.SetOutput("fileCount", files.Length);
        state.SetOutput("processedCount", state.Local["processedCount"]);
        state.SetGlobal("lastProcessedPath", basePath);

        // Assert
        state.Output["fileCount"].Should().Be(3);
        state.Output["processedCount"].Should().Be(3);
        workflowContext.Variables["lastProcessedPath"].Should().Be("/data");
        state.Local["processedCount"].Should().Be(3);
    }
}
