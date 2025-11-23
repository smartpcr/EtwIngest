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
using ExecutionEngine.Nodes.Definitions;

/// <summary>
/// Node that executes PowerShell scripts using System.Management.Automation.
/// Scripts have access to $State variable containing ExecutionState.
/// </summary>
public class PowerShellScriptNode : ExecutableNodeBase
{
    private string? scriptContent;

    public override void Initialize(NodeDefinition definition)
    {
        if (definition is not PowerShellScriptNodeDefinition scriptDefinition)
        {
            throw new ArgumentException(
                "Invalid node definition type. Expected PowerShellScriptNodeDefinition.",
                nameof(definition));
        }

        this.Definition = scriptDefinition;
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
        if (this.Definition == null)
        {
            throw new InvalidOperationException("Node has not been initialized.");
        }

        var scriptDefinition = this.Definition as PowerShellScriptNodeDefinition;
        if (scriptDefinition == null)
        {
            throw new InvalidOperationException("Node definition is not of type PowerShellScriptNodeDefinition.");
        }

        if (string.IsNullOrWhiteSpace(scriptDefinition.ScriptPath))
        {
            throw new InvalidOperationException("ScriptPath is not defined.");
        }

        if (!File.Exists(scriptDefinition.ScriptPath))
        {
            throw new FileNotFoundException($"Script file not found: {scriptDefinition.ScriptPath}");
        }

        this.scriptContent = await File.ReadAllTextAsync(scriptDefinition.ScriptPath, cancellationToken);
    }

    /// <summary>
    /// Executes the PowerShell script with the execution state.
    /// </summary>
    /// <param name="state">The execution state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ExecuteScriptAsync(ExecutionState state, CancellationToken cancellationToken)
    {
        // Use CreateDefault2() for cross-platform compatibility
        // This creates a minimal session state without loading Windows-specific snapins
        var initialSessionState = InitialSessionState.CreateDefault2();

        var scriptDefinition = this.Definition as PowerShellScriptNodeDefinition;
        if (scriptDefinition == null)
        {
            throw new InvalidOperationException("Node definition is not of type PowerShellScriptNodeDefinition.");
        }

        // Import required modules if specified
        if (scriptDefinition.RequiredModules != null)
        {
            foreach (var moduleName in scriptDefinition.RequiredModules)
            {
                // Check if custom module path is provided
                if (scriptDefinition.ModulePaths?.TryGetValue(moduleName, out var modulePath) == true)
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

        // Set the $State variable
        var stateWrapper = new PowerShellStateWrapper(state);
        runspace.SessionStateProxy.SetVariable("State", stateWrapper);

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

        // Add the user script
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

    /// <summary>
    /// Gets the script content (for testing/diagnostics).
    /// </summary>
    public string? ScriptContent => this.scriptContent;
}

/// <summary>
/// Wrapper class that exposes ExecutionState functionality as PowerShell-friendly methods.
/// PowerShell can call these methods directly without needing to invoke delegates.
/// </summary>
public class PowerShellStateWrapper
{
    private readonly ExecutionState state;

    /// <summary>
    /// Initializes a new instance of the PowerShellStateWrapper class.
    /// </summary>
    /// <param name="state">The execution state to wrap.</param>
    public PowerShellStateWrapper(ExecutionState state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>
    /// Gets an input value by key.
    /// </summary>
    /// <param name="key">The input key.</param>
    /// <returns>The input value, or null if not found.</returns>
    public object? GetInput(string key)
    {
        // Directly access the Input dictionary to avoid delegate invocation issues
        return this.state.Input.TryGetValue(key, out var val) ? val : null;
    }

    /// <summary>
    /// Sets an output value.
    /// </summary>
    /// <param name="key">The output key.</param>
    /// <param name="value">The output value.</param>
    public void SetOutput(string key, object value)
    {
        // Directly set in the Output dictionary to avoid delegate invocation issues
        this.state.Output[key] = value;
    }

    /// <summary>
    /// Gets a global variable value by key.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <returns>The variable value, or null if not found.</returns>
    public object? GetGlobal(string key)
    {
        // Directly access the GlobalVariables dictionary to avoid delegate invocation issues
        return this.state.GlobalVariables.TryGetValue(key, out var val) ? val : null;
    }

    /// <summary>
    /// Sets a global variable value.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The variable value.</param>
    public void SetGlobal(string key, object value)
    {
        // Directly set in the GlobalVariables dictionary to avoid delegate invocation issues
        this.state.GlobalVariables[key] = value;
    }

    /// <summary>
    /// Gets the workflow execution context.
    /// </summary>
    public WorkflowExecutionContext WorkflowContext => this.state.WorkflowContext;

    /// <summary>
    /// Gets the node execution context.
    /// </summary>
    public NodeExecutionContext NodeContext => this.state.NodeContext;

    /// <summary>
    /// Gets the global variables dictionary.
    /// </summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, object> GlobalVariables => this.state.GlobalVariables;

    /// <summary>
    /// Gets the input data dictionary.
    /// </summary>
    public Dictionary<string, object> Input => this.state.Input;

    /// <summary>
    /// Gets the local variables dictionary.
    /// </summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, object> Local => this.state.Local;

    /// <summary>
    /// Gets the output data dictionary.
    /// </summary>
    public Dictionary<string, object> Output => this.state.Output;
}
