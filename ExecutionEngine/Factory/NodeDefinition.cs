// -----------------------------------------------------------------------
// <copyright file="NodeDefinition.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Factory;

using ExecutionEngine.Enums;

/// <summary>
/// Defines metadata for dynamically loading and instantiating nodes.
/// Supports both C# compiled assemblies and PowerShell scripts.
/// </summary>
public class NodeDefinition
{
    /// <summary>
    /// Gets or sets the node ID.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node display name.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node type (Task, IfElse, ForEach, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime type (CSharp, PowerShell, Subflow).
    /// </summary>
    public string RuntimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the assembly path for compiled C# nodes.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified type name for C# nodes.
    /// </summary>
    public string? TypeName { get; set; }

    /// <summary>
    /// Gets or sets the script path for PowerShell nodes.
    /// </summary>
    public string? ScriptPath { get; set; }

    /// <summary>
    /// Gets or sets the required PowerShell modules.
    /// </summary>
    public List<string>? RequiredModules { get; set; }

    /// <summary>
    /// Gets or sets custom module paths for PowerShell modules.
    /// Key: module name, Value: module path
    /// </summary>
    public Dictionary<string, string>? ModulePaths { get; set; }

    /// <summary>
    /// Gets or sets additional configuration for the node.
    /// </summary>
    public Dictionary<string, object>? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the join type for nodes with multiple inbound connections.
    /// Any: Trigger when ANY upstream completes (OR logic) - default.
    /// All: Trigger only when ALL upstreams complete (AND logic).
    /// </summary>
    public JoinType JoinType { get; set; } = JoinType.Any;

    /// <summary>
    /// Gets or sets the execution priority for this node.
    /// Higher priority nodes are scheduled before lower priority nodes when
    /// workflow-level concurrency limits are reached.
    /// Default is Normal priority.
    /// </summary>
    public NodePriority Priority { get; set; } = NodePriority.Normal;

    /// <summary>
    /// Gets or sets the maximum number of concurrent executions allowed for this node type.
    /// 0 = unlimited (default). Used for per-node-type throttling to prevent resource exhaustion.
    /// Example: Limit database query nodes to 5 concurrent executions.
    /// </summary>
    public int MaxConcurrentExecutions { get; set; } = 0;

    /// <summary>
    /// Gets or sets the retry policy for this node.
    /// Defines how the node should behave when it fails (retry strategies, delays, conditions).
    /// If null, no retry is performed (fail immediately).
    /// </summary>
    public Policies.RetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker policy for this node.
    /// Prevents cascading failures by temporarily blocking calls to failing nodes.
    /// If null, no circuit breaker is applied.
    /// </summary>
    public Policies.CircuitBreakerPolicy? CircuitBreakerPolicy { get; set; }

    /// <summary>
    /// Gets or sets the node ID to execute for compensation (undo) on workflow failure.
    /// Used in the Saga pattern to rollback completed operations.
    /// The compensation node receives a CompensationContext with failure details.
    /// If null, no compensation is performed for this node.
    /// </summary>
    public string? CompensationNodeId { get; set; }

    /// <summary>
    /// Gets or sets the fallback node ID to execute when the circuit breaker is open.
    /// Provides an alternative execution path when the primary node is unavailable.
    /// Example: Use cached data when external API is failing.
    /// If null, requests fail immediately when circuit is open.
    /// </summary>
    public string? FallbackNodeId { get; set; }
}
