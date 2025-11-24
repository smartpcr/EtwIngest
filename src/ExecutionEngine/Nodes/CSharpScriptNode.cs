// -----------------------------------------------------------------------
// <copyright file="CSharpScriptNode.cs" company="Microsoft Corp.">
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
/// Node that executes C# scripts using Roslyn scripting engine.
/// Scripts have access to ExecutionState for input/output and global variables.
/// </summary>
public class CSharpScriptNode : ExecutableNodeBase
{
    private Script<object>? compiledScript;
    private string? scriptContent;

    public override void Initialize(NodeDefinition definition)
    {
        if (definition is not CSharpScriptNodeDefinition scriptDefinition)
        {
            throw new InvalidOperationException($"Node definition is invalid for CSharpScriptNode: {definition.GetType().FullName}");
        }

        this.Definition = scriptDefinition;
    }

    /// <summary>
    /// Executes the C# script asynchronously.
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

            // Load script if not already loaded
            if (this.compiledScript == null)
            {
                await this.LoadScriptAsync(cancellationToken);
            }

            // Create execution state for script
            var state = this.CreateExecutionState(workflowContext, nodeContext);

            // Execute the script
            var scriptState = await this.compiledScript!.RunAsync(state, cancellationToken);

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
    /// Loads and compiles the C# script.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadScriptAsync(CancellationToken cancellationToken)
    {
        if (this.Definition == null)
        {
            throw new InvalidOperationException("Node has not been initialized.");
        }

        var scriptDefinition = this.Definition as CSharpScriptNodeDefinition;
        if (scriptDefinition == null)
        {
            throw new InvalidOperationException($"Node definition is invalid for CSharpScriptNode: {this.Definition.GetType().FullName}");
        }

        if (!string.IsNullOrEmpty(scriptDefinition.ScriptContent))
        {
            this.scriptContent = scriptDefinition.ScriptContent;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(scriptDefinition.ScriptPath))
            {
                throw new InvalidOperationException("ScriptPath is not defined.");
            }

            if (!File.Exists(scriptDefinition.ScriptPath))
            {
                throw new FileNotFoundException($"Script file not found: {scriptDefinition.ScriptPath}");
            }

            // Read script content
            this.scriptContent = await File.ReadAllTextAsync(scriptDefinition.ScriptPath, cancellationToken);
        }

        // Compile script with ExecutionState as globals
        var scriptOptions = ScriptOptions.Default
            .AddReferences(typeof(ExecutionState).Assembly)
            .AddImports("System", "System.Collections.Generic", "System.Linq");

        this.compiledScript = CSharpScript.Create<object>(
            this.scriptContent,
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

    /// <summary>
    /// Gets the compiled script content (for testing/diagnostics).
    /// </summary>
    public string? ScriptContent => this.scriptContent;
}
