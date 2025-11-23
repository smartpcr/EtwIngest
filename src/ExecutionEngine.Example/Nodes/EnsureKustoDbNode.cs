// -----------------------------------------------------------------------
// <copyright file="EnsureKustoDbNode.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace ExecutionEngine.Example.Nodes;

using ExecutionEngine.Contexts;
using ExecutionEngine.Core;
using ExecutionEngine.Enums;
using ExecutionEngine.Nodes;
using ExecutionEngine.Nodes.Definitions;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;

/// <summary>
/// Node that ensures a Kusto database exists.
/// Creates the database if it doesn't exist.
/// </summary>
public class EnsureKustoDbNode : ExecutableNodeBase
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
        this.Definition = definition;

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
                throw new InvalidOperationException("Kusto connection string is not defined.");
            }

            if (string.IsNullOrWhiteSpace(this.DatabaseName))
            {
                throw new InvalidOperationException("Database name is not defined.");
            }

            // Create Kusto connection
            var kcsb = new KustoConnectionStringBuilder(this.ConnectionString);
            using var adminClient = KustoClientFactory.CreateCslAdminProvider(kcsb);

            // Check if database exists
            var showDatabasesCommand = ".show databases";
            var databaseExists = false;

            using (var result = adminClient.ExecuteControlCommand(showDatabasesCommand))
            {
                while (result.Read())
                {
                    if (result.GetString(0) == this.DatabaseName)
                    {
                        databaseExists = true;
                        break;
                    }
                }
            }

            if (!databaseExists)
            {
                // Create database
                var createDbCommand = $".create database ['{this.DatabaseName}']";
                adminClient.ExecuteControlCommand(createDbCommand);

                nodeContext.OutputData["DatabaseCreated"] = true;
                nodeContext.OutputData["Action"] = "Created";
            }
            else
            {
                nodeContext.OutputData["DatabaseCreated"] = false;
                nodeContext.OutputData["Action"] = "AlreadyExists";
            }

            nodeContext.OutputData["DatabaseName"] = this.DatabaseName;
            nodeContext.OutputData["DatabaseExists"] = true;

            // Set in workflow global variables
            workflowContext.Variables["kustoDatabaseName"] = this.DatabaseName;
            workflowContext.Variables["kustoConnectionString"] = this.ConnectionString;

            instance.Status = NodeExecutionStatus.Completed;
            instance.EndTime = DateTime.UtcNow;

            this.RaiseOnProgress(new ProgressEventArgs
            {                Status = $"Database '{this.DatabaseName}' is ready",
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
