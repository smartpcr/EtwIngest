// -----------------------------------------------------------------------
// <copyright file="PowerShellScriptNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;

/// <summary>
/// Node that executes PowerShell scripts using System.Management.Automation.
/// Scripts have access to $State variable containing ExecutionState.
/// </summary>
public class PowerShellScriptNode : ExecutableNodeBase
{
    private string? scriptContent;

    /// <summary>
    /// Executes the PowerShell script asynchronously.
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
            if (this.scriptContent == null)
            {
                await this.LoadScriptAsync(cancellationToken);
            }

            // Create execution state for script
            var state = this.CreateExecutionState(workflowContext, nodeContext);

            // Execute the script
            await this.ExecuteScriptAsync(state, cancellationToken);

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
    /// Loads the PowerShell script content.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadScriptAsync(CancellationToken cancellationToken)
    {
        if (this.definition == null)
        {
            throw new InvalidOperationException("Node has not been initialized.");
        }

        if (string.IsNullOrWhiteSpace(this.definition.ScriptPath))
        {
            throw new InvalidOperationException("ScriptPath is not defined.");
        }

        if (!File.Exists(this.definition.ScriptPath))
        {
            throw new FileNotFoundException($"Script file not found: {this.definition.ScriptPath}");
        }

        this.scriptContent = await File.ReadAllTextAsync(this.definition.ScriptPath, cancellationToken);
    }

    /// <summary>
    /// Executes the PowerShell script with the execution state.
    /// </summary>
    /// <param name="state">The execution state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExecuteScriptAsync(ExecutionState state, CancellationToken cancellationToken)
    {
        // Create initial session state
        var initialSessionState = InitialSessionState.CreateDefault();

        // Import required modules if specified
        if (this.definition?.RequiredModules != null)
        {
            foreach (var moduleName in this.definition.RequiredModules)
            {
                // Check if custom module path is provided
                if (this.definition.ModulePaths?.TryGetValue(moduleName, out var modulePath) == true)
                {
                    initialSessionState.ImportPSModule(new[] { modulePath });
                }
                else
                {
                    initialSessionState.ImportPSModule(new[] { moduleName });
                }
            }
        }

        // Create runspace
        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;

        // Set the $State variable
        runspace.SessionStateProxy.SetVariable("State", state);

        // Add the script
        powerShell.AddScript(this.scriptContent!);

        // Execute asynchronously
        var psTask = Task.Run(() =>
        {
            var results = powerShell.Invoke();

            // Check for errors
            if (powerShell.HadErrors)
            {
                var errors = powerShell.Streams.Error.ReadAll();
                var errorMessages = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell script execution failed:{Environment.NewLine}{errorMessages}");
            }

            return results;
        }, cancellationToken);

        await psTask;
    }

    /// <summary>
    /// Gets the script content (for testing/diagnostics).
    /// </summary>
    public string? ScriptContent => this.scriptContent;
}
