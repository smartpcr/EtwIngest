// -----------------------------------------------------------------------
// <copyright file="DiscoverEtlFilesNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using EtwEventReader.Tools;

/// <summary>
/// Node that discovers ETL/EVTX files using EventFileHandler.
/// Supports wildcards, directories, and ZIP file extraction.
/// </summary>
public class DiscoverEtlFilesNode : ExecutableNodeBase
{
    /// <summary>
    /// Gets or sets the paths to search for ETL files (supports wildcards, directories, ZIP files).
    /// </summary>
    public string[] SearchPaths { get; set; } = Array.Empty<string>();

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get search paths from configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("SearchPaths", out var pathsValue))
        {
            if (pathsValue is string[] paths)
            {
                this.SearchPaths = paths;
            }
            else if (pathsValue is string singlePath)
            {
                this.SearchPaths = new[] { singlePath };
            }
        }
    }

    /// <inheritdoc/>
    public override async Task<NodeInstance> ExecuteAsync(
        WorkflowExecutionContext workflowContext,
        NodeExecutionContext nodeContext,
        CancellationToken cancellationToken)
    {
        var instance = new NodeInstance
        {
            NodeInstanceId = Guid.NewGuid(),            WorkflowInstanceId = workflowContext.InstanceId,
            Status = NodeExecutionStatus.Running,
            StartTime = DateTime.UtcNow,
            ExecutionContext = nodeContext
        };

        try
        {
            this.RaiseOnStart(new NodeStartEventArgs
            {                Timestamp = DateTime.UtcNow
            });

            if (this.SearchPaths == null || this.SearchPaths.Length == 0)
            {
                throw new InvalidOperationException("SearchPaths are not defined.");
            }

            // Use EventFileHandler to resolve all paths
            using var eventFileHandler = new EventFileHandler();
            var resolvedFiles = eventFileHandler.ResolveAllPaths(this.SearchPaths);

            // Set output data
            nodeContext.OutputData["EtlFiles"] = resolvedFiles.ToArray();
            nodeContext.OutputData["TotalFiles"] = resolvedFiles.Count;
            nodeContext.OutputData["DiscoveredAt"] = DateTime.UtcNow;

            // Also set in workflow global variables for backward compatibility
            workflowContext.Variables["etlFiles"] = resolvedFiles.ToArray();
            workflowContext.Variables["totalFiles"] = resolvedFiles.Count;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Discovered {resolvedFiles.Count} files",
                ProgressPercent = 100
            });
        }
        catch (Exception ex)
        {
            instance.Status = NodeExecutionStatus.Failed;
            instance.EndTime = DateTime.UtcNow;
            instance.ErrorMessage = ex.Message;
            instance.Exception = ex;
        }

        return await Task.FromResult(instance);
    }
}
