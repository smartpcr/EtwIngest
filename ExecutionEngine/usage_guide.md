## 8. Integration with EtwIngest

### 8.1 Example Workflow: ETL Processing

```yaml
graphId: etl-processing-workflow
name: ETL Processing Workflow
description: Automated ETL file processing and Kusto ingestion

nodes:
  - nodeId: timer-daily
    nodeName: Daily Trigger
    type: Timer
    configuration:
      schedule: "0 2 * * *"  # 2 AM daily

  - nodeId: task-discover-files
    nodeName: Discover ETL Files
    type: Task
    configuration:
      language: CSharp
      script: |
        var directory = context.Variables["etlDirectory"].ToString();
        var files = Directory.GetFiles(directory, "*.etl", SearchOption.AllDirectories);
        context.Variables["etlFiles"] = files;
        context.Variables["totalFiles"] = files.Length;
        return new { fileCount = files.Length };

  - nodeId: foreach-process-file
    nodeName: Process Each File
    type: ForEach
    configuration:
      collectionExpression: context.Variables["etlFiles"]
      itemVariableName: etlFile

  - nodeId: task-parse-etl
    nodeName: Parse ETL File
    type: Task
    configuration:
      language: CSharp
      script: |
        var etlFilePath = context.Variables["etlFile"].ToString();
        var etlFile = new EtlFile(etlFilePath);

        await etlFile.Parse();

        context.Variables["eventSchemas"] = etlFile.EventSchemas;
        context.Variables["csvPath"] = Path.ChangeExtension(etlFilePath, ".csv");

        return new { schemaCount = etlFile.EventSchemas.Count };

  - nodeId: task-create-kusto-tables
    nodeName: Create Kusto Tables
    type: Task
    configuration:
      language: CSharp
      script: |
        var schemas = context.Variables["eventSchemas"] as Dictionary<string, EventSchema>;
        var kustoClient = new KustoClient(context.Variables["kustoConnectionString"].ToString());

        foreach (var schema in schemas.Values)
        {
            var tableName = $"ETL-{schema.ProviderName}.{schema.EventName}";
            await kustoClient.CreateTableIfNotExistsAsync(tableName, schema);
        }

  - nodeId: task-export-csv
    nodeName: Export to CSV
    type: Task
    configuration:
      language: CSharp
      script: |
        var etlFilePath = context.Variables["etlFile"].ToString();
        var csvPath = context.Variables["csvPath"].ToString();
        var etlFile = new EtlFile(etlFilePath);

        await etlFile.Process(csvPath);

  - nodeId: task-ingest-kusto
    nodeName: Ingest to Kusto
    type: Task
    configuration:
      language: CSharp
      script: |
        var csvPath = context.Variables["csvPath"].ToString();
        var kustoClient = new KustoClient(context.Variables["kustoConnectionString"].ToString());

        // Ingest CSV files
        var csvFiles = Directory.GetFiles(Path.GetDirectoryName(csvPath), "*.csv");
        foreach (var csv in csvFiles)
        {
            var tableName = Path.GetFileNameWithoutExtension(csv);
            await kustoClient.IngestFromCsvAsync(tableName, csv);
        }

  - nodeId: task-cleanup
    nodeName: Cleanup Temp Files
    type: Task
    configuration:
      language: CSharp
      script: |
        var csvPath = context.Variables["csvPath"].ToString();
        var csvFiles = Directory.GetFiles(Path.GetDirectoryName(csvPath), "*.csv");

        foreach (var csv in csvFiles)
        {
            File.Delete(csv);
        }

  - nodeId: task-error-handler
    nodeName: Handle Processing Error
    type: Task
    configuration:
      language: CSharp
      script: |
        var error = context.Variables["lastError"].ToString();
        Console.WriteLine($"Error processing ETL file: {error}");
        // Log to monitoring system, send alert, etc.

edges:
  - edgeId: edge-1
    sourceNodeId: timer-daily
    targetNodeId: task-discover-files
    type: OnComplete

  - edgeId: edge-2
    sourceNodeId: task-discover-files
    targetNodeId: foreach-process-file
    type: OnComplete

  - edgeId: edge-3
    sourceNodeId: foreach-process-file
    targetNodeId: task-parse-etl
    type: LoopBody

  - edgeId: edge-4
    sourceNodeId: task-parse-etl
    targetNodeId: task-create-kusto-tables
    type: OnComplete

  - edgeId: edge-5
    sourceNodeId: task-create-kusto-tables
    targetNodeId: task-export-csv
    type: OnComplete

  - edgeId: edge-6
    sourceNodeId: task-export-csv
    targetNodeId: task-ingest-kusto
    type: OnComplete

  - edgeId: edge-7
    sourceNodeId: task-ingest-kusto
    targetNodeId: task-cleanup
    type: OnComplete

  - edgeId: edge-error-1
    sourceNodeId: task-parse-etl
    targetNodeId: task-error-handler
    type: OnFail

  - edgeId: edge-error-2
    sourceNodeId: task-ingest-kusto
    targetNodeId: task-error-handler
    type: OnFail

defaultVariables:
  etlDirectory: "C:\\logs\\etl"
  kustoConnectionString: "Data Source=http://172.24.102.61:8080;Initial Catalog=etldata"
```

### 8.2 NodeExecutionContext Flow Example

This example demonstrates how NodeExecutionContext flows between nodes in a data processing pipeline:

```yaml
graphId: context-flow-demo
name: NodeExecutionContext Flow Demonstration
description: Shows how data flows between nodes via NodeExecutionContext

nodes:
  - nodeId: node-a
    nodeName: Data Generator
    type: Task
    configuration:
      language: CSharp
      script: |
        // This is the first node - Input will be empty
        Console.WriteLine($"Node A: Input has {Input.Count} items");

        // Generate data and set output
        SetOutput("fileList", new[] { "file1.txt", "file2.txt", "file3.txt" });
        SetOutput("generatedCount", 3);
        SetOutput("timestamp", DateTime.UtcNow);

        // Use local variables for internal state
        Local["processedBy"] = "Node A";

        return new Dictionary<string, object>
        {
            { "status", "success" }
        };

  - nodeId: node-b
    nodeName: Data Transformer
    type: Task
    configuration:
      language: CSharp
      script: |
        // Node B receives Node A's output as input
        Console.WriteLine($"Node B: Input has {Input.Count} items");

        var files = (string[])Input["fileList"];
        var count = (int)Input["generatedCount"];
        var timestamp = (DateTime)Input["timestamp"];

        Console.WriteLine($"Node B: Processing {count} files from {timestamp}");

        // Transform the data
        var transformedFiles = files.Select(f => f.ToUpper()).ToArray();

        // Set output for next node
        SetOutput("transformedFiles", transformedFiles);
        SetOutput("originalCount", count);
        SetOutput("transformedCount", transformedFiles.Length);

        // Local variable stays in this node only
        Local["transformationType"] = "uppercase";

  - nodeId: node-c
    nodeName: Data Validator
    type: Task
    configuration:
      language: CSharp
      script: |
        // Node C receives Node B's output as input
        Console.WriteLine($"Node C: Input has {Input.Count} items");

        var transformedFiles = (string[])Input["transformedFiles"];
        var originalCount = (int)Input["originalCount"];
        var transformedCount = (int)Input["transformedCount"];

        // Validate
        var isValid = transformedCount == originalCount;

        SetOutput("validationResult", isValid);
        SetOutput("finalFiles", transformedFiles);
        SetOutput("validatedAt", DateTime.UtcNow);

        Console.WriteLine($"Validation: {isValid}");

        return new Dictionary<string, object>
        {
            { "validationPassed", isValid }
        };

edges:
  - edgeId: edge-1
    sourceNodeId: node-a
    targetNodeId: node-b
    type: OnComplete

  - edgeId: edge-2
    sourceNodeId: node-b
    targetNodeId: node-c
    type: OnComplete
```

**Execution Flow:**

```
┌──────────────────────────────────────────────────────────────────┐
│ Node A Execution                                                 │
│ ┌──────────────────────────────────────────────────────────────┐ │
│ │ NodeExecutionContext                                         │ │
│ │ InputData: {}                                                │ │
│ │ LocalVariables: { "processedBy": "Node A" }                  │ │
│ │ OutputData: {                                                │ │
│ │   "fileList": ["file1.txt", "file2.txt", "file3.txt"],       │ │
│ │   "generatedCount": 3,                                       │ │
│ │   "timestamp": "2025-01-01T10:00:00Z",                       │ │
│ │   "status": "success"                                        │ │
│ │ }                                                            │ │
│ └──────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ NodeCompleteMessage
                              │ (contains NodeExecutionContext)
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ Node B Execution                                                 │
│ ┌──────────────────────────────────────────────────────────────┐ │
│ │ NodeExecutionContext                                         │ │
│ │ InputData: {  ← Node A's OutputData                          │ │
│ │   "fileList": ["file1.txt", "file2.txt", "file3.txt"],       │ │
│ │   "generatedCount": 3,                                       │ │
│ │   "timestamp": "2025-01-01T10:00:00Z",                       │ │
│ │   "status": "success"                                        │ │
│ │ }                                                            │ │
│ │ LocalVariables: { "transformationType": "uppercase" }        │ │
│ │ OutputData: {                                                │ │
│ │   "transformedFiles": ["FILE1.TXT", "FILE2.TXT", ...],       │ │
│ │   "originalCount": 3,                                        │ │
│ │   "transformedCount": 3                                      │ │
│ │ }                                                            │ │
│ └──────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ NodeCompleteMessage
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ Node C Execution                                                 │
│ ┌──────────────────────────────────────────────────────────────┐ │
│ │ NodeExecutionContext                                         │ │
│ │ InputData: {  ← Node B's OutputData                          │ │
│ │   "transformedFiles": ["FILE1.TXT", "FILE2.TXT", ...],       │ │
│ │   "originalCount": 3,                                        │ │
│ │   "transformedCount": 3                                      │ │
│ │ }                                                            │ │
│ │ LocalVariables: {}                                           │ │
│ │ OutputData: {                                                │ │
│ │   "validationResult": true,                                  │ │
│ │   "finalFiles": ["FILE1.TXT", "FILE2.TXT", ...],             │ │
│ │   "validatedAt": "2025-01-01T10:00:01Z",                     │ │
│ │   "validationPassed": true                                   │ │
│ │ }                                                            │ │
│ └──────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

**Key Observations:**

1. **Data Isolation**: Each node has its own `LocalVariables` - Node B's "transformationType" is not visible to Node C
2. **Pipeline Pattern**: Output from one node becomes input to the next
3. **Clean Contracts**: Each node clearly defines what it produces via `OutputData`
4. **No Global Pollution**: Data flows through explicit channels, not global variables
5. **Testability**: Each node can be tested independently by providing mock `InputData`

**Inspecting Node Instances:**

After workflow completion, you can inspect the execution history:


**Output:**
```
Node: node-a
  Started: 2025-01-01 10:00:00
  Duration: 00:00:00.1234567
  Status: Completed
  Input:
  Output:
    fileList = System.String[]
    generatedCount = 3
    timestamp = 1/1/2025 10:00:00 AM
    status = success

Node: node-b
  Started: 2025-01-01 10:00:00
  Duration: 00:00:00.0987654
  Status: Completed
  Input:
    fileList = System.String[]
    generatedCount = 3
    timestamp = 1/1/2025 10:00:00 AM
    status = success
  Output:
    transformedFiles = System.String[]
    originalCount = 3
    transformedCount = 3

Node: node-c
  Started: 2025-01-01 10:00:00
  Duration: 00:00:00.0543210
  Status: Completed
  Input:
    transformedFiles = System.String[]
    originalCount = 3
    transformedCount = 3
  Output:
    validationResult = True
    finalFiles = System.String[]
    validatedAt = 1/1/2025 10:00:01 AM
    validationPassed = True
```

## 11. Future Enhancements

1. **Distributed Execution**: Support for distributed workflow execution across multiple machines
2. **Retry Policies**: Automatic retry with exponential backoff for failed nodes
3. **Monitoring Dashboard**: Real-time visualization of workflow execution
4. **Workflow Versioning**: Support for versioning and migrating workflow definitions
5. **SLA Tracking**: Monitor and alert on workflow SLA violations
6. **Dynamic Graph Modification**: Modify graphs while workflows are running
7. **Human-in-the-Loop**: Support for manual approval nodes
8. **External Triggers**: Webhooks, message queues, file watchers
9. **Resource Quotas**: Limit CPU, memory, and execution time per workflow
10. **Workflow Templates**: Reusable workflow templates with parameterization

## 12. Conclusion

This design provides a robust, extensible execution engine for orchestrating complex workflows. The architecture emphasizes:

- **Flexibility**: Support for multiple execution languages and control flow patterns
- **Reliability**: State persistence and error handling
- **Observability**: Event-driven architecture with progress tracking
- **Maintainability**: Clean separation of concerns and testable components

The implementation can be integrated into the EtwIngest solution to provide automated, scheduled processing of ETL files with full monitoring and error recovery capabilities.
