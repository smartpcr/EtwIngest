# ExecutionEngine.Example

A console application for running ExecutionEngine workflows defined in YAML files. This app demonstrates how to load, validate, and execute workflows using the ExecutionEngine framework.

## Features

- **YAML Workflow Loading**: Load workflow definitions from YAML files
- **Workflow Validation**: Validate workflows without executing them
- **Single Execution Mode**: Run a workflow once and exit
- **Watch Mode**: Continuously execute workflows (useful for timer-based workflows)
- **Event Tracking**: Real-time progress and status updates
- **Custom Nodes**: Example custom node implementations
- **Graceful Shutdown**: Handle Ctrl+C to cancel running workflows

## Project Structure

```
ExecutionEngine.Example/
├── Nodes/
│   ├── EnsureKustoDbNode.cs         # Kusto database provisioning
│   ├── EnsureKustoTableNode.cs      # Kusto table creation
│   ├── ParseEtlFileNode.cs          # ETL file parsing
│   ├── IngestToKustoNode.cs         # Kusto data ingestion
│   ├── DiscoverEtlFilesNode.cs      # ETL file discovery
│   ├── LogNode.cs                   # Simple logging node
│   ├── DataProcessorNode.cs         # Data transformation node
│   └── AggregatorNode.cs            # Results aggregation node
├── Workflows/
│   ├── etl-workflow-with-kusto.yaml # ETL to Kusto pipeline (YAML)
│   ├── SimpleSequentialWorkflow.cs  # Sequential execution (C#)
│   ├── ParallelWorkflow.cs          # Parallel execution (C#)
│   └── FanOutWorkflow.cs            # Fan-out/fan-in (C#)
├── Program.cs                       # Console app entry point
└── README.md                        # This file
```

## Quick Start

```bash
# Build the console app
dotnet build ExecutionEngine.Example/ExecutionEngine.Example.csproj

# Show help
dotnet run --project ExecutionEngine.Example -- --help

# List available workflows
dotnet run --project ExecutionEngine.Example -- list

# Validate a workflow
dotnet run --project ExecutionEngine.Example -- validate Workflows/etl-workflow-with-kusto.yaml

# Run a workflow once
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml

# Run in watch mode (continuous execution)
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml --watch
```

## Command Reference

### `run` - Execute a Workflow

Runs a workflow from a YAML file.

**Syntax:**
```bash
ExecutionEngine.Example run <workflow-file.yaml> [--watch]
```

**Options:**
- `--watch`: Run workflow continuously with 30-second polling interval

**Examples:**
```bash
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml --watch
```

### `validate` - Validate a Workflow

Validates a workflow YAML file without executing it.

**Syntax:**
```bash
ExecutionEngine.Example validate <workflow-file.yaml>
```

**Example:**
```bash
dotnet run --project ExecutionEngine.Example -- validate Workflows/etl-workflow-with-kusto.yaml
```

### `list` - List Available Workflows

Lists all YAML workflow files in the Workflows directory.

**Syntax:**
```bash
ExecutionEngine.Example list
```

## Example Workflows

### 1. ETL Workflow with Kusto (YAML) - `etl-workflow-with-kusto.yaml`

A comprehensive ETL pipeline demonstrating:
- Timer-triggered daily execution (2 AM)
- Kusto database provisioning with EnsureKustoDBNode
- ETL file discovery and parsing
- Parallel file processing with ForEach
- Dynamic table creation based on event schemas
- CSV extraction and Kusto ingestion
- File archiving with timestamps
- Statistics tracking and reporting

**Prerequisites:**
- Kustainer running at `http://172.24.102.61:8080`
- Volume mount from `C:\kustodata` to `/kustodata` in Kustainer
- ETL files in `C:\logs\etl`

**Configuration:**

Edit the `defaultVariables` section in the YAML file:

```yaml
defaultVariables:
  kustoClusterUri: "http://172.24.102.61:8080"
  kustoDatabase: "EtwLogs"
  etlSourcePath: "C:\\logs\\etl"
  csvOutputPath: "C:\\logs\\csv"
  archivePath: "C:\\logs\\archive"
```

**Running:**

```bash
# Single execution
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml

# Watch mode (checks timer every 30 seconds)
dotnet run --project ExecutionEngine.Example -- run Workflows/etl-workflow-with-kusto.yaml --watch
```

**Expected Output:**
```
[15:23:45] Workflow started: etl-kusto-pipeline
[15:23:45] Node started: daily-timer
[15:23:45] daily-timer: Evaluating schedule (10%)
[15:23:45] Node completed: daily-timer
[15:23:45] Node started: ensure-kusto-db
[15:23:46] ensure-kusto-db: Connecting to Kusto cluster (30%)
[15:23:47] ensure-kusto-db: Database ready (100%)
[15:23:47] Node completed: ensure-kusto-db
[15:23:47] Node started: scan-etl-files
...
```

### 2. Simple Sequential Workflow (C#)

Demonstrates basic sequential execution:
- Start Logger → Data Processor → Finish Logger

**Expected Output:**
```
[HH:mm:ss] start: Workflow started
[HH:mm:ss] process: Processing 'sample_data'
[HH:mm:ss] finish: Workflow completed
Status: Completed
Duration: ~2.05s
```

### 3. Parallel Processing Workflow (C#)

Demonstrates parallel execution with fan-out and fan-in:
- Start → Process1 & Process2 → Finish

**Expected Output:**
```
[HH:mm:ss] start: Workflow started
[HH:mm:ss] process1: Processing 'dataset_1'
[HH:mm:ss] process2: Processing 'dataset_2'
[HH:mm:ss] finish: Workflow completed (may trigger multiple times)
Status: Completed
Duration: ~3.5s
```

**Note:** The finish node may execute multiple times since it uses the default `JoinType.Any` behavior, triggering when ANY upstream node completes.

## Node Types

### LogNode
Simple node that logs messages with timestamps. Configuration:
- `message`: The message to log

### DataProcessorNode
Simulates data processing with a 1-second delay. Configuration:
- `data`: The data to process

Outputs:
- Sets `result` in output data
- Sets `{NodeId}_result` in global context

### AggregatorNode
Collects results from all nodes that set `*_result` in the global context and aggregates them into a comma-separated string.

Outputs:
- Sets `aggregated` in output data
- Sets `aggregate_result` in global context

## Known Issues

### JoinType.All Not Working

The `FanOutWorkflow` example includes an aggregator node that should use `JoinType.All` to wait for all upstream nodes to complete before executing. However, this functionality appears to have a bug in the WorkflowEngine.

**Current Behavior:**
- When `JoinType.All` is set, the aggregator node never executes
- All upstream nodes complete successfully, but the aggregator is never triggered

**Workaround:**
- The FanOutWorkflow is disabled in Program.cs
- Using default `JoinType.Any` causes the aggregator to run multiple times (once per upstream completion)

**Recommendation:**
- Investigate the JoinType.All implementation in WorkflowEngine.cs
- Add unit tests for JoinType.All behavior
- Fix the upstream completion tracking logic

## Key Learnings

1. **Node Implementation Pattern:**
   - Extend `ExecutableNodeBase`
   - Override `ExecuteAsync(WorkflowExecutionContext, NodeExecutionContext, CancellationToken)`
   - Return `NodeInstance` with execution status and timing
   - Use try-catch to properly handle errors

2. **Configuration Access:**
   - Configuration values are accessed via `this.definition.Configuration`
   - Must be set in the NodeDefinition when creating workflows

3. **Data Flow:**
   - Input/Output data: `nodeContext.InputData` / `nodeContext.OutputData`
   - Global variables: `workflowContext.Variables`
   - Local variables: `nodeContext.LocalVariables`

4. **Assembly Loading:**
   - For CSharp runtime type, must specify `AssemblyPath`
   - Use `typeof(ClassName).Assembly.Location` to get current assembly path

5. **Workflow Patterns:**
   - Sequential: Linear chain of connections
   - Parallel: Multiple connections from single source node
   - Join: Multiple connections to single target node (requires JoinType configuration)
