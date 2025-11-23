// -----------------------------------------------------------------------
// <copyright file="ForEachNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Collections;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Messages;
using ExecutionEngine.Nodes.Definitions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

/// <summary>
/// Node that iterates over a collection and stores iteration data.
/// Uses Roslyn scripting engine to evaluate collection expressions.
/// For each iteration, sets item variables in workflow context.
/// </summary>
public class ForEachNode : ExecutableNodeBase
{
    /// <summary>
    /// Port name for loop body iteration messages.
    /// </summary>
    public const string LoopBodyPort = "LoopBody";

    /// <summary>
    /// Gets or sets the expression that returns a collection to iterate over.
    /// This should be a valid C# expression that evaluates to IEnumerable.
    /// </summary>
    public string CollectionExpression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the variable name for the current item in each iteration.
    /// Defaults to "item".
    /// </summary>
    public string ItemVariableName { get; set; } = "item";

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        if (definition is not ForEachNodeDefinition forEachNodeDefinition)
        {
            throw new InvalidOperationException("ForEachNodeDefinition is not supported");
        }

        this.Definition = forEachNodeDefinition;
        this.CollectionExpression = forEachNodeDefinition.CollectionExpression;
        this.ItemVariableName = forEachNodeDefinition.ItemVariableName;
    }

    /// <summary>
    /// Gets the available output ports for this node.
    /// </summary>
    /// <returns>Array of available port names.</returns>
    public string[] GetAvailablePorts()
    {
        // ForEach node has LoopBody port for iteration messages
        return new[] { LoopBodyPort };
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

            // Evaluate collection expression
            var collection = await this.EvaluateCollectionAsync(workflowContext, nodeContext, cancellationToken);

            if (collection == null)
            {
                throw new InvalidOperationException("Collection expression evaluated to null");
            }

            // Iterate over collection
            var itemCount = 0;
            var items = collection.Cast<object>().ToList();
            var totalCount = items.Count;

            // Get router if available for message routing
            var router = workflowContext.Router;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Set item variables in workflow context
                workflowContext.Variables[this.ItemVariableName] = item;
                workflowContext.Variables[$"{this.ItemVariableName}Index"] = itemCount;

                // Create iteration context for this item
                var iterationContext = new NodeExecutionContext
                {
                    InputData = new Dictionary<string, object>
                    {
                        { this.ItemVariableName, item },
                        { $"{this.ItemVariableName}Index", itemCount }
                    },
                    OutputData = new Dictionary<string, object>()
                };

                // Emit OnNext event to trigger child node execution for this iteration
                this.RaiseOnNext(new NodeNextEventArgs
                {
                    NodeId = this.NodeId,
                    NodeInstanceId = instance.NodeInstanceId,
                    IterationIndex = itemCount,
                    IterationContext = iterationContext,
                    Metadata = new Dictionary<string, object>
                    {
                        { "TotalItems", totalCount },
                        { "ItemValue", item }
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
                        IterationIndex = itemCount,
                        IterationContext = iterationContext,
                        Metadata = new Dictionary<string, object>
                        {
                            { "TotalItems", totalCount },
                            { "ItemValue", item }
                        }
                    };

                    await router.RouteMessageAsync(nextMessage, workflowContext, cancellationToken);
                }

                // Emit progress event
                var percentComplete = totalCount > 0 ? (int)((itemCount * 100.0) / totalCount) : 0;
                this.RaiseOnProgress(new ProgressEventArgs
                {
                    Status = $"Processing item {itemCount + 1} of {totalCount}",
                    ProgressPercent = percentComplete
                });

                itemCount++;
            }

            // Store iteration results in output data
            nodeContext.OutputData["ItemsProcessed"] = itemCount;
            nodeContext.OutputData["CollectionExpression"] = this.CollectionExpression;
            nodeContext.OutputData["TotalItems"] = totalCount;

            // Set SourcePort to LoopBody to indicate loop iterations completed
            instance.SourcePort = LoopBodyPort;
            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            instance.Status = NodeExecutionStatus.Cancelled;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = "Node execution was cancelled";
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
    /// Evaluates the collection expression using Roslyn scripting.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The evaluated collection as IEnumerable.</returns>
    private async Task<IEnumerable?> EvaluateCollectionAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.CollectionExpression))
        {
            throw new InvalidOperationException("CollectionExpression cannot be null or empty");
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
                this.CollectionExpression,
                scriptOptions,
                globalsType: typeof(ExecutionState));

            // Pre-compile to catch syntax errors
            var diagnostics = script.Compile(cancellationToken);
            if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
                throw new InvalidOperationException($"Collection expression compilation failed:{Environment.NewLine}{errors}");
            }

            // Execute the script and get the result
            var scriptState = await script.RunAsync(state, cancellationToken);
            var result = scriptState.ReturnValue;

            // Check for null result
            if (result == null)
            {
                throw new InvalidOperationException("Collection expression evaluated to null");
            }

            // Convert result to IEnumerable
            if (result is IEnumerable enumerable)
            {
                return enumerable;
            }

            throw new InvalidOperationException(
                $"Collection expression did not return an IEnumerable. Returned type: {result.GetType().Name}");
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(", ", ex.Diagnostics.Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Collection expression compilation failed: {errors}", ex);
        }
    }
}
