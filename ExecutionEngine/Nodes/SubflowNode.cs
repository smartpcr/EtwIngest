// -----------------------------------------------------------------------
// <copyright file="SubflowNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Text.Json;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Workflow;

/// <summary>
/// Node that executes another workflow as a child/nested workflow.
/// Enables workflow composition, reusability, and modular workflow design.
/// Provides context isolation with explicit input/output variable mapping.
/// </summary>
public class SubflowNode : ExecutableNodeBase
{
    /// <summary>
    /// Gets or sets the workflow definition to execute as a subflow.
    /// Used when the child workflow is provided directly as a definition.
    /// </summary>
    public WorkflowDefinition? ChildWorkflowDefinition { get; set; }

    /// <summary>
    /// Gets or sets the file path to load the child workflow definition from.
    /// Supports both absolute and relative paths.
    /// </summary>
    public string? WorkflowFilePath { get; set; }

    /// <summary>
    /// Gets or sets the input variable mappings from parent to child.
    /// Key: Parent variable name
    /// Value: Child variable name
    /// Example: { "parentItemId" -> "itemId", "parentUserId" -> "userId" }
    /// </summary>
    public Dictionary<string, string> InputMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the output variable mappings from child to parent.
    /// Key: Child variable name
    /// Value: Parent variable name
    /// Example: { "result" -> "childResult", "status" -> "childStatus" }
    /// </summary>
    public Dictionary<string, string> OutputMappings { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the timeout for child workflow execution.
    /// If not set, the child workflow executes without timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the child workflow execution context after execution.
    /// Allows access to child workflow state and node instances for debugging.
    /// </summary>
    public WorkflowExecutionContext? ChildWorkflowContext { get; private set; }

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        if (definition.Configuration != null)
        {
            // Get workflow file path
            if (definition.Configuration.TryGetValue("WorkflowFilePath", out var filePath))
            {
                this.WorkflowFilePath = filePath?.ToString();
            }

            // Get input mappings
            if (definition.Configuration.TryGetValue("InputMappings", out var inputMappings))
            {
                if (inputMappings is Dictionary<string, string> inputDict)
                {
                    this.InputMappings = inputDict;
                }
                else if (inputMappings is Dictionary<string, object> inputObjDict)
                {
                    this.InputMappings = inputObjDict.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty);
                }
            }

            // Get output mappings
            if (definition.Configuration.TryGetValue("OutputMappings", out var outputMappings))
            {
                if (outputMappings is Dictionary<string, string> outputDict)
                {
                    this.OutputMappings = outputDict;
                }
                else if (outputMappings is Dictionary<string, object> outputObjDict)
                {
                    this.OutputMappings = outputObjDict.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty);
                }
            }

            // Get timeout
            if (definition.Configuration.TryGetValue("Timeout", out var timeout))
            {
                if (timeout is TimeSpan timeoutValue)
                {
                    this.Timeout = timeoutValue;
                }
                else if (timeout is int timeoutMs)
                {
                    this.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                }
                else if (double.TryParse(timeout?.ToString(), out var timeoutMsDouble))
                {
                    this.Timeout = TimeSpan.FromMilliseconds(timeoutMsDouble);
                }
            }

            // Get child workflow definition if provided directly
            if (definition.Configuration.TryGetValue("ChildWorkflowDefinition", out var childWorkflow))
            {
                if (childWorkflow is WorkflowDefinition workflowDef)
                {
                    this.ChildWorkflowDefinition = workflowDef;
                }
            }
        }
    }

    /// <inheritdoc/>
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        var instance = new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),
            NodeId = this.NodeId,
            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Running,
            StartTime = DateTime.UtcNow,
            ExecutionContext = nodeContext
        };

        try
        {
            this.RaiseOnStart(new NodeStartEventArgs
            {
                NodeId = this.NodeId,
                NodeInstanceId = instance.NodeInstanceId,
                Timestamp = DateTime.UtcNow
            });

            // Load child workflow definition
            var childWorkflowDef = await this.LoadChildWorkflowAsync();

            if (childWorkflowDef == null)
            {
                throw new InvalidOperationException(
                    "Child workflow definition not provided. Set either ChildWorkflowDefinition or WorkflowFilePath.");
            }

            // Prepare initial variables for child workflow from input mappings
            var initialVariables = new Dictionary<string, object>();
            foreach (var mapping in this.InputMappings)
            {
                var parentVarName = mapping.Key;
                var childVarName = mapping.Value;

                if (workflowContext.Variables.TryGetValue(parentVarName, out var value))
                {
                    initialVariables[childVarName] = value;
                }
            }

            // Execute child workflow with initial variables
            var engine = new WorkflowEngine();
            this.ChildWorkflowContext = await engine.StartAsync(
                childWorkflowDef,
                initialVariables,
                this.Timeout,
                cancellationToken);

            // Check child workflow execution status
            if (this.ChildWorkflowContext.Status == WorkflowExecutionStatus.Failed)
            {
                var childErrors = this.GetChildWorkflowErrors(this.ChildWorkflowContext, engine);
                throw new InvalidOperationException(
                    $"Child workflow '{childWorkflowDef.WorkflowId}' failed with errors:{Environment.NewLine}{childErrors}");
            }

            if (this.ChildWorkflowContext.Status == WorkflowExecutionStatus.Cancelled)
            {
                throw new OperationCanceledException("Child workflow was cancelled");
            }

            // Map output variables from child to parent
            this.MapOutputVariables(this.ChildWorkflowContext, workflowContext);

            // Store child workflow output in node context for downstream access
            nodeContext.OutputData["ChildWorkflowId"] = childWorkflowDef.WorkflowId;
            nodeContext.OutputData["ChildWorkflowInstanceId"] = this.ChildWorkflowContext.InstanceId;
            nodeContext.OutputData["ChildWorkflowStatus"] = this.ChildWorkflowContext.Status.ToString();
            nodeContext.OutputData["ChildOutputData"] = new Dictionary<string, object>(this.ChildWorkflowContext.Variables);

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Subflow execution was cancelled";
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
        }

        return instance;
    }

    /// <summary>
    /// Loads the child workflow definition from file or uses the provided definition.
    /// </summary>
    /// <returns>The child workflow definition.</returns>
    private async Task<WorkflowDefinition?> LoadChildWorkflowAsync()
    {
        // If definition provided directly, use it
        if (this.ChildWorkflowDefinition != null)
        {
            return this.ChildWorkflowDefinition;
        }

        // Load from file path
        if (!string.IsNullOrWhiteSpace(this.WorkflowFilePath))
        {
            if (!File.Exists(this.WorkflowFilePath))
            {
                throw new FileNotFoundException($"Child workflow file not found: {this.WorkflowFilePath}");
            }

            var json = await File.ReadAllTextAsync(this.WorkflowFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<WorkflowDefinition>(json, options);
        }

        return null;
    }

    /// <summary>
    /// Maps input variables from parent context to child context.
    /// </summary>
    /// <param name="parentContext">Parent workflow context.</param>
    /// <param name="childContext">Child workflow context.</param>
    private void MapInputVariables(WorkflowExecutionContext parentContext, WorkflowExecutionContext childContext)
    {
        foreach (var mapping in this.InputMappings)
        {
            var parentVarName = mapping.Key;
            var childVarName = mapping.Value;

            if (parentContext.Variables.TryGetValue(parentVarName, out var value))
            {
                childContext.Variables[childVarName] = value;
            }
        }
    }

    /// <summary>
    /// Maps output variables from child context to parent context.
    /// </summary>
    /// <param name="childContext">Child workflow context.</param>
    /// <param name="parentContext">Parent workflow context.</param>
    private void MapOutputVariables(WorkflowExecutionContext childContext, WorkflowExecutionContext parentContext)
    {
        foreach (var mapping in this.OutputMappings)
        {
            var childVarName = mapping.Key;
            var parentVarName = mapping.Value;

            if (childContext.Variables.TryGetValue(childVarName, out var value))
            {
                parentContext.Variables[parentVarName] = value;
            }
        }
    }

    /// <summary>
    /// Gets child workflow error details for error reporting.
    /// </summary>
    /// <param name="childContext">Child workflow context.</param>
    /// <param name="engine">Workflow engine that executed the child.</param>
    /// <returns>Formatted error message.</returns>
    private string GetChildWorkflowErrors(WorkflowExecutionContext childContext, WorkflowEngine engine)
    {
        var errors = new List<string>();

        var childInstances = engine.GetNodeInstances(childContext.InstanceId);
        var failedInstances = childInstances.Where(i => i.Status == NodeExecutionStatus.Failed);

        foreach (var failedInstance in failedInstances)
        {
            errors.Add($"  Node '{failedInstance.NodeId}': {failedInstance.ErrorMessage}");
        }

        return errors.Any() ? string.Join(Environment.NewLine, errors) : "Unknown error";
    }
}
