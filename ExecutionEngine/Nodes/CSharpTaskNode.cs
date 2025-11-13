// -----------------------------------------------------------------------
// <copyright file="CSharpTaskNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

/// <summary>
/// Node that executes C# code either as inline scripts or compiled executors.
/// Supports both Roslyn scripting for inline scripts and delegate execution for compiled assemblies.
/// </summary>
public class CSharpTaskNode : ExecutableNodeBase
{
    private Func<ExecutionState, CancellationToken, Task<Dictionary<string, object>?>>? compiledExecutor;
    private Script<object>? compiledScript;
    private string? scriptContent;

    /// <summary>
    /// Gets or sets the inline script content.
    /// </summary>
    public string? ScriptContent
    {
        get => this.scriptContent;
        set => this.scriptContent = value;
    }

    /// <summary>
    /// Initializes the node with its definition.
    /// </summary>
    /// <param name="definition">The node definition.</param>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Check for inline script in configuration
        if (definition.Configuration?.TryGetValue("script", out var scriptObj) == true)
        {
            this.scriptContent = scriptObj?.ToString();
        }
    }

    /// <summary>
    /// Executes the node asynchronously.
    /// </summary>
    /// <param name="workflowContext">The workflow execution context.</param>
    /// <param name="nodeContext">The node execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The node instance representing the execution result.</returns>
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

            // Create execution state for script/executor
            var state = this.CreateExecutionState(workflowContext, nodeContext);

            Dictionary<string, object>? result = null;

            // Execute based on type (inline script vs compiled executor)
            if (this.compiledExecutor != null)
            {
                // Compiled assembly path: invoke the executor function
                result = await this.compiledExecutor(state, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(this.scriptContent))
            {
                // Inline script path: use Roslyn
                result = await this.ExecuteInlineScriptAsync(state, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("CSharpTaskNode must have either ScriptContent or compiled executor.");
            }

            // Populate output from result (if script returned a dictionary)
            if (result != null)
            {
                foreach (var kvp in result)
                {
                    nodeContext.OutputData[kvp.Key] = kvp.Value;
                }
            }

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;
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
    /// Sets the compiled executor for this node.
    /// Used when loading nodes from compiled assemblies.
    /// </summary>
    /// <param name="executor">The executor function.</param>
    public void SetExecutor(Func<ExecutionState, CancellationToken, Task<Dictionary<string, object>?>> executor)
    {
        this.compiledExecutor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <summary>
    /// Executes inline C# script using Roslyn scripting engine.
    /// </summary>
    /// <param name="state">The execution state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary result from script, or null.</returns>
    private async Task<Dictionary<string, object>?> ExecuteInlineScriptAsync(
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        // Compile script if not already compiled
        if (this.compiledScript == null)
        {
            var scriptOptions = ScriptOptions.Default
                .AddReferences(typeof(ExecutionState).Assembly)
                .AddImports("System", "System.Collections.Generic", "System.Threading.Tasks", "System.Linq");

            this.compiledScript = CSharpScript.Create<object>(
                this.scriptContent!,
                scriptOptions,
                globalsType: typeof(ExecutionState));

            // Pre-compile to catch syntax errors early
            var diagnostics = this.compiledScript.Compile(cancellationToken);
            if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                var errors = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
                throw new InvalidOperationException($"Script compilation failed:{Environment.NewLine}{errors}");
            }
        }

        // Execute the script
        var scriptState = await this.compiledScript.RunAsync(state, cancellationToken);

        // Script can return Dictionary<string, object> which becomes output
        return scriptState.ReturnValue as Dictionary<string, object>;
    }
}
