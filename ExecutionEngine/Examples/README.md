# ExecutionEngine Example Workflows

This directory contains example workflow definitions demonstrating various features of the ExecutionEngine.

## Available Examples

### 1. ETL Workflow with Kusto Database (`etl-workflow-with-kusto.yaml`)

A comprehensive ETL (Extract, Transform, Load) pipeline that demonstrates:

**Features Demonstrated:**
- **TimerNode**: Scheduled workflow execution (daily at 2 AM)
- **EnsureKustoDBNode**: Database provisioning and connection management
- **ForEachNode**: Parallel processing of multiple ETL files
- **CSharpScriptNode**: Inline C# scripting for custom logic
- **Conditional Routing**: Execute nodes only when conditions are met
- **Global Variables**: Shared state across the workflow
- **Data Flow**: Passing data between nodes via Input/Output contexts

**Workflow Steps:**
1. Timer triggers workflow daily at 2 AM
2. Ensures Kusto database exists (creates if missing)
3. Scans directory for ETL files
4. Processes each ETL file in parallel:
   - Parses ETL file to extract events and schemas
   - Creates Kusto tables for each event type
   - Extracts events to CSV format
   - Ingests CSV files into Kusto
   - Archives processed file
5. Generates summary report

**Prerequisites:**
- Kustainer running at `http://172.24.102.61:8080`
- Volume mount from `C:\kustodata` to `/kustodata` in Kustainer
- Source ETL files in `C:\logs\etl`
- EtwIngest library compiled with Kusto dependencies

**Configuration:**

Edit the `defaultVariables` section to match your environment:

```yaml
defaultVariables:
  kustoClusterUri: "http://172.24.102.61:8080"
  kustoDatabase: "EtwLogs"
  etlSourcePath: "C:\\logs\\etl"
  csvOutputPath: "C:\\logs\\csv"
  archivePath: "C:\\logs\\archive"
  volumeHostPath: "C:\\kustodata"
  volumeContainerPath: "/kustodata"
```

**Running the Workflow:**

```bash
# Load and validate the workflow
dotnet run --project WorkflowRunner -- load etl-workflow-with-kusto.yaml

# Execute the workflow manually (bypass timer)
dotnet run --project WorkflowRunner -- execute etl-workflow-with-kusto.yaml

# Run in continuous mode (timer-driven)
dotnet run --project WorkflowRunner -- watch etl-workflow-with-kusto.yaml
```

## EnsureKustoDBNode Reference

The `EnsureKustoDBNode` is a specialized node for managing Kusto database lifecycle.

### Configuration

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `KustoClusterUri` | string | Yes | - | Kusto cluster endpoint (e.g., "http://172.24.102.61:8080") |
| `DatabaseName` | string | Yes | - | Name of the database to ensure exists |
| `MetadataPersistPath` | string | No | `/kustodata/dbs/{dbName}/md` | Path for database metadata persistence |
| `DataPersistPath` | string | No | `/kustodata/dbs/{dbName}/data` | Path for database data persistence |
| `ConnectionTimeoutSeconds` | int | No | 10 | HTTP connection timeout in seconds |

### Output Data

The node provides the following outputs to downstream nodes:

| Output Key | Type | Description |
|------------|------|-------------|
| `ClusterAccessible` | bool | Whether the cluster was successfully contacted |
| `DatabaseName` | string | Name of the database |
| `DatabaseAlreadyExisted` | bool | Whether the database existed before this execution |
| `DatabaseCreated` | bool | Whether the database was created in this execution |
| `ConnectionString` | string | Kusto connection string for the database |
| `AdminClient` | ICslAdminProvider | Kusto admin client (for DDL operations) |
| `QueryClient` | ICslQueryProvider | Kusto query client (for queries) |
| `MetadataPersistPath` | string | Metadata persistence path used |
| `DataPersistPath` | string | Data persistence path used |
| `KustoClusterUri` | string | Cluster URI used |

### Global Variables Set

The node also sets the following workflow-level global variables:

- `KustoAdminClient`: Admin client for downstream nodes
- `KustoQueryClient`: Query client for downstream nodes
- `KustoDatabaseName`: Database name
- `KustoClusterUri`: Cluster URI

### Example Usage

#### YAML Configuration

```yaml
nodes:
  - nodeId: setup-kusto
    nodeName: Setup Kusto Database
    runtimeType: CSharp
    assemblyPath: ./ExecutionEngine.dll
    typeName: ExecutionEngine.Nodes.EnsureKustoDBNode
    configuration:
      KustoClusterUri: "http://172.24.102.61:8080"
      DatabaseName: "MyDatabase"
      ConnectionTimeoutSeconds: 10
```

#### C# Programmatic Usage

```csharp
using ExecutionEngine.Nodes;
using ExecutionEngine.Factory;
using ExecutionEngine.Contexts;

var node = new EnsureKustoDBNode();
node.Initialize(new NodeDefinition
{
    NodeId = "kusto-setup",
    NodeName = "Ensure Database",
    Configuration = new Dictionary<string, object>
    {
        { "KustoClusterUri", "http://172.24.102.61:8080" },
        { "DatabaseName", "EtwLogs" }
    }
});

var workflowContext = new WorkflowExecutionContext { GraphId = "etl-pipeline" };
var nodeContext = new NodeExecutionContext();

var instance = await node.ExecuteAsync(workflowContext, nodeContext, CancellationToken.None);

if (instance.Status == NodeExecutionStatus.Completed)
{
    var adminClient = nodeContext.OutputData["AdminClient"] as ICslAdminProvider;
    var queryClient = nodeContext.OutputData["QueryClient"] as ICslQueryProvider;

    // Use clients for Kusto operations
}
```

### Error Handling

The node handles the following error scenarios:

| Error | Behavior |
|-------|----------|
| Missing `KustoClusterUri` | Fails with `InvalidOperationException` |
| Missing `DatabaseName` | Fails with `InvalidOperationException` |
| Cluster not accessible | Fails with connection error message |
| Invalid cron expression | Fails during `Initialize()` |
| Kusto command errors | Propagates exception from Kusto client |

### Best Practices

1. **Connection Pooling**: The node creates client instances that can be reused by downstream nodes via global variables
2. **Idempotency**: Safe to run multiple times - checks database existence before creation
3. **Early Validation**: Place this node early in the workflow to fail fast if Kusto is unavailable
4. **Error Handling**: Use `OnFail` connections to handle database setup failures gracefully
5. **Timeout Configuration**: Adjust `ConnectionTimeoutSeconds` based on network latency

### Integration with EtwIngest

The node is designed to work seamlessly with the EtwIngest library:

```yaml
# Step 1: Ensure database
- nodeId: ensure-db
  runtimeType: CSharp
  typeName: ExecutionEngine.Nodes.EnsureKustoDBNode
  configuration:
    KustoClusterUri: "http://172.24.102.61:8080"
    DatabaseName: "EtwLogs"

# Step 2: Parse ETL file using EtwIngest library
- nodeId: parse-etl
  runtimeType: CSharpScript
  configuration:
    script: |
      using EtwIngest.Libs;
      var etl = new EtlFile(GetGlobal("etlFilePath"));
      var events = new ConcurrentDictionary<(string, string), EtwEvent>();
      bool failed = false;
      etl.Parse(events, ref failed);
      SetOutput("events", events);

# Step 3: Create Kusto tables using admin client from Step 1
- nodeId: create-tables
  runtimeType: CSharpScript
  configuration:
    script: |
      using EtwIngest.Libs;
      var adminClient = GetGlobal("KustoAdminClient");
      var events = GetInput("events");

      foreach (var kvp in events)
      {
          var tableName = $"ETL-{kvp.Key.Item1}.{kvp.Key.Item2}";
          if (!adminClient.IsTableExist(tableName))
          {
              var cmd = KustoExtension.GenerateCreateTableCommand(tableName, kvp.Value.PayloadSchema);
              adminClient.ExecuteControlCommand(cmd);
          }
      }
```

## See Also

- [ExecutionEngine Design Document](../design.md)
- [Implementation Guide](../implementation.md)
- [Node Reference Documentation](../Nodes/README.md)
- [Testing Guide](../README.md#testing)
