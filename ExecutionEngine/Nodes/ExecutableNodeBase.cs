// -----------------------------------------------------------------------
// <copyright file="ExecutableNodeBase.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Factory;

/// <summary>
/// Abstract base class for executable nodes.
/// Provides common functionality for event handling and execution state creation.
/// </summary>
public abstract class ExecutableNodeBase : INode
{
    protected NodeDefinition? definition;

    /// <inheritdoc/>
    public virtual string NodeId => this.definition?.NodeId ?? string.Empty;

    /// <inheritdoc/>
    public virtual string NodeName => this.definition?.NodeName ?? string.Empty;

    /// <inheritdoc/>
    public event EventHandler<NodeStartEventArgs>? OnStart;

    /// <inheritdoc/>
    public event EventHandler<ProgressEventArgs>? OnProgress;

    /// <inheritdoc/>
    public virtual void Initialize(NodeDefinition definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <inheritdoc/>
    public abstract Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Raises the OnStart event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    protected virtual void RaiseOnStart(NodeStartEventArgs args)
    {
        this.OnStart?.Invoke(this, args);
    }

    /// <summary>
    /// Raises the OnProgress event.
    /// </summary>
    /// <param name="args">Event arguments.</param>
    protected virtual void RaiseOnProgress(ProgressEventArgs args)
    {
        this.OnProgress?.Invoke(this, args);
    }

    /// <summary>
    /// Creates an execution state object for scripts.
    /// </summary>
    /// <param name="workflowContext">Workflow context.</param>
    /// <param name="nodeContext">Node context.</param>
    /// <returns>Execution state with helper functions.</returns>
    protected ExecutionState CreateExecutionState(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext)
    {
        return new ExecutionState
        {
            WorkflowContext = workflowContext,
            NodeContext = nodeContext,
            GlobalVariables = workflowContext.Variables,
            Input = nodeContext.InputData,
            Local = nodeContext.LocalVariables,
            Output = nodeContext.OutputData,
            SetOutput = (key, value) => nodeContext.OutputData[key] = value,
            GetInput = (key) => nodeContext.InputData.TryGetValue(key, out var val) ? val : null,
            GetGlobal = (key) => workflowContext.Variables.TryGetValue(key, out var val) ? val : null,
            SetGlobal = (key, value) => workflowContext.Variables[key] = value
        };
    }
}
