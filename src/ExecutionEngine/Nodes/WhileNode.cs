// -----------------------------------------------------------------------
// <copyright file="WhileNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Collections;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Events;
using ExecutionEngine.Factory;
using ExecutionEngine.Messages;
using ExecutionEngine.Queue;
using ExecutionEngine.Routing;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

/// <summary>
/// Node that iterates while a condition is true.
/// Uses Roslyn scripting engine to evaluate condition expressions.
/// For each iteration, sets iteration data in workflow context.
/// ARCHITECTURE: Uses feedback loop - child nodes send Complete back to WhileNode to trigger next iteration.
/// </summary>
public class WhileNode : ExecutableNodeBase
{
    /// <summary>
    /// Port name for loop body iteration messages.
    /// </summary>
    public const string LoopBodyPort = "LoopBody";

    /// <summary>
    /// Default maximum iterations to prevent infinite loops.
    /// </summary>
    public const int DefaultMaxIterations = 1000;

    /// <summary>
    /// Gets or sets the condition expression that must evaluate to true for iteration to continue.
    /// This should be a valid C# expression that evaluates to bool.
    /// IMPORTANT: Condition is re-evaluated before each iteration.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of iterations allowed.
    /// Prevents infinite loops. Default is 1000.
    /// </summary>
    public int MaxIterations { get; set; } = DefaultMaxIterations;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get condition expression from definition configuration
        if (definition.Configuration != null)
        {
            if (definition.Configuration.TryGetValue("Condition", out var conditionValue))
            {
                this.Condition = conditionValue?.ToString() ?? string.Empty;
            }

            if (definition.Configuration.TryGetValue("MaxIterations", out var maxIterValue))
            {
                if (maxIterValue is int maxIter)
                {
                    this.MaxIterations = maxIter;
                }
                else if (int.TryParse(maxIterValue?.ToString(), out var parsedMax))
                {
                    this.MaxIterations = parsedMax;
                }
            }
        }
    }

    /// <summary>
    /// Gets the available output ports for this node.
    /// </summary>
    /// <returns>Array of available port names.</returns>
    public string[] GetAvailablePorts()
    {
        // While node has LoopBody port for iteration messages
        return new[] { LoopBodyPort };
    }

    /// <inheritdoc/>
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        // Track iteration count using workflow variables
        var counterKey = $"__{this.NodeId}__iterationCount";
        var iterationCount = 0;
        if (workflowContext.Variables.TryGetValue(counterKey, out var counterValue))
        {
            iterationCount = (int)counterValue;
        }

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

            if (string.IsNullOrWhiteSpace(this.Condition))
            {
                throw new InvalidOperationException("Condition cannot be null or empty");
            }

            // Check max iterations safety limit
            if (iterationCount >= this.MaxIterations)
            {
                // Clean up and fail
                workflowContext.Variables.TryRemove(counterKey, out _);
                throw new InvalidOperationException(
                    $"Maximum iterations ({this.MaxIterations}) reached. Possible infinite loop.");
            }

            // Evaluate condition based on current workflow variables (updated by child nodes)
            var conditionResult = await this.EvaluateConditionAsync(workflowContext, nodeContext, cancellationToken);

            if (conditionResult)
            {
                // Condition is true: send ONE Next message to child nodes
                var router = workflowContext.Router;

                // Create iteration context for this iteration
                var iterationContext = new NodeExecutionContext
                {
                    InputData = new Dictionary<string, object>
                    {
                        { "iterationIndex", iterationCount }
                    },
                    OutputData = new Dictionary<string, object>()
                };

                // Emit OnNext event to trigger child node execution for this iteration
                this.RaiseOnNext(new NodeNextEventArgs
                {
                    NodeId = this.NodeId,
                    NodeInstanceId = instance.NodeInstanceId,
                    IterationIndex = iterationCount,
                    IterationContext = iterationContext,
                    Metadata = new Dictionary<string, object>
                    {
                        { "Condition", this.Condition },
                        { "IterationIndex", iterationCount }
                    }
                });

                // Route NodeNextMessage to downstream nodes connected via Next port
                if (router != null)
                {
                    var nextMessage = new NodeNextMessage
                    {
                        NodeId = this.NodeId,
                        NodeInstanceId = instance.NodeInstanceId,
                        Timestamp = DateTime.UtcNow,
                        IterationIndex = iterationCount,
                        IterationContext = iterationContext,
                        Metadata = new Dictionary<string, object>
                        {
                            { "Condition", this.Condition },
                            { "IterationIndex", iterationCount }
                        }
                    };

                    await router.RouteMessageAsync(nextMessage, workflowContext, cancellationToken);
                }

                // Emit progress event
                var percentComplete = this.MaxIterations > 0 ? (int)((iterationCount * 100.0) / this.MaxIterations) : 0;
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"Iteration {iterationCount + 1}",
                    ProgressPercent = percentComplete
                });

                // Increment iteration count for next time
                iterationCount++;
                workflowContext.Variables[counterKey] = iterationCount;

                // Mark this iteration check as completed
                // Child will send Complete back to us (via feedback connection) to trigger next iteration
                instance.Status = NodeExecutionStatus.Completed;
                instance.EndTime = DateTime.UtcNow;
                // Use special port to prevent routing to final Complete handlers
                instance.SourcePort = "IterationCheck";
            }
            else
            {
                // Condition is false: loop is done
                nodeContext.OutputData["IterationCount"] = iterationCount;
                nodeContext.OutputData["Condition"] = this.Condition;
                nodeContext.OutputData["MaxIterations"] = this.MaxIterations;

                // Clean up iteration counter
                workflowContext.Variables.TryRemove(counterKey, out _);

                // Set SourcePort to LoopBody to indicate loop iterations completed
                instance.SourcePort = LoopBodyPort;
                instance.Status = NodeExecutionStatus.Completed;
                instance.EndTime = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Node execution was cancelled";
            // Clean up iteration counter
            workflowContext.Variables.TryRemove(counterKey, out _);
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
            // Clean up iteration counter
            workflowContext.Variables.TryRemove(counterKey, out _);
        }

        return instance;
    }

    /// <summary>
    /// Evaluates the condition expression using Roslyn scripting.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if condition evaluates to true, false otherwise.</returns>
    private async Task<bool> EvaluateConditionAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Condition))
        {
            throw new InvalidOperationException("Condition cannot be null or empty");
        }

        // Create execution state for script evaluation
        var state = this.CreateExecutionState(workflowContext, nodeContext);

        try
        {
            // Create script options
            var scriptOptions = ScriptOptions.Default
                .AddReferences(typeof(ExecutionState).Assembly)
                .AddImports("System", "System.Collections", "System.Collections.Generic", "System.Linq");

            // Create and compile the script
            var script = CSharpScript.Create<object>(
                this.Condition,
                scriptOptions,
                globalsType: typeof(ExecutionState));

            // Pre-compile to catch syntax errors
            var diagnostics = script.Compile(cancellationToken);
            if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
                throw new InvalidOperationException($"Condition expression compilation failed:{Environment.NewLine}{errors}");
            }

            // Execute the script and get the result
            var scriptState = await script.RunAsync(state, cancellationToken);
            var result = scriptState.ReturnValue;

            // Convert result to bool
            if (result is bool boolResult)
            {
                return boolResult;
            }

            throw new InvalidOperationException(
                $"Condition expression did not return a boolean. Returned type: {result?.GetType().Name ?? "null"}");
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(", ", ex.Diagnostics.Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Condition expression compilation failed: {errors}", ex);
        }
    }
}
