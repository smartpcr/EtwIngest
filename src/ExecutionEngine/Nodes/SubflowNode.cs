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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Node that executes another workflow as a child/nested workflow.
/// Enables workflow composition, reusability, and modular workflow design.
/// Provides context isolation with explicit input/output variable mapping.
/// </summary>
public class SubflowNode : ExecutableNodeBase
{
    private ILogger logger = NullLogger.Instance;

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

        // Read WorkflowFilePath from direct property or Configuration dictionary
        if (!string.IsNullOrWhiteSpace(subflowDef.WorkflowFilePath))
        {
            this.WorkflowFilePath = subflowDef.WorkflowFilePath;
        }
        else if (subflowDef.Configuration?.TryGetValue("WorkflowFilePath", out var pathValue) == true && pathValue is string path)
        {
            this.WorkflowFilePath = path;
        }

        // Read InputMappings from direct property or Configuration dictionary
        if (subflowDef.InputMappings != null && subflowDef.InputMappings.Count > 0)
        {
            this.InputMappings = subflowDef.InputMappings;
        }
        else if (subflowDef.Configuration?.TryGetValue("InputMappings", out var inputMappingsValue) == true && inputMappingsValue is Dictionary<string, string> inputMappings)
        {
            this.InputMappings = inputMappings;
        }

        // Read OutputMappings from direct property or Configuration dictionary
        if (subflowDef.OutputMappings != null && subflowDef.OutputMappings.Count > 0)
        {
            this.OutputMappings = subflowDef.OutputMappings;
        }
        else if (subflowDef.Configuration?.TryGetValue("OutputMappings", out var outputMappingsValue) == true && outputMappingsValue is Dictionary<string, string> outputMappings)
        {
            this.OutputMappings = outputMappings;
        }

        // Read Timeout from direct property or Configuration dictionary
        if (subflowDef.Timeout != TimeSpan.FromMinutes(5)) // Check if not default
        {
            this.Timeout = subflowDef.Timeout;
        }
        else if (subflowDef.Configuration?.TryGetValue("Timeout", out var timeoutValue) == true)
        {
            if (timeoutValue is TimeSpan timeout)
            {
                this.Timeout = timeout;
            }
            else if (timeoutValue is string timeoutStr && TimeSpan.TryParse(timeoutStr, out var parsedTimeout))
            {
                this.Timeout = parsedTimeout;
            }
        }

        // Read SkipValidation from direct property or Configuration dictionary
        this.SkipValidation = subflowDef.SkipValidation;
        if (subflowDef.Configuration?.TryGetValue("SkipValidation", out var skipValidationValue) == true && skipValidationValue is bool skipValidation)
        {
            this.SkipValidation = skipValidation;
        }

        // Load child workflow definition if not already provided
        if (subflowDef.WorkflowDefinition != null)
        {
            this.ChildWorkflowDefinition = subflowDef.WorkflowDefinition;
        }
        else if (!string.IsNullOrWhiteSpace(this.WorkflowFilePath))
        {
            try
            {
                var loader = new WorkflowLoader();
                this.ChildWorkflowDefinition = loader.Load(this.WorkflowFilePath);
            }
            catch (FileNotFoundException ex)
            {
                // Wrap with SubflowNode-specific error message
                throw new FileNotFoundException(
                    $"Child workflow file not found: {this.WorkflowFilePath}",
                    this.WorkflowFilePath,
                    ex);
            }
        }

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
            // Normalize path separators for cross-platform compatibility
            // On Linux, backslashes are not recognized as path separators
            var normalizedPath = this.WorkflowFilePath.Replace('\\', '/');
            var resolvedPath = Path.GetFullPath(normalizedPath);

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
        // Create logger from workflow context if available
        if (workflowContext.LoggerFactory != null)
        {
            this.logger = workflowContext.LoggerFactory.CreateLogger<SubflowNode>();
        }

        this.logger.LogInformation("SubflowNode.ExecuteAsync starting for NodeId: {NodeId}, WorkflowInstanceId: {WorkflowInstanceId}",
            this.NodeId, workflowContext.InstanceId);

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
            this.logger.LogInformation("SubflowNode loading child workflow for NodeId: {NodeId}", this.NodeId);
            var childWorkflowDef = await this.LoadChildWorkflowAsync();
            this.logger.LogInformation("SubflowNode loaded child workflow: {ChildWorkflowId} for NodeId: {NodeId}",
                childWorkflowDef?.WorkflowId, this.NodeId);

            if (childWorkflowDef == null)
            {
                throw new InvalidOperationException(
                    "Child workflow definition not provided. Set either ChildWorkflowDefinition or WorkflowFilePath.");
            }

            // Prepare initial variables for child workflow from input mappings
            var initialVariables = new Dictionary<string, object>();
            this.logger.LogDebug("SubflowNode preparing input mappings: {MappingCount} mappings for NodeId: {NodeId}",
                this.InputMappings.Count, this.NodeId);
            foreach (var parentKey in this.InputMappings.Keys)
            {
                var childKey = this.InputMappings[parentKey];

                // Map parent variable to child variable
                if (workflowContext.Variables.TryGetValue(parentKey, out var value))
                {
                    initialVariables[childKey] = value;
                    this.logger.LogDebug("SubflowNode mapped variable: {ParentKey} -> {ChildKey} for NodeId: {NodeId}",
                        parentKey, childKey, this.NodeId);
                }
            }

            // Execute child workflow with initial variables
            // Use service provider from parent workflow context to enable logging in child workflows
            this.logger.LogInformation("SubflowNode creating child WorkflowEngine for NodeId: {NodeId}, ChildWorkflow: {ChildWorkflowId}",
                this.NodeId, childWorkflowDef.WorkflowId);
            var engine = new WorkflowEngine(null, workflowContext.ServiceProvider);

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

            this.logger.LogInformation("SubflowNode starting child workflow engine for NodeId: {NodeId}, ChildWorkflow: {ChildWorkflowId}, Timeout: {Timeout}",
                this.NodeId, childWorkflowDef.WorkflowId, this.Timeout);

            this.ChildWorkflowContext = await engine.StartAsync(
                childWorkflowDef,
                initialVariables,
                this.Timeout,
                cancellationToken);

            this.logger.LogInformation("SubflowNode child workflow completed for NodeId: {NodeId}, ChildWorkflow: {ChildWorkflowId}, Status: {Status}",
                this.NodeId, childWorkflowDef.WorkflowId, this.ChildWorkflowContext.Status);

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
            this.logger.LogDebug("SubflowNode mapping output variables: {MappingCount} mappings for NodeId: {NodeId}",
                this.OutputMappings.Count, this.NodeId);
            this.MapOutputVariables(this.ChildWorkflowContext, workflowContext);

            // Store child workflow output in node context for downstream access
            nodeContext.OutputData["ChildWorkflowId"] = childWorkflowDef.WorkflowId;
            nodeContext.OutputData["ChildWorkflowInstanceId"] = this.ChildWorkflowContext.InstanceId;
            nodeContext.OutputData["ChildWorkflowStatus"] = this.ChildWorkflowContext.Status.ToString();
            nodeContext.OutputData["ChildOutputData"] = new Dictionary<string, object>(this.ChildWorkflowContext.Variables);

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.logger.LogInformation("SubflowNode.ExecuteAsync completed successfully for NodeId: {NodeId}, Duration: {Duration}ms",
                this.NodeId, instance.Duration?.TotalMilliseconds);
        }
        catch (OperationCanceledException ex)
        {
            this.logger.LogWarning("SubflowNode.ExecuteAsync cancelled for NodeId: {NodeId}, Message: {Message}",
                this.NodeId, ex.Message);
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Subflow execution was cancelled";
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "SubflowNode.ExecuteAsync failed for NodeId: {NodeId}, Message: {Message}",
                this.NodeId, ex.Message);
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
            // Normalize path separators for cross-platform compatibility
            // On Linux, backslashes are not recognized as path separators
            var normalizedPath = this.WorkflowFilePath.Replace('\\', '/');
            var resolvedPath = Path.GetFullPath(normalizedPath);

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
    /// Maps output variables from child context to parent context.
    /// </summary>
    /// <param name="childContext">Child workflow context.</param>
    /// <param name="parentContext">Parent workflow context.</param>
    private void MapOutputVariables(WorkflowExecutionContext childContext, WorkflowExecutionContext parentContext)
    {
        foreach (var outKey in this.OutputMappings.Keys)
        {
            var globalVariableName = this.OutputMappings[outKey];

            // Map child variable to parent variable
            if (childContext.Variables.TryGetValue(outKey, out var value))
            {
                parentContext.Variables[globalVariableName] = value;
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
