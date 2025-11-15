// -----------------------------------------------------------------------
// <copyright file="SwitchNode.cs" company="Microsoft Corp.">
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
/// Node that evaluates an expression and routes execution based on matching case values.
/// Uses Roslyn scripting engine to evaluate C# expressions.
/// Similar to C# switch statement - routes to different ports based on expression result.
/// </summary>
public class SwitchNode : ExecutableNodeBase
{
    /// <summary>
    /// Port name for the default case (when no case matches).
    /// </summary>
    public const string DefaultPort = "Default";

    /// <summary>
    /// Gets or sets the expression to evaluate.
    /// The result of this expression is matched against case values.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of case values to port names.
    /// Key: The value to match against the expression result (e.g., "success", "failure")
    /// Value: The port name to route to (e.g., "SuccessPort", "FailurePort")
    /// If value is null/empty, the key is used as the port name.
    /// </summary>
    public Dictionary<string, string> Cases { get; set; } = new Dictionary<string, string>();

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get expression from definition configuration
        if (definition.Configuration != null)
        {
            if (definition.Configuration.TryGetValue("Expression", out var expressionValue))
            {
                this.Expression = expressionValue?.ToString() ?? string.Empty;
            }

            if (definition.Configuration.TryGetValue("Cases", out var casesValue))
            {
                if (casesValue is Dictionary<string, string> casesDict)
                {
                    this.Cases = casesDict;
                }
                else if (casesValue is Dictionary<string, object> casesObjDict)
                {
                    // Convert object dictionary to string dictionary
                    this.Cases = casesObjDict.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? kvp.Key);
                }
            }
        }
    }

    /// <summary>
    /// Gets the available output ports for this node.
    /// Returns all case port names plus the default port.
    /// </summary>
    /// <returns>Array of available port names.</returns>
    public string[] GetAvailablePorts()
    {
        var ports = new List<string>();

        // Add all case ports
        foreach (var caseEntry in this.Cases)
        {
            var portName = string.IsNullOrWhiteSpace(caseEntry.Value) ? caseEntry.Key : caseEntry.Value;
            if (!ports.Contains(portName))
            {
                ports.Add(portName);
            }
        }

        // Add default port
        ports.Add(DefaultPort);

        return ports.ToArray();
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

            if (string.IsNullOrWhiteSpace(this.Expression))
            {
                throw new InvalidOperationException("Expression is not defined.");
            }

            // Evaluate the expression using Roslyn scripting
            var expressionResult = await this.EvaluateExpressionAsync(workflowContext, nodeContext, cancellationToken);

            // Convert result to string for comparison
            var resultString = expressionResult?.ToString() ?? string.Empty;

            // Find matching case
            string? matchedPort = null;
            foreach (var caseEntry in this.Cases)
            {
                if (string.Equals(caseEntry.Key, resultString, StringComparison.Ordinal))
                {
                    // Use the port name from the case value, or the key if value is empty
                    matchedPort = string.IsNullOrWhiteSpace(caseEntry.Value) ? caseEntry.Key : caseEntry.Value;
                    break;
                }
            }

            // Set the source port based on matched case or default
            instance.SourcePort = matchedPort ?? DefaultPort;
            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            // Store the result in output data for debugging/tracking
            nodeContext.OutputData["ExpressionResult"] = resultString;
            nodeContext.OutputData["MatchedCase"] = matchedPort ?? "Default";
            nodeContext.OutputData["PortSelected"] = instance.SourcePort;
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
    /// Evaluates the expression using Roslyn scripting.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the expression evaluation.</returns>
    private async Task<object?> EvaluateExpressionAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(this.Expression))
        {
            throw new InvalidOperationException("Expression cannot be null or empty");
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
                this.Expression,
                scriptOptions,
                globalsType: typeof(ExecutionState));

            // Pre-compile to catch syntax errors
            var diagnostics = script.Compile(cancellationToken);
            if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
                throw new InvalidOperationException($"Expression compilation failed:{Environment.NewLine}{errors}");
            }

            // Execute the script and get the result
            var scriptState = await script.RunAsync(state, cancellationToken);
            return scriptState.ReturnValue;
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(", ", ex.Diagnostics.Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Expression compilation failed: {errors}", ex);
        }
    }
}
