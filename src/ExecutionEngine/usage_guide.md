## 8. Integration with EtwIngest

### 8.1 Example Workflow: ETL Processing

This example demonstrates an automated ETL processing workflow using custom nodes from ExecutionEngine.Example:

```yaml
graphId: etl-processing-workflow
name: ETL Processing Workflow
description: Automated ETL file processing and Kusto ingestion using custom nodes

nodes:
  # Trigger: Daily at 2 AM
  - nodeId: timer-daily
    nodeName: Daily Trigger
    type: CSharp  # Loaded from assembly
    runtimeType: Timer
    assemblyPath: "ExecutionEngine/ExecutionEngine.dll"
    typeName: "ExecutionEngine.Nodes.TimerNode"
    configuration:
      Schedule: "0 2 * * *"  # Cron: 2 AM daily
      TriggerOnStart: false

  # Step 1: Discover ETL Files (uses EventFileHandler)
  - nodeId: node-discover-files
    nodeName: Discover ETL Files
    type: CSharp  # Custom node loaded from assembly
    runtimeType: CSharp
    assemblyPath: "ExecutionEngine.Example/bin/Release/net8.0/ExecutionEngine.Example.dll"
    typeName: "ExecutionEngine.Example.Nodes.DiscoverEtlFilesNode"
    configuration:
      SearchPaths:
        - "C:\\logs\\etl\\*.etl"
        - "C:\\logs\\zip\\*.zip"

  # Step 2: Ensure Kusto Database Exists
  - nodeId: node-ensure-db
    nodeName: Ensure Kusto Database
    type: CSharp
    runtimeType: CSharp
    assemblyPath: "ExecutionEngine.Example/bin/Release/net8.0/ExecutionEngine.Example.dll"
    typeName: "ExecutionEngine.Example.Nodes.EnsureKustoDbNode"
    configuration:
      ConnectionString: "Data Source=http://172.24.102.61:8080"
      DatabaseName: "etldata"

  # Step 3: Process Each File (ForEach loop)
  - nodeId: foreach-process-file
    nodeName: Process Each File
    type: ForEach
    runtimeType: ForEach
    configuration:
      CollectionExpression: "GetGlobal(\"etlFiles\")"
      ItemVariableName: "etlFile"

  # Step 4: Parse ETL File (uses ScalableEventProcessor)
  - nodeId: node-parse-etl
    nodeName: Parse ETL to CSV Batches
    type: CSharp
    runtimeType: CSharp
    assemblyPath: "ExecutionEngine.Example/bin/Release/net8.0/ExecutionEngine.Example.dll"
    typeName: "ExecutionEngine.Example.Nodes.ParseEtlFileNode"
    configuration:
      OutputDirectory: "C:\\logs\\csv"
      BatchSize: 100  # Group CSVs into batches of 100 files

  # Step 5: Ensure Kusto Tables Exist
  - nodeId: node-ensure-tables
    nodeName: Ensure Kusto Tables
    type: CSharp
    runtimeType: CSharp
    assemblyPath: "ExecutionEngine.Example/bin/Release/net8.0/ExecutionEngine.Example.dll"
    typeName: "ExecutionEngine.Example.Nodes.EnsureKustoTableNode"
    # ConnectionString and DatabaseName will be read from global variables

  # Step 6: Process Each Batch (nested ForEach)
  - nodeId: foreach-batch
    nodeName: Process Each Batch
    type: ForEach
    runtimeType: ForEach
    configuration:
      CollectionExpression: "Input[\"BatchedCsvFiles\"]"
      ItemVariableName: "csvBatch"

  # Step 7: Ingest Batch to Kusto
  - nodeId: node-ingest-kusto
    nodeName: Ingest CSV Batch to Kusto
    type: CSharp
    runtimeType: CSharp
    assemblyPath: "ExecutionEngine.Example/bin/Release/net8.0/ExecutionEngine.Example.dll"
    typeName: "ExecutionEngine.Example.Nodes.IngestToKustoNode"
    # ConnectionString and DatabaseName will be read from global variables

  # Step 8: Cleanup CSV Files
  - nodeId: task-cleanup
    nodeName: Cleanup CSV Files
    type: CSharpTask
    runtimeType: CSharpTask
    configuration:
      Script: |
        var outputDir = GetGlobal("outputDirectory")?.ToString();
        if (!string.IsNullOrEmpty(outputDir) && Directory.Exists(outputDir))
        {
            var csvFiles = Directory.GetFiles(outputDir, "*.csv");
            foreach (var csv in csvFiles)
            {
                File.Delete(csv);
            }
            SetOutput("FilesDeleted", csvFiles.Length);
        }

  # Error Handler
  - nodeId: task-error-handler
    nodeName: Handle Processing Error
    type: CSharpTask
    runtimeType: CSharpTask
    configuration:
      Script: |
        var errorMsg = Input.ContainsKey("ErrorMessage")
            ? Input["ErrorMessage"]?.ToString()
            : "Unknown error";
        Console.WriteLine($"ERROR: {errorMsg}");
        // TODO: Log to monitoring system, send alert, etc.

edges:
  # Timer -> Discover Files
  - edgeId: edge-1
    sourceNodeId: timer-daily
    targetNodeId: node-discover-files
    messageType: OnComplete

  # Discover Files -> Ensure DB
  - edgeId: edge-2
    sourceNodeId: node-discover-files
    targetNodeId: node-ensure-db
    messageType: OnComplete

  # Ensure DB -> ForEach File
  - edgeId: edge-3
    sourceNodeId: node-ensure-db
    targetNodeId: foreach-process-file
    messageType: OnComplete

  # ForEach File -> Parse ETL (loop body)
  - edgeId: edge-4
    sourceNodeId: foreach-process-file
    targetNodeId: node-parse-etl
    messageType: OnNext
    sourcePort: "LoopBody"

  # Parse ETL -> Ensure Tables
  - edgeId: edge-5
    sourceNodeId: node-parse-etl
    targetNodeId: node-ensure-tables
    messageType: OnComplete

  # Ensure Tables -> ForEach Batch
  - edgeId: edge-6
    sourceNodeId: node-ensure-tables
    targetNodeId: foreach-batch
    messageType: OnComplete

  # ForEach Batch -> Ingest (nested loop body)
  - edgeId: edge-7
    sourceNodeId: foreach-batch
    targetNodeId: node-ingest-kusto
    messageType: OnNext
    sourcePort: "LoopBody"

  # Ingest -> Cleanup (after all batches complete)
  - edgeId: edge-8
    sourceNodeId: foreach-batch
    targetNodeId: task-cleanup
    messageType: OnComplete

  # Error handling edges
  - edgeId: edge-error-1
    sourceNodeId: node-parse-etl
    targetNodeId: task-error-handler
    messageType: OnFail

  - edgeId: edge-error-2
    sourceNodeId: node-ingest-kusto
    targetNodeId: task-error-handler
    messageType: OnFail

defaultVariables:
  etlDirectory: "C:\\logs\\etl"
  kustoConnectionString: "Data Source=http://172.24.102.61:8080"
  kustoDatabaseName: "etldata"
```

**Workflow Execution Flow:**

```
┌────────────────┐
│  Timer (Daily) │ Trigger at 2 AM
└────────┬───────┘
         │
         ▼
┌────────────────────────┐
│  Discover ETL Files    │ Find all .etl and .zip files
│  (EventFileHandler)    │
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  Ensure Kusto Database │ Create DB if not exists
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  ForEach ETL File      │ Loop through each file
└────────┬───────────────┘
         │
         ▼
┌────────────────────────────┐
│  Parse ETL to CSV Batches  │ Generate CSV files in batches
│  (ScalableEventProcessor)  │
└────────┬───────────────────┘
         │
         ▼
┌────────────────────────┐
│  Ensure Kusto Tables   │ Create tables for CSV schemas
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  ForEach CSV Batch     │ Loop through batches
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  Ingest to Kusto       │ Ingest batch into Kusto
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│  Cleanup CSV Files     │ Delete temporary CSV files
└────────────────────────┘
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
