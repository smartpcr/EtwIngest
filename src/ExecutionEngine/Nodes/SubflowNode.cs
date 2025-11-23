// -----------------------------------------------------------------------
// <copyright file="SubflowNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Engine;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
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

    /// <summary>
    /// Gets or sets whether to skip workflow validation during initialization.
    /// Used for unit testing. Default is false.
    /// </summary>
    public bool SkipValidation { get; set; }

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        if (definition is not SubflowNodeDefinition subflowDef)
        {
            throw new InvalidOperationException(
                $"SubflowNode '{this.NodeId}': Invalid node definition type: {definition.GetType().FullName}");
        }

        this.Definition = subflowDef;
        this.WorkflowFilePath = subflowDef.WorkflowFilePath;
        this.InputMappings = subflowDef.InputMappings;
        this.OutputMappings = subflowDef.OutputMappings;
        this.Timeout = subflowDef.Timeout;
        this.SkipValidation = subflowDef.SkipValidation;

        var loader = new WorkflowLoader();
        this.ChildWorkflowDefinition = loader.Load(this.WorkflowFilePath);

        // Validate that workflow can be loaded and is valid (unless skipped for testing)
        if (!this.SkipValidation)
        {
            this.ValidateWorkflowConfiguration();
        }
    }

    /// <summary>
    /// Validates that the subflow configuration is correct and the workflow can be loaded.
    /// </summary>
    private void ValidateWorkflowConfiguration()
    {
        // Validate that either ChildWorkflowDefinition or WorkflowFilePath is provided
        if (this.ChildWorkflowDefinition == null && string.IsNullOrWhiteSpace(this.WorkflowFilePath))
        {
            throw new InvalidOperationException(
                $"SubflowNode '{this.NodeId}': Either ChildWorkflowDefinition or WorkflowFilePath must be provided.");
        }

        // If workflow file path is provided, validate that it exists and can be loaded
        if (!string.IsNullOrWhiteSpace(this.WorkflowFilePath))
        {
            // Resolve relative paths by joining with current directory
            var resolvedPath = this.WorkflowFilePath;
            if (!Path.IsPathRooted(this.WorkflowFilePath))
            {
                resolvedPath = Path.Combine(Directory.GetCurrentDirectory(), this.WorkflowFilePath);
                Console.WriteLine($"[SubflowNode.ValidateWorkflowConfiguration] Resolved relative path '{this.WorkflowFilePath}' to '{resolvedPath}'");
            }

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException(
                    $"SubflowNode '{this.NodeId}': Child workflow file not found: {resolvedPath} (original: {this.WorkflowFilePath})");
            }

            // Try to load and deserialize the workflow to catch errors early
            try
            {
                var serializer = new WorkflowSerializer();
                var loadedWorkflow = serializer.LoadFromFile(resolvedPath);

                // Validate the loaded workflow
                var validator = new WorkflowValidator();
                var validationResult = validator.Validate(loadedWorkflow);

                if (!validationResult.IsValid)
                {
                    var errors = string.Join(Environment.NewLine, validationResult.Errors);
                    throw new InvalidOperationException(
                        $"SubflowNode '{this.NodeId}': Child workflow validation failed for '{resolvedPath}':{Environment.NewLine}{errors}");
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"SubflowNode '{this.NodeId}': Failed to load or deserialize child workflow from '{resolvedPath}' (original: {this.WorkflowFilePath}): {ex.Message}",
                    ex);
            }
        }
        else if (this.ChildWorkflowDefinition != null)
        {
            // Validate the provided workflow definition
            var validator = new WorkflowValidator();
            var validationResult = validator.Validate(this.ChildWorkflowDefinition);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(Environment.NewLine, validationResult.Errors);
                throw new InvalidOperationException(
                    $"SubflowNode '{this.NodeId}': Child workflow validation failed:{Environment.NewLine}{errors}");
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

            // Track child node completion for progress calculation (like ContainerNode)
            var totalChildNodes = childWorkflowDef.Nodes.Count;
            var completedChildNodes = 0;
            var childNodeLock = new object();

            // Subscribe to NodeCreated event to register OnProgress handlers (like ContainerNode)
            engine.NodeCreated += (nodeId, node) =>
            {
                if (node is ExecutableNodeBase executableNode)
                {
                    // Subscribe to child node's OnProgress events (like ContainerNode does)
                    executableNode.OnProgress += (sender, e) =>
                    {
                        // Forward child node progress with hierarchical key
                        // Don't wrap e.Status if it's already a lifecycle message (contains brackets)
                        // Only wrap raw progress messages from the child node itself
                        var status = e.Status;
                        if (!status.StartsWith("["))
                        {
                            // Raw progress message from child - wrap it with hierarchical key
                            status = $"[{this.NodeId}/{nodeId}] {status}";
                        }
                        // else: Already formatted by lifecycle events, forward as-is

                        this.RaiseOnProgress(new ProgressEventArgs
                        {
                            Status = status,
                            ProgressPercent = e.ProgressPercent
                        });
                    };
                }
            };

            // Subscribe to child workflow engine events to bubble up progress
            // Use hierarchical format "[parentNodeId/childNodeId]" to avoid key collisions
            engine.NodeStarted += (nodeId, instanceId) =>
            {
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"[{this.NodeId}/{nodeId}] Started",
                    ProgressPercent = 0
                });
            };

            engine.NodeCompleted += (nodeId, instanceId, duration) =>
            {
                // Report individual child completion with hierarchical key
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"[{this.NodeId}/{nodeId}] Completed in {duration.TotalSeconds:F1}s",
                    ProgressPercent = 100
                });

                // Update overall progress based on completed children (like ContainerNode)
                lock (childNodeLock)
                {
                    completedChildNodes++;
                    var overallProgress = totalChildNodes > 0 ? (completedChildNodes * 100 / totalChildNodes) : 0;

                    this.RaiseOnProgress(new ProgressEventArgs
                    {
                        Status = $"Subflow progress: {completedChildNodes}/{totalChildNodes} nodes completed",
                        ProgressPercent = overallProgress
                    });
                }
            };

            engine.NodeFailed += (nodeId, instanceId, error) =>
            {
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"[{this.NodeId}/{nodeId}] Failed: {error}",
                    ProgressPercent = 100
                });

                // Update overall progress on failure too
                lock (childNodeLock)
                {
                    completedChildNodes++;
                    var overallProgress = totalChildNodes > 0 ? (completedChildNodes * 100 / totalChildNodes) : 0;

                    this.RaiseOnProgress(new ProgressEventArgs
                    {
                        Status = $"Subflow progress: {completedChildNodes}/{totalChildNodes} nodes completed (with failures)",
                        ProgressPercent = overallProgress
                    });
                }
            };

            engine.NodeCancelled += (nodeId, instanceId, reason) =>
            {
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"[{this.NodeId}/{nodeId}] Cancelled: {reason}",
                    ProgressPercent = 100
                });
            };

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
            // Resolve path: if relative, join with current directory; if absolute, use as-is
            string resolvedPath;
            if (Path.IsPathRooted(this.WorkflowFilePath))
            {
                // Absolute path - use as-is
                resolvedPath = this.WorkflowFilePath;
            }
            else
            {
                // Relative path - join with current directory
                resolvedPath = Path.Combine(Directory.GetCurrentDirectory(), this.WorkflowFilePath);
                Console.WriteLine($"[SubflowNode.LoadChildWorkflowAsync] Resolved relative path '{this.WorkflowFilePath}' to '{resolvedPath}'");
            }

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Child workflow file not found: {resolvedPath} (original path: {this.WorkflowFilePath})");
            }

            // Use WorkflowSerializer to support both JSON and YAML formats
            var serializer = new WorkflowSerializer();
            return await Task.FromResult(serializer.LoadFromFile(resolvedPath));
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
