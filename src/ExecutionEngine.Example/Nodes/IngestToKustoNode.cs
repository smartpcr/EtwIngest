// -----------------------------------------------------------------------
// <copyright file="IngestToKustoNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Factory;
using ExecutionEngine.Nodes;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

/// <summary>
/// Node that ingests CSV files into Kusto tables.
/// </summary>
public class IngestToKustoNode : ExecutableNodeBase
{
    /// <summary>
    /// Gets or sets the Kusto connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override void Initialize(NodeDefinition definition)
    {
        base.Initialize(definition);

        // Get connection string from configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("ConnectionString", out var connStrValue))
        {
            this.ConnectionString = connStrValue?.ToString() ?? string.Empty;
        }

        // Get database name from configuration
        if (definition.Configuration != null && definition.Configuration.TryGetValue("DatabaseName", out var dbNameValue))
        {
            this.DatabaseName = dbNameValue?.ToString() ?? string.Empty;
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

            if (string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                // Try to get from global variables
                if (workflowContext.Variables.TryGetValue("kustoConnectionString", out var connStrObj))
                {
                    this.ConnectionString = connStrObj?.ToString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(this.DatabaseName))
            {
                // Try to get from global variables
                if (workflowContext.Variables.TryGetValue("kustoDatabaseName", out var dbNameObj))
                {
                    this.DatabaseName = dbNameObj?.ToString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(this.ConnectionString))
            {
                throw new InvalidOperationException("Kusto connection string is not defined.");
            }

            if (string.IsNullOrWhiteSpace(this.DatabaseName))
            {
                throw new InvalidOperationException("Database name is not defined.");
            }

            // Get CSV files from input data
            if (!nodeContext.InputData.TryGetValue("CsvFiles", out var csvFilesObj) &&
                !nodeContext.InputData.TryGetValue("csvFiles", out csvFilesObj))
            {
                throw new InvalidOperationException("CSV files not found in input data.");
            }

            var csvFiles = csvFilesObj as string[] ?? Array.Empty<string>();

            // Create Kusto admin client
            var kcsb = new KustoConnectionStringBuilder(this.ConnectionString)
            {
                InitialCatalog = this.DatabaseName
            };

            using var adminClient = KustoClientFactory.CreateCslAdminProvider(kcsb);

            var filesIngested = 0;
            long totalRowsIngested = 0;

            foreach (var csvFile in csvFiles)
            {
                // Table name is the CSV file name without extension
                var tableName = Path.GetFileNameWithoutExtension(csvFile);

                // Use .ingest control command for ingestion
                var ingestCommand = $".ingest into table ['{tableName}'] (\"{csvFile}\") with (format='csv', ingestionMappingReference='CsvMapping', ignoreFirstRecord=true)";
                adminClient.ExecuteControlCommand(ingestCommand);

                filesIngested++;

                this.RaiseOnProgress(new ProgressEventArgs
                {                    Status = $"Ingested {filesIngested}/{csvFiles.Length} files",
                    ProgressPercent = (int)((filesIngested / (double)csvFiles.Length) * 100)
                });
            }

            nodeContext.OutputData["FilesIngested"] = filesIngested;
            nodeContext.OutputData["TotalRowsIngested"] = totalRowsIngested;
            nodeContext.OutputData["IngestedAt"] = DateTime.UtcNow;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Ingested {filesIngested} files into Kusto",
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
