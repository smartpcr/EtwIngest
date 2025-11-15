// -----------------------------------------------------------------------
// <copyright file="EnsureKustoTableNode.cs" company="Microsoft Corp.">
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
using EtwIngest.Libs;

/// <summary>
/// Node that ensures Kusto tables exist for CSV files.
/// Creates tables and ingestion mappings if they don't exist.
/// </summary>
public class EnsureKustoTableNode : ExecutableNodeBase
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

            // Create Kusto connection with database
            var kcsb = new KustoConnectionStringBuilder(this.ConnectionString)
            {
                InitialCatalog = this.DatabaseName
            };

            using var adminClient = KustoClientFactory.CreateCslAdminProvider(kcsb);

            int tablesCreated = 0;
            int tablesAlreadyExist = 0;
            var tableNames = new List<string>();

            foreach (var csvFile in csvFiles)
            {
                // Table name is the CSV file name without extension
                var tableName = Path.GetFileNameWithoutExtension(csvFile);
                tableNames.Add(tableName);

                // Check if table exists
                bool tableExists = adminClient.IsTableExist(tableName);

                if (!tableExists)
                {
                    // Read CSV header to get schema
                    var fields = this.ReadCsvSchema(csvFile);

                    // Generate and execute create table command
                    var createTableCommand = KustoExtension.GenerateCreateTableCommand(tableName, fields);
                    adminClient.ExecuteControlCommand(createTableCommand);

                    // Generate and execute CSV ingestion mapping
                    var csvMappingCommand = KustoExtension.GenerateCsvIngestionMapping(tableName, "CsvMapping", fields);
                    adminClient.ExecuteControlCommand(csvMappingCommand);

                    tablesCreated++;
                }
                else
                {
                    tablesAlreadyExist++;
                }
            }

            nodeContext.OutputData["TablesCreated"] = tablesCreated;
            nodeContext.OutputData["TablesAlreadyExist"] = tablesAlreadyExist;
            nodeContext.OutputData["TableNames"] = tableNames.ToArray();
            nodeContext.OutputData["TotalTables"] = tableNames.Count;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Ensured {tableNames.Count} tables ({tablesCreated} created, {tablesAlreadyExist} already exist)",
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

    /// <summary>
    /// Reads the CSV file header to determine schema.
    /// </summary>
    /// <param name="csvFilePath">Path to the CSV file.</param>
    /// <returns>List of field names and types.</returns>
    private List<(string fieldName, Type fieldType)> ReadCsvSchema(string csvFilePath)
    {
        var fields = new List<(string fieldName, Type fieldType)>();

        using (var reader = new StreamReader(csvFilePath))
        {
            // Read header line
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                throw new InvalidOperationException($"CSV file '{csvFilePath}' has no header.");
            }

            // Parse header (simple comma-separated, doesn't handle quoted commas)
            var columnNames = headerLine.Split(',');

            // Read first data line to infer types
            var dataLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(dataLine))
            {
                // No data, default all to string
                foreach (var columnName in columnNames)
                {
                    fields.Add((columnName.Trim(), typeof(string)));
                }
            }
            else
            {
                var values = dataLine.Split(',');
                for (int i = 0; i < columnNames.Length && i < values.Length; i++)
                {
                    var columnName = columnNames[i].Trim();
                    var value = values[i].Trim().Trim('"');

                    // Simple type inference
                    Type fieldType = typeof(string);
                    if (int.TryParse(value, out _))
                    {
                        fieldType = typeof(int);
                    }
                    else if (long.TryParse(value, out _))
                    {
                        fieldType = typeof(long);
                    }
                    else if (double.TryParse(value, out _))
                    {
                        fieldType = typeof(double);
                    }
                    else if (DateTime.TryParse(value, out _))
                    {
                        fieldType = typeof(DateTime);
                    }
                    else if (Guid.TryParse(value, out _))
                    {
                        fieldType = typeof(Guid);
                    }

                    fields.Add((columnName, fieldType));
                }
            }
        }

        return fields;
    }
}
