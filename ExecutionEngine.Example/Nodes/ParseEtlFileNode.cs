// -----------------------------------------------------------------------
// <copyright file="ParseEtlFileNode.cs" company="Microsoft Corp.">
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
/// Node that parses an ETL/EVTX file and generates batch CSV files.
/// Uses ScalableEventProcessor for efficient processing.
/// </summary>
public class ParseEtlFileNode : ExecutableNodeBase
{
    /// <summary>
    /// Gets or sets the output directory for CSV files.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the batch size for file batching.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get output directory from configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("OutputDirectory", out var outputDirValue))
        {
            this.OutputDirectory = outputDirValue?.ToString() ?? string.Empty;
        }

        // Get batch size from configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("BatchSize", out var batchSizeValue))
        {
            if (int.TryParse(batchSizeValue?.ToString(), out var batchSize))
            {
                this.BatchSize = batchSize;
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

            // Get the ETL file path from input data
            if (!nodeContext.InputData.TryGetValue("etlFile", out var etlFileObj) && 
                !nodeContext.InputData.TryGetValue("EtlFile", out etlFileObj))
            {
                throw new InvalidOperationException("ETL file path not found in input data.");
            }

            var etlFilePath = etlFileObj?.ToString() ?? throw new InvalidOperationException("ETL file path is null.");

            if (string.IsNullOrWhiteSpace(this.OutputDirectory))
            {
                // Default to same directory as ETL file
                this.OutputDirectory = Path.GetDirectoryName(etlFilePath) ?? Environment.CurrentDirectory;
            }

            // Create output directory if it doesn't exist
            if (!Directory.Exists(this.OutputDirectory))
            {
                Directory.CreateDirectory(this.OutputDirectory);
            }

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Processing {Path.GetFileName(etlFilePath)}...",
                ProgressPercent = 10
            });

            // Use ScalableEventProcessor to process the file
            var processor = new ScalableEventProcessor(this.OutputDirectory, maxDegreeOfParallelism: 1);
            var summary = processor.ProcessFiles(new[] { etlFilePath });

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = "Processing complete, finding generated CSV files...",
                ProgressPercent = 80
            });

            // Find all generated CSV files
            var csvFiles = Directory.GetFiles(this.OutputDirectory, "*.csv", SearchOption.TopDirectoryOnly);

            // Group CSV files into batches
            var batchFiles = new List<string[]>();
            for (int i = 0; i < csvFiles.Length; i += this.BatchSize)
            {
                var batch = csvFiles.Skip(i).Take(this.BatchSize).ToArray();
                batchFiles.Add(batch);
            }

            // Set output data
            nodeContext.OutputData["CsvFiles"] = csvFiles;
            nodeContext.OutputData["BatchedCsvFiles"] = batchFiles.ToArray();
            nodeContext.OutputData["TotalCsvFiles"] = csvFiles.Length;
            nodeContext.OutputData["TotalBatches"] = batchFiles.Count;
            nodeContext.OutputData["OutputDirectory"] = this.OutputDirectory;
            nodeContext.OutputData["TotalEvents"] = summary.TotalEvents;
            nodeContext.OutputData["ProcessedAt"] = DateTime.UtcNow;

            // Also set in workflow global variables
            workflowContext.Variables["csvFiles"] = csvFiles;
            workflowContext.Variables["batchedCsvFiles"] = batchFiles.ToArray();
            workflowContext.Variables["outputDirectory"] = this.OutputDirectory;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Processed {summary.TotalEvents:N0} events into {csvFiles.Length} CSV files ({batchFiles.Count} batches)",
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
