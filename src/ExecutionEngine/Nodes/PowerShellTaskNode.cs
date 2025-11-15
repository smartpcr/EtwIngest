// -----------------------------------------------------------------------
// <copyright file="PowerShellTaskNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;

/// <summary>
/// Node that executes PowerShell scripts with helper cmdlets for workflow integration.
/// Supports both inline scripts and script files.
/// Provides Get-Input, Set-Output, Get-Global, Set-Global cmdlets.
/// </summary>
public class PowerShellTaskNode : ExecutableNodeBase
{
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

            // Load script from file if not using inline script
            if (string.IsNullOrWhiteSpace(this.scriptContent))
            {
                await this.LoadScriptFromFileAsync(cancellationToken);
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
    /// Loads the PowerShell script content from file.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadScriptFromFileAsync(CancellationToken cancellationToken)
    {
        if (this.definition == null)
        {
            throw new InvalidOperationException("Node has not been initialized.");
        }

        if (string.IsNullOrWhiteSpace(this.definition.ScriptPath))
        {
            throw new InvalidOperationException("PowerShellTaskNode must have either inline script or ScriptPath.");
        }

        if (!File.Exists(this.definition.ScriptPath))
        {
            throw new FileNotFoundException($"Script file not found: {this.definition.ScriptPath}");
        }

        this.scriptContent = await File.ReadAllTextAsync(this.definition.ScriptPath, cancellationToken);
    }

    /// <summary>
    /// Executes the PowerShell script with the execution state and helper cmdlets.
    /// </summary>
    /// <param name="state">The execution state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExecuteScriptAsync(ExecutionState state, CancellationToken cancellationToken)
    {
        // Use CreateDefault2() for cross-platform compatibility
        // This creates a minimal session state without loading Windows-specific snapins
        var initialSessionState = InitialSessionState.CreateDefault2();

        // Add custom cmdlets for workflow integration
        var getInputCmdlet = new SessionStateCmdletEntry("Get-Input", typeof(GetInputCmdlet), null);
        var setOutputCmdlet = new SessionStateCmdletEntry("Set-Output", typeof(SetOutputCmdlet), null);
        var getGlobalCmdlet = new SessionStateCmdletEntry("Get-Global", typeof(GetGlobalCmdlet), null);
        var setGlobalCmdlet = new SessionStateCmdletEntry("Set-Global", typeof(SetGlobalCmdlet), null);

        initialSessionState.Commands.Add(getInputCmdlet);
        initialSessionState.Commands.Add(setOutputCmdlet);
        initialSessionState.Commands.Add(getGlobalCmdlet);
        initialSessionState.Commands.Add(setGlobalCmdlet);

        // Import required modules if specified
        if (this.definition?.RequiredModules != null)
        {
            foreach (var moduleName in this.definition.RequiredModules)
            {
                // Check if custom module path is provided
                if (this.definition.ModulePaths?.TryGetValue(moduleName, out var modulePath) == true)
                {
                    // Normalize path for cross-platform compatibility
                    var normalizedPath = Path.GetFullPath(modulePath);
                    initialSessionState.ImportPSModule(new[] { normalizedPath });
                }
                else
                {
                    initialSessionState.ImportPSModule(new[] { moduleName });
                }
            }
        }

        // Create runspace with options for better cross-platform behavior
        using var runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        runspace.Open();

        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;

        // Set the $State variable for cmdlets to access
        runspace.SessionStateProxy.SetVariable("State", state);

        // Collections to capture all output
        var outputData = new List<string>();
        var verboseData = new List<string>();
        var warningData = new List<string>();
        var errorData = new List<string>();
        var debugData = new List<string>();
        var informationData = new List<string>();

        // Subscribe to stream events for real-time capture
        powerShell.Streams.Verbose.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Verbose[e.Index];
            var message = $"[VERBOSE] {record.Message}";
            verboseData.Add(message);
            Console.WriteLine(message);
        };

        powerShell.Streams.Warning.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Warning[e.Index];
            var message = $"[WARNING] {record.Message}";
            warningData.Add(message);
            Console.WriteLine(message);
        };

        powerShell.Streams.Error.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Error[e.Index];
            var message = $"[ERROR] {record.Exception?.Message ?? record.ToString()}";
            errorData.Add(message);
            Console.WriteLine(message);
        };

        powerShell.Streams.Debug.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Debug[e.Index];
            var message = $"[DEBUG] {record.Message}";
            debugData.Add(message);
            Console.WriteLine(message);
        };

        powerShell.Streams.Information.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Information[e.Index];
            var message = $"[INFO] {record.MessageData}";
            informationData.Add(message);
            Console.WriteLine(message);
        };

        powerShell.Streams.Progress.DataAdded += (sender, e) =>
        {
            var record = powerShell.Streams.Progress[e.Index];
            Console.WriteLine($"[PROGRESS] {record.Activity}: {record.StatusDescription} ({record.PercentComplete}%)");
        };

        // Add the script
        powerShell.AddScript(this.scriptContent!);

        // Execute with proper cancellation support
        try
        {
            var psTask = Task.Run(() =>
            {
                var results = powerShell.Invoke();

                // Capture regular output
                foreach (var result in results)
                {
                    var output = result?.BaseObject?.ToString() ?? "(null)";
                    outputData.Add(output);
                    Console.WriteLine($"[OUTPUT] {output}");
                }

                // Check for errors
                if (powerShell.HadErrors)
                {
                    var errors = powerShell.Streams.Error.ReadAll();
                    var errorMessages = string.Join(Environment.NewLine,
                        errors.Select(e => $"{e.Exception?.Message ?? e.ToString()}\n{e.ScriptStackTrace}"));
                    throw new InvalidOperationException(
                        $"PowerShell script execution failed:{Environment.NewLine}{errorMessages}");
                }

                return results;
            }, cancellationToken);

            await psTask.ConfigureAwait(false);

            // Log summary of captured streams
            if (outputData.Count > 0 || verboseData.Count > 0 || warningData.Count > 0 ||
                errorData.Count > 0 || debugData.Count > 0 || informationData.Count > 0)
            {
                Console.WriteLine("\n=== PowerShell Execution Summary ===");
                Console.WriteLine($"Output lines: {outputData.Count}");
                Console.WriteLine($"Verbose lines: {verboseData.Count}");
                Console.WriteLine($"Warning lines: {warningData.Count}");
                Console.WriteLine($"Error lines: {errorData.Count}");
                Console.WriteLine($"Debug lines: {debugData.Count}");
                Console.WriteLine($"Information lines: {informationData.Count}");
            }
        }
        catch (OperationCanceledException)
        {
            powerShell.Stop();
            throw;
        }
    }
}

/// <summary>
/// PowerShell cmdlet to get input data from previous nodes.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Input")]
public class GetInputCmdlet : PSCmdlet
{
    /// <summary>
    /// Gets or sets the key to retrieve from input data.
    /// </summary>
    [Parameter(Position = 0, Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Processes the cmdlet to get input value.
    /// </summary>
    protected override void ProcessRecord()
    {
        var state = this.SessionState.PSVariable.GetValue("State") as ExecutionState;
        if (state != null && state.Input.TryGetValue(this.Key, out var value))
        {
            this.WriteObject(value);
        }
        else
        {
            this.WriteObject(null);
        }
    }
}

/// <summary>
/// PowerShell cmdlet to set output data for downstream nodes.
/// </summary>
[Cmdlet(VerbsCommon.Set, "Output")]
public class SetOutputCmdlet : PSCmdlet
{
    /// <summary>
    /// Gets or sets the key to set in output data.
    /// </summary>
    [Parameter(Position = 0, Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value to set in output data.
    /// </summary>
    [Parameter(Position = 1, Mandatory = true)]
    public object? Value { get; set; }

    /// <summary>
    /// Processes the cmdlet to set output value.
    /// </summary>
    protected override void ProcessRecord()
    {
        var state = this.SessionState.PSVariable.GetValue("State") as ExecutionState;
        if (state != null)
        {
            state.SetOutput(this.Key, this.Value!);
        }
    }
}

/// <summary>
/// PowerShell cmdlet to get global workflow variables.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Global")]
public class GetGlobalCmdlet : PSCmdlet
{
    /// <summary>
    /// Gets or sets the key to retrieve from global variables.
    /// </summary>
    [Parameter(Position = 0, Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Processes the cmdlet to get global value.
    /// </summary>
    protected override void ProcessRecord()
    {
        var state = this.SessionState.PSVariable.GetValue("State") as ExecutionState;
        if (state != null)
        {
            var value = state.GetGlobal(this.Key);
            this.WriteObject(value);
        }
        else
        {
            this.WriteObject(null);
        }
    }
}

/// <summary>
/// PowerShell cmdlet to set global workflow variables.
/// </summary>
[Cmdlet(VerbsCommon.Set, "Global")]
public class SetGlobalCmdlet : PSCmdlet
{
    /// <summary>
    /// Gets or sets the key to set in global variables.
    /// </summary>
    [Parameter(Position = 0, Mandatory = true)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value to set in global variables.
    /// </summary>
    [Parameter(Position = 1, Mandatory = true)]
    public object? Value { get; set; }

    /// <summary>
    /// Processes the cmdlet to set global value.
    /// </summary>
    protected override void ProcessRecord()
    {
        var state = this.SessionState.PSVariable.GetValue("State") as ExecutionState;
        if (state != null)
        {
            state.SetGlobal(this.Key, this.Value!);
        }
    }
}
