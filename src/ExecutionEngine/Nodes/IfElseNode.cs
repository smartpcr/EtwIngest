// -----------------------------------------------------------------------
// <copyright file="IfElseNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

/// <summary>
/// Node that evaluates a condition and routes execution to either the true or false branch.
/// Uses Roslyn scripting engine to evaluate C# boolean expressions.
/// </summary>
public class IfElseNode : ExecutableNodeBase
{
    /// <summary>
    /// Port name for the true branch.
    /// </summary>
    public const string TrueBranchPort = "TrueBranch";

    /// <summary>
    /// Port name for the false branch.
    /// </summary>
    public const string FalseBranchPort = "FalseBranch";

    /// <summary>
    /// Gets or sets the condition expression to evaluate.
    /// This should be a valid C# boolean expression.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        if (definition is not IfElseNodeDefinition ifElseDefinition)
        {
            throw new ArgumentException("Invalid node definition type for IfElseNode.");
        }

        this.Definition = ifElseDefinition;
        this.Condition = ifElseDefinition.Condition;
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

            if (string.IsNullOrWhiteSpace(this.Condition))
            {
                throw new InvalidOperationException("Condition expression is not defined.");
            }

            // Evaluate the condition using Roslyn scripting
            var result = await this.EvaluateConditionAsync(workflowContext, nodeContext, cancellationToken);

            // Set the source port based on the condition result
            instance.SourcePort = result ? TrueBranchPort : FalseBranchPort;
            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            // Store the branch taken in output data for debugging/tracking
            nodeContext.OutputData["BranchTaken"] = instance.SourcePort;
            nodeContext.OutputData["ConditionResult"] = result;
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

    public string[] GetAvailablePorts()
    {
        return new[] { TrueBranchPort, FalseBranchPort };
    }

    /// <summary>
    /// Evaluates the condition expression asynchronously.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the condition evaluation.</returns>
    private async Task<bool> EvaluateConditionAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        // Create execution state for script context
        var state = this.CreateExecutionState(workflowContext, nodeContext);

        // Create script options
        var scriptOptions = ScriptOptions.Default
            .AddReferences(typeof(ExecutionState).Assembly)
            .AddImports("System", "System.Collections.Generic", "System.Linq");

        // Compile and evaluate the condition expression
        var script = CSharpScript.Create<bool>(
            this.Condition,
            scriptOptions,
            globalsType: typeof(ExecutionState));

        // Pre-compile to catch syntax errors
        var diagnostics = script.Compile(cancellationToken);
        if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
        {
            var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException($"Condition compilation failed:{Environment.NewLine}{errors}");
        }

        // Execute the script and return the result
        var scriptState = await script.RunAsync(state, cancellationToken);
        return scriptState.ReturnValue;
    }
}
