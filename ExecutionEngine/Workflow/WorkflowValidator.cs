// -----------------------------------------------------------------------
// <copyright file="WorkflowValidator.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Workflow;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates workflow definitions to ensure they are well-formed and executable.
/// </summary>
public class WorkflowValidator
{
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

        return result;
    }

    /// <summary>
    /// Validates basic workflow properties.
    /// </summary>
    private void ValidateBasicProperties(WorkflowDefinition workflow, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(workflow.WorkflowId))
        {
            result.Errors.Add("WorkflowId cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(workflow.WorkflowName))
        {
            result.Warnings.Add("WorkflowName is empty. Consider providing a descriptive name.");
        }

        if (workflow.MaxConcurrency < 0)
        {
            result.Errors.Add("MaxConcurrency cannot be negative.");
        }

        if (workflow.TimeoutSeconds < 0)
        {
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
            result.Errors.Add("Workflow must contain at least one node.");
            return;
        }

        // Check for duplicate node IDs
        var nodeIds = new HashSet<string>();
        foreach (var node in workflow.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                result.Errors.Add("Node found with null or empty NodeId.");
                continue;
            }

            if (!nodeIds.Add(node.NodeId))
            {
                result.Errors.Add($"Duplicate node ID found: '{node.NodeId}'.");
            }
        }

        // Validate entry point if specified
        if (!string.IsNullOrWhiteSpace(workflow.EntryPointNodeId))
        {
            if (!nodeIds.Contains(workflow.EntryPointNodeId))
            {
                result.Errors.Add($"Entry point node '{workflow.EntryPointNodeId}' does not exist in the workflow.");
            }
        }
    }

    /// <summary>
    /// Validates that connections reference valid nodes.
    /// </summary>
    private void ValidateConnections(WorkflowDefinition workflow, ValidationResult result)
    {
        if (workflow.Connections == null)
        {
            return;
        }

        var nodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.NodeId));

        foreach (var connection in workflow.Connections)
        {
            if (string.IsNullOrWhiteSpace(connection.SourceNodeId))
            {
                result.Errors.Add("Connection found with null or empty SourceNodeId.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(connection.TargetNodeId))
            {
                result.Errors.Add("Connection found with null or empty TargetNodeId.");
                continue;
            }

            if (!nodeIds.Contains(connection.SourceNodeId))
            {
                result.Errors.Add($"Connection references non-existent source node: '{connection.SourceNodeId}'.");
            }

            if (!nodeIds.Contains(connection.TargetNodeId))
            {
                result.Errors.Add($"Connection references non-existent target node: '{connection.TargetNodeId}'.");
            }

            if (connection.SourceNodeId == connection.TargetNodeId)
            {
                result.Warnings.Add($"Self-referencing connection detected on node '{connection.SourceNodeId}'. This may cause issues.");
            }
        }
    }

    /// <summary>
    /// Validates the graph structure (cycles, entry points).
    /// </summary>
    private void ValidateGraphStructure(WorkflowDefinition workflow, ValidationResult result)
    {
        if (workflow.Nodes == null || workflow.Nodes.Count == 0 || workflow.Connections == null)
        {
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

        // Check for cycles using DFS
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var nodeId in nodeIds)
        {
            if (!visited.Contains(nodeId))
            {
                if (this.HasCycleDFS(nodeId, adjacencyList, visited, recursionStack))
                {
                    result.Errors.Add($"Cycle detected in workflow graph involving node '{nodeId}'. This will cause infinite loops.");
                }
            }
        }

        // Check for at least one entry point
        var nodesWithIncoming = new HashSet<string>(
            workflow.Connections.Where(c => c.IsEnabled).Select(c => c.TargetNodeId));

        var entryPoints = nodeIds.Except(nodesWithIncoming).ToList();

        if (string.IsNullOrWhiteSpace(workflow.EntryPointNodeId))
        {
            if (entryPoints.Count == 0)
            {
                result.Errors.Add("Workflow has no entry points. All nodes have incoming connections, which may prevent execution from starting.");
            }
            else if (entryPoints.Count > 1)
            {
                result.Warnings.Add($"Workflow has {entryPoints.Count} entry points: {string.Join(", ", entryPoints)}. Consider specifying an explicit EntryPointNodeId.");
            }
        }
    }

    /// <summary>
    /// Detects cycles in the graph using depth-first search.
    /// </summary>
    /// <param name="nodeId">Current node being visited.</param>
    /// <param name="adjacencyList">Graph adjacency list.</param>
    /// <param name="visited">Set of visited nodes.</param>
    /// <param name="recursionStack">Current recursion stack for cycle detection.</param>
    /// <returns>True if a cycle is detected, false otherwise.</returns>
    private bool HasCycleDFS(
        string nodeId,
        Dictionary<string, List<string>> adjacencyList,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        foreach (var neighbor in adjacencyList[nodeId])
        {
            if (!visited.Contains(neighbor))
            {
                if (this.HasCycleDFS(neighbor, adjacencyList, visited, recursionStack))
                {
                    return true;
                }
            }
            else if (recursionStack.Contains(neighbor))
            {
                return true; // Cycle detected
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
