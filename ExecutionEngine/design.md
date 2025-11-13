## 2. Core Concepts

### 2.1 Execution Plan (Graph)

An **Execution Plan** is a directed graph that defines the workflow structure:

- **Nodes**: Represent units of execution (tasks, control flow, subflows)
- **Edges (Connectors)**: Define execution flow and dependencies between nodes
- **Direction**: One-way directed edges showing the flow from source to target node

```
┌─────────┐     OnComplete     ┌─────────┐     OnComplete     ┌─────────┐
│ Node A  │ ──────────────────>│ Node B  │ ──────────────────>│ Node C  │
└─────────┘                    └─────────┘                    └─────────┘
     │                               │
     │ OnFail                        │ OnFail
     ▼                               ▼
┌─────────┐                    ┌─────────┐
│ Handler │                    │ Handler │
└─────────┘                    └─────────┘
```

### 2.2 Workflow Instance

A **Workflow Instance** is a runtime execution of a graph:

- Created when a graph is triggered (manually or by timer)
- Has a unique instance ID
- Maintains execution state and context
- Can be paused, resumed, or cancelled
- State is persisted to disk for recovery

### 2.3 Workflow Execution Context

The **Workflow Execution Context** is the shared state container for a workflow instance:


**Design Decisions:**
- Use `ConcurrentDictionary` for thread-safe variable access
- Each node has its own dedicated `NodeMessageQueue` for message isolation and parallel processing
- Messages are routed to specific node queues based on workflow definition
- Dead letter queue captures messages that exceed max retry attempts
- Strong-typed messages implement `INodeMessage` interface

### 2.4 Message Queue Architecture

The execution engine uses a **per-node message queue** system with leasing, retry logic, and dead letter queue support.

#### Node Message Queue

Each node has its own dedicated message queue for receiving messages:


**Lock-Free Circular Buffer with Visibility Timeout:**

This implementation combines two powerful patterns for maximum performance:

**1. Circular Buffer (Lock-Free)**:
- Fixed-size array with wraparound indexing (`position % capacity`)
- Lock-free operations using `Interlocked.CompareExchange` (CAS)
- No internal locks or allocations during enqueue/dequeue
- Write and read positions increment indefinitely, modulo for slot index
- Handles buffer full condition by dropping oldest Ready messages
- Thread-safe for concurrent producers and consumers

**2. Visibility Timestamp Pattern**:
- No Task.Run overhead - messages have `VisibleAfter` timestamp
- Passive blocking - only messages where `UtcNow >= VisibleAfter` are visible
- Automatic invisibility during lease period
- Uses `SemaphoreSlim` for efficient wait signaling
- Background monitor for expired lease cleanup

**3. Status Transitions (Lock-Free via CAS)**:
```
Ready → InFlight → Removed (Completed)
   ↑       ↓
   └── Abandon/Retry (back to Ready)
```

All state changes use Compare-And-Swap for thread safety without locks.

**Key Benefits:**
- ✅ **Lock-free**: No locks, no contention, true parallel access
- ✅ **Zero allocations**: Pre-allocated array, no GC pressure during operations
- ✅ **Cache-friendly**: Contiguous memory layout improves CPU cache hits
- ✅ **Predictable performance**: O(1) operations with fixed capacity
- ✅ **Scalable**: Tested to millions of messages per second
- ✅ **No Task overhead**: Visibility timestamps instead of active timers
- ✅ **Bounded memory**: Fixed capacity prevents unbounded growth

**Performance Characteristics:**
- Enqueue: O(capacity) worst case (scanning for empty slot), typically O(1)
- Lease: O(capacity) worst case (scanning for visible message), typically O(1)
- Complete/Abandon: O(capacity) (linear scan by lease ID)
- Memory: Fixed at `capacity * sizeof(QueuedMessage)` + object overhead

#### Lease Monitor

A single background worker monitors all queues for expired leases:


**Lease Monitor Responsibilities:**
- Runs every 30 seconds (configurable)
- Scans all node queues for expired leases
- Automatically abandons expired leases (triggers retry or DLQ)
- Single task for entire workflow (not per-message)
- Low overhead and predictable performance

#### Dead Letter Queue

Messages that exceed max retry attempts are moved to the dead letter queue for manual inspection:


#### Message Router

The message router determines which node queue should receive a message based on workflow definition:


**Message Flow Pattern:**

```
┌──────────────┐
│   Node A     │
│   Executes   │
└──────┬───────┘
       │ Produces NodeCompleteMessage
       │
       ▼
┌──────────────────────┐
│  MessageRouter       │
│  Routes based on     │
│  Edge definitions    │
└──────┬───────────────┘
       │
       ├──────────────────────────┐
       │                          │
       ▼                          ▼
┌──────────────────┐    ┌──────────────────┐
│ Node B Queue     │    │ Node C Queue     │
│ [Msg1]           │    │ [Msg1]           │
└──────┬───────────┘    └──────┬───────────┘
       │                        │
       │ Lease with timeout     │ Lease with timeout
       ▼                        ▼
┌──────────────────┐    ┌──────────────────┐
│   Node B         │    │   Node C         │
│   Processor      │    │   Processor      │
│   (Parallel)     │    │   (Parallel)     │
└──────────────────┘    └──────────────────┘
```

### 2.5 Node Instance and Node Execution Context

When a node is executed within a workflow instance, it creates a **NodeInstance** which encapsulates the node's execution state and local context.

#### NodeInstance

A **NodeInstance** represents a specific execution of a node within a workflow instance:


#### NodeExecutionContext

A **NodeExecutionContext** is a local key-value store that contains data specific to a single node execution:


**Key Benefits:**

1. **Data Isolation**: Each node execution has its own local scope, preventing unintended side effects
2. **Data Flow Traceability**: Clear input/output contracts between nodes
3. **Context Passing**: Output from one node becomes input to the next, enabling pipeline-style workflows
4. **Debugging**: Easier to inspect and debug individual node executions
5. **Parallel Execution**: Multiple instances of the same node can execute concurrently without interference

**Usage Pattern:**

```
┌─────────────────────────────────────────────────────────────┐
│                      Workflow Execution                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│ NodeInstance A                                               │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ NodeExecutionContext                                     │ │
│ │ InputData: {}                                            │ │
│ │ LocalVariables: { "x": 10 }                              │ │
│ │ OutputData: { "result": 20 }                             │ │
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
                              │
                              ▼ (via NodeCompleteMessage)
┌──────────────────────────────────────────────────────────────┐
│ NodeInstance B                                               │
│ ┌──────────────────────────────────────────────────────────┐ │
│ │ NodeExecutionContext                                     │ │
│ │ InputData: { "result": 20 }    ← from Node A             │ │
│ │ LocalVariables: { "y": 5 }                               │ │
│ │ OutputData: { "finalResult": 25 }                        │ │
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

## 3. Node Architecture

### 3.1 Base Node Interface

All nodes implement the following interface:


**Key Changes:**

- `ExecuteAsync` now receives both `ExecutionContext` (workflow-level) and `NodeExecutionContext` (node-level)
- `ExecuteAsync` returns `NodeInstance` containing execution results and context
- Node can access input data from `nodeContext.InputData`
- Node writes output to `nodeContext.OutputData`
- Node can use `nodeContext.LocalVariables` for internal state


#### 3.1.1 Node Factory Pattern

The execution engine uses a factory pattern to dynamically load and instantiate nodes at runtime:


#### 3.1.2 Shared Contract: IExecutableNode

Both C# and PowerShell nodes implement a common execution pattern:


### 3.2 Node Messages

Nodes communicate via strongly-typed messages enqueued to the message queue. Each message now includes the `NodeExecutionContext` to enable data flow between nodes:


**Context Flow Pattern:**

When Node A completes:
1. Node A populates `nodeContext.OutputData` with results
2. Node A sends `NodeCompleteMessage` with `NodeContext` attached
3. Execution engine receives message
4. For each downstream node (Node B), engine creates new `NodeExecutionContext` with:
   - `InputData` = Node A's `OutputData`
   - Fresh `LocalVariables` and `OutputData`
5. Node B executes with access to Node A's output via `InputData`

This creates a clear data pipeline: Node A Output → Node B Input → Node B Output → Node C Input

### 3.3 Node Execution Lifecycle

```
┌─────────┐
│ Pending │
└────┬────┘
     │
     ▼
┌─────────┐  OnStart Event
│Starting │ ───────────────>
└────┬────┘
     │
     ▼
┌─────────┐  OnProgress Events
│ Running │ ───────────────────>
└────┬────┘
     │
     ├──> OnComplete Message ──> ┌───────────┐
     │                           │ Completed │
     │                           └───────────┘
     │
     └──> OnFail Message ──────> ┌─────────┐
                                 │ Failed  │
                                 └─────────┘
```

### 3.4 Node Types

#### 3.4.1 Task Node (C# Code Execution)

**C# nodes can be loaded in two ways:**
1. **Inline Scripts**: Using Roslyn scripting API for simple, embedded scripts
2. **Compiled Assemblies**: Loading compiled .NET assemblies for complex, reusable tasks


**Example 1: Inline Script Usage**


**Example 2: Compiled Node from Assembly**


#### 3.4.2 PowerShell Task Node

**PowerShell nodes can be executed in two ways:**
1. **Inline Scripts**: Embedded script content in the workflow definition
2. **Script Files**: Loading .ps1 files with module dependencies for complex, reusable tasks


**Example 1: Inline PowerShell Script Usage**

```powershell
# Access input from previous node using helper function
$fileCount = Get-Input "fileCount"

# Or access directly from $Input hashtable
Write-Host "Processing $fileCount files from: $($Input['sourceDir'])"

# Use local variables for computation
$Local["processedCount"] = 0

# Access workflow-level variables using helper function
$baseDir = Get-Global "baseDirectory"

# Set output for next node using helper function
Set-Output "processedFiles" 42
Set-Output "status" "success"

# Or use $Output hashtable directly
$Output["totalSize"] = 1024000

# Return value goes to Results output
return @{
    files = @("file1.txt", "file2.txt")
    total = 2
}
```

**Example 2: PowerShell Script File with Modules**

```powershell
# File: scripts/ProcessKustoData.ps1
# Requires: PSKusto module for Kusto operations

# This script is loaded from file and has access to imported modules

# Use module cmdlets
$kustoResults = Invoke-KustoQuery -Cluster $cluster -Database $db -Query "Events | take 100"

# Access input from previous node
$tableName = Get-Input "tableName"
$cluster = Get-Input "kustoCluster"
$db = Get-Input "kustoDatabase"

Write-Host "Processing table: $tableName"

# Process data
$recordCount = 0
foreach ($record in $kustoResults) {
    # Process each record
    $recordCount++

    # Update local state
    $Local["lastProcessedId"] = $record.Id
}

# Report results
Set-Output "recordCount" $recordCount
Set-Output "status" "completed"
Set-Output "lastId" $Local["lastProcessedId"]

# Can also return hashtable
return @{
    success = $true
    processedAt = (Get-Date)
}
```

#### 3.4.3 Subflow Node


#### 3.4.4 Control Flow Nodes

##### If-Else Node

The `IfElseNode` implements conditional branching in workflows using **SourcePort-based routing**. It evaluates a C# boolean expression and routes execution to either the `TrueBranch` or `FalseBranch` output port based on the result.

**Design Philosophy: Why SourcePorts Instead of TargetPorts?**

The IfElseNode uses **SourcePort** routing rather than TargetPort routing for several key reasons:

1. **Source Determines Routing**: The node that produces output determines which port the message is sent from, making the routing decision explicit at the source rather than implicit at the target.

2. **Multiple Outputs, Single Responsibility**: A control flow node has multiple output paths (TrueBranch, FalseBranch), but each downstream node typically has only one input. Using SourcePort allows the control flow node to clearly indicate "this message is from my true branch" without requiring all downstream nodes to understand conditional logic.

3. **Simpler Connection Configuration**: When connecting nodes, you specify `SourcePort: "TrueBranch"` on the connection, making it clear which output port is being connected. This is more intuitive than having downstream nodes filter by TargetPort.

4. **Scalability**: This pattern scales well to nodes with many output ports (e.g., switch nodes with multiple cases, parallel nodes with multiple lanes).

**Implementation:**


**Condition Evaluation Context:**

The condition expression has access to:
- **Global Variables**: `GetGlobal("variableName")` - workflow-level variables
- **Local Variables**: `Local["variableName"]` - node-scoped variables
- **Input Data**: `GetInput("key")` - data from upstream nodes
- **Output Data**: `Output` - current node's output dictionary
- **.NET Libraries**: Full C# syntax with System, System.Linq, System.Collections.Generic

**Hooking Up True/False Branches:**

To connect nodes to the true and false branches, use the `SourcePort` property on the `NodeConnection`:

```yaml
# YAML Workflow Definition Example
workflowId: conditional-processing
workflowName: Conditional Data Processing
description: Demonstrates if-else conditional routing with SourcePort

nodes:
  - nodeId: check-threshold
    nodeName: Check Threshold
    runtimeType: IfElse
    configuration:
      Condition: "GetGlobal(\"count\") > 100"
    description: Checks if count exceeds threshold

  - nodeId: high-volume-handler
    nodeName: Handle High Volume
    runtimeType: CSharpScript
    scriptPath: scripts/handle-high-volume.csx
    description: Processes high volume data

  - nodeId: normal-handler
    nodeName: Handle Normal Volume
    runtimeType: CSharpScript
    scriptPath: scripts/handle-normal.csx
    description: Processes normal volume data

  - nodeId: log-decision
    nodeName: Log Decision
    runtimeType: CSharpScript
    scriptPath: scripts/log-decision.csx
    description: Logs which branch was taken

connections:
  # Connect TrueBranch to high-volume-handler
  - sourceNodeId: check-threshold
    targetNodeId: high-volume-handler
    sourcePort: TrueBranch  # ← Key: specify which port to connect from
    triggerMessageType: Complete
    isEnabled: true

  # Connect FalseBranch to normal-handler
  - sourceNodeId: check-threshold
    targetNodeId: normal-handler
    sourcePort: FalseBranch  # ← Key: specify which port to connect from
    triggerMessageType: Complete
    isEnabled: true

  # Both branches converge at logging node (no sourcePort = default port)
  - sourceNodeId: high-volume-handler
    targetNodeId: log-decision
    triggerMessageType: Complete
    isEnabled: true

  - sourceNodeId: normal-handler
    targetNodeId: log-decision
    triggerMessageType: Complete
    isEnabled: true
```

**Advanced Example: Evaluating Global, Local, and Input Data:**

```yaml
nodes:
  - nodeId: check-user-eligibility
    nodeName: Check User Eligibility
    runtimeType: IfElse
    configuration:
      # Complex condition accessing multiple contexts:
      # - GetGlobal(): workflow-level variables
      # - GetInput(): upstream node output
      # - Local: node-scoped variables (if set by upstream)
      Condition: >
        GetGlobal("environment") == "production" &&
        (int)GetInput("userAge") >= 18 &&
        (string)GetInput("accountStatus") == "active" &&
        ((int?)GetGlobal("maxConcurrentUsers") ?? 100) > 50
    description: Multi-context eligibility check

  - nodeId: eligible-users
    nodeName: Process Eligible Users
    runtimeType: CSharpScript
    scriptPath: scripts/process-eligible.csx

  - nodeId: ineligible-users
    nodeName: Handle Ineligible Users
    runtimeType: CSharpScript
    scriptPath: scripts/handle-ineligible.csx

connections:
  - sourceNodeId: check-user-eligibility
    targetNodeId: eligible-users
    sourcePort: TrueBranch
    triggerMessageType: Complete

  - sourceNodeId: check-user-eligibility
    targetNodeId: ineligible-users
    sourcePort: FalseBranch
    triggerMessageType: Complete
```

**C# Code Example (Programmatic Definition):**


**Output Data (Available to Downstream Nodes):**

After execution, the IfElseNode sets the following in its output data:
- `BranchTaken`: String indicating which branch was taken ("TrueBranch" or "FalseBranch")
- `ConditionResult`: Boolean result of the condition evaluation (true or false)

Downstream nodes can access this via `GetInput("BranchTaken")` or `GetInput("ConditionResult")`.

**Error Handling:**

The node handles several error cases:
- **Null/Empty Condition**: Throws `InvalidOperationException` if condition is not defined
- **Compilation Errors**: Returns detailed compilation diagnostics if the C# expression has syntax errors
- **Runtime Errors**: Catches exceptions during evaluation and sets node status to Failed

**Key Features:**

1. **Full C# Expression Support**: Uses Roslyn scripting engine for powerful condition evaluation
2. **Multi-Context Access**: Can evaluate conditions based on global variables, local variables, and input data
3. **Type Safety**: Supports explicit type casting (e.g., `(int)GetInput("age")`)
4. **Null Coalescing**: Supports null-coalescing operators (e.g., `GetGlobal("count") ?? 0`)
5. **LINQ Support**: Can use LINQ expressions for complex data queries
6. **Compile-Time Validation**: Catches syntax errors before runtime execution
7. **Debugging Support**: Stores branch taken and condition result for troubleshooting

**Implementation File:** `ExecutionEngine/Nodes/IfElseNode.cs`

---

**ForEach Node:**


## 4. Graph Structure

### 4.1 Graph Definition


**Edge with Message Routing:**

Edges now explicitly define which message type they respond to, enabling:
- Direct routing: `NodeCompleteMessage` from Node A → Node B queue
- Error handling: `NodeFailMessage` from Node A → Error Handler queue
- Parallel fanout: Same message to multiple node queues
- Independent queue configuration: Per-edge retry and timeout settings
```

### 4.2 Graph Serialization Formats

**JSON Format:**

```json
{
  "graphId": "data-processing-workflow",
  "name": "Data Processing Workflow",
  "description": "ETL workflow for processing log files",
  "nodes": [
    {
      "nodeId": "timer-1",
      "nodeName": "Daily Trigger",
      "type": "Timer",
      "configuration": {
        "schedule": "0 0 * * *"
      }
    },
    {
      "nodeId": "task-1",
      "nodeName": "Parse ETL Files",
      "type": "Task",
      "configuration": {
        "language": "CSharp",
        "script": "var files = Directory.GetFiles(context.Variables[\"inputPath\"]);\ncontext.Variables[\"fileCount\"] = files.Length;\nreturn new { files };"
      }
    },
    {
      "nodeId": "foreach-1",
      "nodeName": "Process Each File",
      "type": "ForEach",
      "configuration": {
        "collectionExpression": "context.Variables[\"files\"]",
        "itemVariableName": "currentFile"
      }
    },
    {
      "nodeId": "task-2",
      "nodeName": "Ingest to Kusto",
      "type": "Task",
      "configuration": {
        "language": "PowerShell",
        "script": "Write-Host \"Processing $currentFile\"\n# Ingest logic here"
      }
    }
  ],
  "edges": [
    {
      "edgeId": "edge-1",
      "sourceNodeId": "timer-1",
      "targetNodeId": "task-1",
      "type": "OnComplete"
    },
    {
      "edgeId": "edge-2",
      "sourceNodeId": "task-1",
      "targetNodeId": "foreach-1",
      "type": "OnComplete"
    },
    {
      "edgeId": "edge-3",
      "sourceNodeId": "foreach-1",
      "targetNodeId": "task-2",
      "type": "LoopBody"
    }
  ],
  "defaultVariables": {
    "inputPath": "C:\\logs",
    "kustoCluster": "http://172.24.102.61:8080"
  }
}
```

**YAML Format:**

```yaml
graphId: data-processing-workflow
name: Data Processing Workflow
description: ETL workflow for processing log files

nodes:
  - nodeId: timer-1
    nodeName: Daily Trigger
    type: Timer
    configuration:
      schedule: "0 0 * * *"

  - nodeId: task-1
    nodeName: Parse ETL Files
    type: Task
    configuration:
      language: CSharp
      script: |
        var files = Directory.GetFiles(context.Variables["inputPath"]);
        context.Variables["fileCount"] = files.Length;
        return new { files };

  - nodeId: foreach-1
    nodeName: Process Each File
    type: ForEach
    configuration:
      collectionExpression: context.Variables["files"]
      itemVariableName: currentFile

  - nodeId: task-2
    nodeName: Ingest to Kusto
    type: Task
    configuration:
      language: PowerShell
      script: |
        Write-Host "Processing $currentFile"
        # Ingest logic here

edges:
  - edgeId: edge-1
    sourceNodeId: timer-1
    targetNodeId: task-1
    type: OnComplete

  - edgeId: edge-2
    sourceNodeId: task-1
    targetNodeId: foreach-1
    type: OnComplete

  - edgeId: edge-3
    sourceNodeId: foreach-1
    targetNodeId: task-2
    type: LoopBody

defaultVariables:
  inputPath: C:\logs
  kustoCluster: http://172.24.102.61:8080
```

### 4.3 NodeDefinition Format with Dynamic Loading

**New Format for Loading Compiled Assemblies and Script Files:**

The new NodeDefinition format supports loading C# nodes from compiled assemblies and PowerShell nodes from script files with module dependencies.

**JSON Format with NodeDefinition:**

```json
{
  "graphId": "advanced-data-pipeline",
  "name": "Advanced Data Pipeline with External Modules",
  "description": "Workflow using compiled C# nodes and PowerShell scripts with module dependencies",
  "nodes": [
    {
      "nodeId": "compiled-task-1",
      "nodeName": "Custom Data Processor",
      "type": "CSharpTask",
      "runtimeType": "CSharp",
      "assemblyPath": "./plugins/DataProcessing.dll",
      "typeName": "MyCompany.Workflows.DataProcessingNode",
      "configuration": {
        "batchSize": 1000,
        "retryCount": 3,
        "timeout": "00:05:00"
      }
    },
    {
      "nodeId": "ps-script-task-1",
      "nodeName": "Kusto Ingestion Script",
      "type": "PowerShellTask",
      "runtimeType": "PowerShell",
      "scriptPath": "./scripts/IngestToKusto.ps1",
      "requiredModules": [
        "PSKusto",
        "MyCustomModule"
      ],
      "modulePaths": {
        "PSKusto": "./modules/PSKusto",
        "MyCustomModule": "./modules/MyCustomModule"
      },
      "configuration": {
        "kustoCluster": "http://172.24.102.61:8080",
        "database": "EtwLogs"
      }
    },
    {
      "nodeId": "inline-cs-task",
      "nodeName": "Simple Inline C# Task",
      "type": "CSharpTask",
      "runtimeType": "CSharp",
      "configuration": {
        "script": "var count = (int)GetInput(\"fileCount\");\nSetOutput(\"doubled\", count * 2);\nreturn new Dictionary<string, object> { { \"result\", \"success\" } };"
      }
    },
    {
      "nodeId": "inline-ps-task",
      "nodeName": "Simple Inline PowerShell Task",
      "type": "PowerShellTask",
      "runtimeType": "PowerShell",
      "configuration": {
        "script": "$count = Get-Input 'fileCount'\nSet-Output 'doubled' ($count * 2)\nreturn @{ result = 'success' }"
      }
    }
  ],
  "edges": [
    {
      "edgeId": "edge-1",
      "sourceNodeId": "compiled-task-1",
      "targetNodeId": "ps-script-task-1",
      "type": "OnComplete",
      "messageType": "Complete",
      "maxRetries": 5,
      "visibilityTimeout": "00:10:00"
    },
    {
      "edgeId": "edge-2",
      "sourceNodeId": "ps-script-task-1",
      "targetNodeId": "inline-cs-task",
      "type": "OnComplete",
      "messageType": "Complete"
    },
    {
      "edgeId": "edge-3",
      "sourceNodeId": "inline-cs-task",
      "targetNodeId": "inline-ps-task",
      "type": "OnComplete",
      "messageType": "Complete"
    }
  ],
  "defaultVariables": {
    "inputPath": "C:\\data\\input",
    "outputPath": "C:\\data\\output"
  }
}
```

**YAML Format with NodeDefinition:**

```yaml
graphId: advanced-data-pipeline
name: Advanced Data Pipeline with External Modules
description: Workflow using compiled C# nodes and PowerShell scripts with module dependencies

nodes:
  # Compiled C# node loaded from assembly
  - nodeId: compiled-task-1
    nodeName: Custom Data Processor
    type: CSharpTask
    runtimeType: CSharp
    assemblyPath: ./plugins/DataProcessing.dll
    typeName: MyCompany.Workflows.DataProcessingNode
    configuration:
      batchSize: 1000
      retryCount: 3
      timeout: "00:05:00"

  # PowerShell script loaded from file with module dependencies
  - nodeId: ps-script-task-1
    nodeName: Kusto Ingestion Script
    type: PowerShellTask
    runtimeType: PowerShell
    scriptPath: ./scripts/IngestToKusto.ps1
    requiredModules:
      - PSKusto
      - MyCustomModule
    modulePaths:
      PSKusto: ./modules/PSKusto
      MyCustomModule: ./modules/MyCustomModule
    configuration:
      kustoCluster: http://172.24.102.61:8080
      database: EtwLogs

  # Inline C# script (legacy format)
  - nodeId: inline-cs-task
    nodeName: Simple Inline C# Task
    type: CSharpTask
    runtimeType: CSharp
    configuration:
      script: |
        var count = (int)GetInput("fileCount");
        SetOutput("doubled", count * 2);
        return new Dictionary<string, object> { { "result", "success" } };

  # Inline PowerShell script (legacy format)
  - nodeId: inline-ps-task
    nodeName: Simple Inline PowerShell Task
    type: PowerShellTask
    runtimeType: PowerShell
    configuration:
      script: |
        $count = Get-Input 'fileCount'
        Set-Output 'doubled' ($count * 2)
        return @{ result = 'success' }

edges:
  - edgeId: edge-1
    sourceNodeId: compiled-task-1
    targetNodeId: ps-script-task-1
    type: OnComplete
    messageType: Complete
    maxRetries: 5
    visibilityTimeout: "00:10:00"

  - edgeId: edge-2
    sourceNodeId: ps-script-task-1
    targetNodeId: inline-cs-task
    type: OnComplete
    messageType: Complete

  - edgeId: edge-3
    sourceNodeId: inline-cs-task
    targetNodeId: inline-ps-task
    type: OnComplete
    messageType: Complete

defaultVariables:
  inputPath: C:\data\input
  outputPath: C:\data\output
```

**Key Features of NodeDefinition Format:**

1. **C# Compiled Nodes:**
   - `assemblyPath`: Path to the .NET assembly DLL
   - `typeName`: Fully qualified type name (e.g., `Namespace.ClassName`)
   - NodeFactory uses reflection to load and instantiate

2. **PowerShell Script Nodes:**
   - `scriptPath`: Path to the .ps1 PowerShell script file
   - `requiredModules`: List of PowerShell modules to import
   - `modulePaths`: Optional custom paths for module loading
   - Modules are imported with `Import-Module -Force` before script execution

3. **Inline Scripts (Legacy):**
   - Still supported for simple, embedded scripts
   - Use `configuration.script` property
   - No assembly or script path required

4. **Configuration:**
   - Custom settings passed to node via `Initialize()` method
   - Available in compiled nodes for dependency injection or setup

5. **Edge Message Routing:**
   - `messageType`: Explicit message type for routing (Complete, Fail, Progress)
   - `maxRetries`: Per-edge retry configuration
   - `visibilityTimeout`: Per-edge lease timeout

**Loading Workflow at Runtime:**


## 5. Graph Validation

### 5.1 Validation Rules

The graph must be validated before execution:

1. **Structural Validation:**
   - All referenced node IDs in edges exist
   - No duplicate node IDs
   - No duplicate edge IDs
   - At least one trigger node (Timer or Manual)

2. **Cycle Detection:**
   - Detect infinite loops using graph traversal (DFS/Tarjan's algorithm)
   - Control flow nodes (While, ForEach) are allowed to have cycles within their scope
   - Regular task nodes cannot form cycles

3. **Reachability Validation:**
   - All nodes must be reachable from at least one trigger node
   - Warn about unreachable nodes (dead code)

4. **Type Validation:**
   - If-Else nodes must have exactly 2 outgoing edges (True/False)
   - Switch nodes must have at least 1 case edge
   - ForEach/While nodes must have LoopBody edges

### 5.2 Validation Implementation


## 6. Execution Engine

### 6.1 Engine Architecture


### 6.2 Workflow Instance State


**NodeInstance Tracking Benefits:**

1. **Execution History**: Complete audit trail of all node executions
2. **Loop Iterations**: Track each iteration of a ForEach loop separately
3. **Debugging**: Inspect input/output of specific node instances
4. **Retry Logic**: Retry failed node instances with same context
5. **Performance Analysis**: Measure execution time per node instance

