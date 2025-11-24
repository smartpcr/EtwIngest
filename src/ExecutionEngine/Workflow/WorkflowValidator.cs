// -----------------------------------------------------------------------
// <copyright file="WorkflowValidator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes.Definitions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Validates workflow definitions to ensure they are well-formed and executable.
/// </summary>
public class WorkflowValidator
{
    private readonly ILogger<WorkflowValidator> logger;

    /// <summary>
    /// Initializes a new instance of the WorkflowValidator class.
    /// </summary>
    /// <param name="serviceProvider">Optional service provider for DI-based logging.</param>
    public WorkflowValidator(IServiceProvider? serviceProvider = null)
    {
        // Get ILoggerFactory from service provider if available, otherwise use NullLoggerFactory
        var loggerFactory = serviceProvider?.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;
        this.logger = loggerFactory.CreateLogger<WorkflowValidator>();
    }

    /// <summary>
    /// Validates a workflow definition and returns a validation result.
    /// </summary>
    /// <param name="workflow">The workflow definition to validate.</param>
    /// <returns>A validation result containing any errors or warnings.</returns>
    public ValidationResult Validate(WorkflowDefinition workflow)
    {
        var result = new ValidationResult();

        if (workflow == null)
        {
            this.logger.LogError("Workflow definition is null");
            result.Errors.Add("Workflow definition cannot be null.");
            return result;
        }

        // Validate basic properties
        this.ValidateBasicProperties(workflow, result);

        // Validate nodes
        this.ValidateNodes(workflow, result);

        // Validate connections
        this.ValidateConnections(workflow, result);

        // Validate graph structure (cycles, entry points)
        this.ValidateGraphStructure(workflow, result);

        if (!result.IsValid)
        {
            this.logger.LogError("Workflow {WorkflowId} validation failed with {ErrorCount} errors",
                workflow.WorkflowId, result.Errors.Count);
            foreach (var error in result.Errors)
            {
                this.logger.LogError("Validation error: {Error}", error);
            }
        }

        return result;
    }

    /// <summary>
    /// Validates basic workflow properties.
    /// </summary>
    private void ValidateBasicProperties(WorkflowDefinition workflow, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(workflow.WorkflowId))
        {
            this.logger.LogError("WorkflowId is null or empty");
            result.Errors.Add("WorkflowId cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(workflow.WorkflowName))
        {
            result.Warnings.Add("WorkflowName is empty. Consider providing a descriptive name.");
        }

        if (workflow.MaxConcurrency < 0)
        {
            this.logger.LogError("MaxConcurrency is negative: {MaxConcurrency}", workflow.MaxConcurrency);
            result.Errors.Add("MaxConcurrency cannot be negative.");
        }

        if (workflow.TimeoutSeconds < 0)
        {
            this.logger.LogError("TimeoutSeconds is negative: {TimeoutSeconds}", workflow.TimeoutSeconds);
            result.Errors.Add("TimeoutSeconds cannot be negative.");
        }
    }

    /// <summary>
    /// Validates that nodes are properly defined.
    /// </summary>
    private void ValidateNodes(WorkflowDefinition workflow, ValidationResult result)
    {
        if (workflow.Nodes == null || workflow.Nodes.Count == 0)
        {
            this.logger.LogError("Workflow contains no nodes");
            result.Errors.Add("Workflow must contain at least one node.");
            return;
        }

        // Check for duplicate node IDs and validate each node
        var nodeIds = new HashSet<string>();
        foreach (var node in workflow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                this.logger.LogError("Node found with null or empty NodeId");
                result.Errors.Add("Node found with null or empty NodeId.");
                continue;
            }

            if (!nodeIds.Add(node.NodeId))
            {
                this.logger.LogError("Duplicate node ID found: {NodeId}", node.NodeId);
                result.Errors.Add($"Duplicate node ID found: '{node.NodeId}'.");
            }

            // Validate node-specific configuration
            this.ValidateNodeConfiguration(node, result);
        }

        // Validate entry point if specified
        if (!string.IsNullOrWhiteSpace(workflow.EntryPointNodeId))
        {
            if (!nodeIds.Contains(workflow.EntryPointNodeId))
            {
                this.logger.LogError("Entry point node {EntryPointNodeId} does not exist in workflow", workflow.EntryPointNodeId);
                result.Errors.Add($"Entry point node '{workflow.EntryPointNodeId}' does not exist in the workflow.");
            }
        }
    }

    /// <summary>
    /// Validates node-specific configuration based on RuntimeType.
    /// </summary>
    /// <param name="nodeDefinition">The node definition to validate.</param>
    /// <param name="result">The validation result to populate.</param>
    private void ValidateNodeConfiguration(NodeDefinition nodeDefinition, ValidationResult result)
    {
        this.logger.LogDebug("Validating configuration for node {NodeId} (Type: {RuntimeType})",
            nodeDefinition.NodeId, nodeDefinition.RuntimeType);

        var validationContext = new ValidationContext(nodeDefinition);
        var validationErrors = nodeDefinition.Validate(validationContext)
            .Where(vr => !string.IsNullOrEmpty(vr.ErrorMessage))
            .ToList();
        if (validationErrors.Any())
        {
            this.logger.LogError("Node {NodeId} has {ErrorCount} configuration errors",
                nodeDefinition.NodeId, validationErrors.Count);
            foreach (var validationError in validationErrors)
            {
                this.logger.LogError("Node {NodeId} validation error: {ErrorMessage}",
                    nodeDefinition.NodeId, validationError.ErrorMessage);
            }
            result.Errors.AddRange(validationErrors.Select(vr => vr.ErrorMessage!));
        }
    }

    /// <summary>
    /// Validates that connections reference valid nodes.
    /// </summary>
    private void ValidateConnections(WorkflowDefinition workflow, ValidationResult result)
    {
        this.logger.LogDebug("Validating workflow connections");

        if (workflow.Connections == null)
        {
            this.logger.LogDebug("No connections to validate");
            return;
        }

        this.logger.LogDebug("Validating {ConnectionCount} connections", workflow.Connections.Count);

        var nodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.NodeId));

        foreach (var connection in workflow.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.SourceNodeId))
            {
                this.logger.LogError("Connection found with null or empty SourceNodeId");
                result.Errors.Add("Connection found with null or empty SourceNodeId.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(connection.TargetNodeId))
            {
                this.logger.LogError("Connection found with null or empty TargetNodeId");
                result.Errors.Add("Connection found with null or empty TargetNodeId.");
                continue;
            }

            if (!nodeIds.Contains(connection.SourceNodeId))
            {
                this.logger.LogError("Connection references non-existent source node: {SourceNodeId}", connection.SourceNodeId);
                result.Errors.Add($"Connection references non-existent source node: '{connection.SourceNodeId}'.");
            }

            if (!nodeIds.Contains(connection.TargetNodeId))
            {
                this.logger.LogError("Connection references non-existent target node: {TargetNodeId}", connection.TargetNodeId);
                result.Errors.Add($"Connection references non-existent target node: '{connection.TargetNodeId}'.");
            }

            if (connection.SourceNodeId == connection.TargetNodeId)
            {
                this.logger.LogWarning("Self-referencing connection detected on node {NodeId}", connection.SourceNodeId);
                result.Warnings.Add($"Self-referencing connection detected on node '{connection.SourceNodeId}'. This may cause issues.");
            }
        }
    }

    /// <summary>
    /// Validates the graph structure (cycles, entry points).
    /// </summary>
    private void ValidateGraphStructure(WorkflowDefinition workflow, ValidationResult result)
    {
        this.logger.LogDebug("Validating workflow graph structure");

        if (workflow.Nodes == null || workflow.Nodes.Count == 0 || workflow.Connections == null)
        {
            this.logger.LogDebug("No graph structure to validate (no nodes or connections)");
            return;
        }

        var nodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.NodeId));

        // Build adjacency list for graph traversal
        var adjacencyList = new Dictionary<string, List<string>>();
        foreach (var nodeId in nodeIds)
        {
            adjacencyList[nodeId] = new List<string>();
        }

        foreach (var connection in workflow.Connections.Where(c => c.IsEnabled))
        {
            if (nodeIds.Contains(connection.SourceNodeId) && nodeIds.Contains(connection.TargetNodeId))
            {
                adjacencyList[connection.SourceNodeId].Add(connection.TargetNodeId);
            }
        }

        this.logger.LogDebug("Checking for cycles in workflow graph");

        // Check for cycles using DFS
        // Allow feedback loops for While nodes (child -> while connection for iteration control)
        var whileNodes = new HashSet<string>(
            workflow.Nodes.Where(n => n.RuntimeType == RuntimeType.While).Select(n => n.NodeId));

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var nodeId in nodeIds)
        {
            if (!visited.Contains(nodeId))
            {
                if (this.HasCycleDFS(nodeId, adjacencyList, visited, recursionStack, whileNodes, workflow.Connections))
                {
                    this.logger.LogError("Cycle detected in workflow graph involving node {NodeId}", nodeId);
                    result.Errors.Add($"Cycle detected in workflow graph involving node '{nodeId}'. This will cause infinite loops.");
                }
            }
        }

        // Check for at least one entry point
        this.logger.LogDebug("Validating workflow entry points");

        var nodesWithIncoming = new HashSet<string>(
            workflow.Connections.Where(c => c.IsEnabled).Select(c => c.TargetNodeId));

        var entryPoints = nodeIds.Except(nodesWithIncoming).ToList();

        if (string.IsNullOrWhiteSpace(workflow.EntryPointNodeId))
        {
            if (entryPoints.Count == 0)
            {
                this.logger.LogError("Workflow has no entry points - all nodes have incoming connections");
                result.Errors.Add("Workflow has no entry points. All nodes have incoming connections, which may prevent execution from starting.");
            }
            else if (entryPoints.Count > 1)
            {
                this.logger.LogWarning("Workflow has {EntryPointCount} entry points: {EntryPoints}",
                    entryPoints.Count, string.Join(", ", entryPoints));
                result.Warnings.Add($"Workflow has {entryPoints.Count} entry points: {string.Join(", ", entryPoints)}. Consider specifying an explicit EntryPointNodeId.");
            }
        }
    }

    /// <summary>
    /// Detects cycles in the graph using depth-first search.
    /// Allows feedback loops where a child sends Complete back to a While node parent.
    /// </summary>
    /// <param name="nodeId">Current node being visited.</param>
    /// <param name="adjacencyList">Graph adjacency list.</param>
    /// <param name="visited">Set of visited nodes.</param>
    /// <param name="recursionStack">Current recursion stack for cycle detection.</param>
    /// <param name="whileNodes">Set of While node IDs that can have feedback loops.</param>
    /// <param name="connections">All workflow connections for checking feedback loop validity.</param>
    /// <returns>True if a cycle is detected, false otherwise.</returns>
    private bool HasCycleDFS(
        string nodeId,
        Dictionary<string, List<string>> adjacencyList,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        HashSet<string> whileNodes,
        List<NodeConnection> connections)
    {
        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        foreach (var neighbor in adjacencyList[nodeId])
        {
            if (!visited.Contains(neighbor))
            {
                if (this.HasCycleDFS(neighbor, adjacencyList, visited, recursionStack, whileNodes, connections))
                {
                    return true;
                }
            }
            else if (recursionStack.Contains(neighbor))
            {
                // Cycle detected - check if it's an allowed feedback loop
                // Allow: child -> While node (Complete message triggering next iteration)
                if (whileNodes.Contains(neighbor))
                {
                    // Check if this is a feedback connection (Complete message from child to While)
                    var connection = connections.FirstOrDefault(c =>
                        c.SourceNodeId == nodeId &&
                        c.TargetNodeId == neighbor &&
                        c.TriggerMessageType == MessageType.Complete);

                    if (connection != null)
                    {
                        // This is an allowed feedback loop for While iteration control
                        continue;
                    }
                }

                return true; // Disallowed cycle detected
            }
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}

/// <summary>
/// Represents the result of a workflow validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; } = new List<string>();

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => this.Errors.Count == 0;
}
