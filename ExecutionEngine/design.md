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

##### ForEach Node

The `ForEachNode` implements collection iteration in workflows using **SourcePort-based routing with NodeNextMessage**. It evaluates a C# expression that returns a collection (IEnumerable) and executes child nodes for each item in the collection.

**Design Philosophy:**

The ForEachNode uses a **push-based iteration model** where:

1. **Single Execution, Multiple Messages**: The ForEachNode executes once but emits multiple `NodeNextMessage` instances, one for each item in the collection.
2. **Iteration Context**: Each iteration receives its own `NodeExecutionContext` with the current item and index as input data.
3. **Parallel Potential**: Child nodes can process items in parallel since each iteration is an independent message.
4. **Message-Driven**: Unlike traditional imperative loops, the ForEachNode doesn't directly call child nodes - it routes messages to their queues.

**Implementation:**

The ForEachNode evaluates a collection expression and iterates over the results:

**Configuration Properties:**

- **CollectionExpression** (string, required): C# expression that evaluates to an IEnumerable collection
- **ItemVariableName** (string, optional): Name of the variable for the current item in each iteration (default: "item")

**Collection Expression Context:**

The collection expression has access to:
- **Global Variables**: `GetGlobal("variableName")` - workflow-level variables
- **Local Variables**: `Local["variableName"]` - node-scoped variables
- **Input Data**: `GetInput("key")` - data from upstream nodes
- **Output Data**: `Output` - current node's output dictionary
- **.NET Libraries**: Full C# syntax with System, System.Linq, System.Collections.Generic

**Iteration Behavior:**

For each item in the collection, the ForEachNode:
1. Sets `<ItemVariableName>` in workflow context to the current item
2. Sets `<ItemVariableName>Index` in workflow context to the current index (0-based)
3. Creates an `NodeExecutionContext` with `InputData` containing the item and index
4. Emits `NodeNextMessage` with the iteration context
5. Routes the message to child nodes connected via the `LoopBody` port

**Output Data (Available to Downstream Nodes):**

After execution, the ForEachNode sets the following in its output data:
- `ItemsProcessed`: Total number of items iterated
- `CollectionExpression`: The expression used to generate the collection
- `TotalItems`: Total count of items in the collection

**YAML Workflow Example:**

```yaml
workflowId: batch-file-processing
workflowName: Batch File Processing
description: Demonstrates ForEach iteration over file collection

nodes:
  - nodeId: discover-files
    nodeName: Discover Files
    runtimeType: CSharpScript
    configuration:
      script: |
        var files = Directory.GetFiles(@"C:\logs", "*.etl");
        SetOutput("files", files);
        return files.Length;
    description: Discovers ETL files in directory

  - nodeId: process-each-file
    nodeName: Process Each File
    runtimeType: ForEach
    configuration:
      CollectionExpression: "GetInput(\"files\")"
      ItemVariableName: "currentFile"
    description: Iterates over each file

  - nodeId: parse-file
    nodeName: Parse File
    runtimeType: CSharpScript
    configuration:
      script: |
        var filePath = (string)GetInput("currentFile");
        var index = (int)GetInput("currentFileIndex");
        Console.WriteLine($"Processing file {index + 1}: {filePath}");

        // Parse file logic here
        SetOutput("parsed", true);
        SetOutput("filePath", filePath);
    description: Parses individual file

  - nodeId: log-completion
    nodeName: Log Completion
    runtimeType: CSharpScript
    configuration:
      script: |
        var itemsProcessed = (int)GetInput("ItemsProcessed");
        Console.WriteLine($"Completed processing {itemsProcessed} files");
    description: Logs completion after all iterations

connections:
  # Connect discover-files to ForEach
  - sourceNodeId: discover-files
    targetNodeId: process-each-file
    triggerMessageType: Complete
    isEnabled: true

  # Connect ForEach to child node using LoopBody port and Next message type
  - sourceNodeId: process-each-file
    targetNodeId: parse-file
    sourcePort: LoopBody
    triggerMessageType: Next  # ← Key: ForEach emits Next messages for iterations
    isEnabled: true

  # Connect ForEach completion to logging node
  - sourceNodeId: process-each-file
    targetNodeId: log-completion
    triggerMessageType: Complete  # ← Triggered after all iterations complete
    isEnabled: true
```

**Advanced Example: Nested Collections and LINQ:**

```yaml
nodes:
  - nodeId: process-customers
    nodeName: Process Customer Orders
    runtimeType: ForEach
    configuration:
      # Complex LINQ expression to filter and transform data
      CollectionExpression: >
        GetInput("customers")
          .Cast<object>()
          .Where(c => ((dynamic)c).OrderCount > 10)
          .Select(c => new {
            CustomerId = ((dynamic)c).Id,
            Name = ((dynamic)c).Name
          })
      ItemVariableName: "customer"
    description: Processes high-value customers

  - nodeId: process-customer-node
    nodeName: Process Single Customer
    runtimeType: CSharpScript
    configuration:
      script: |
        var customer = GetInput("customer");
        var index = (int)GetInput("customerIndex");

        // Access customer properties
        var customerId = ((dynamic)customer).CustomerId;
        var customerName = ((dynamic)customer).Name;

        Console.WriteLine($"Processing customer {index + 1}: {customerName}");

connections:
  - sourceNodeId: process-customers
    targetNodeId: process-customer-node
    sourcePort: LoopBody
    triggerMessageType: Next
```

**C# Code Example (Programmatic Definition):**

```csharp
var workflow = new WorkflowDefinition
{
    WorkflowId = "batch-processing",
    Name = "Batch File Processing",
    Nodes = new List<NodeDefinition>
    {
        new NodeDefinition
        {
            NodeId = "foreach-files",
            NodeName = "Process Each File",
            RuntimeType = RuntimeType.ForEach,
            Configuration = new Dictionary<string, object>
            {
                { "CollectionExpression", "GetGlobal(\"files\")" },
                { "ItemVariableName", "file" }
            }
        },
        new NodeDefinition
        {
            NodeId = "parse-file",
            NodeName = "Parse File",
            RuntimeType = RuntimeType.CSharpScript,
            Configuration = new Dictionary<string, object>
            {
                { "Script", @"
                    var filePath = (string)GetInput(""file"");
                    var index = (int)GetInput(""fileIndex"");
                    Console.WriteLine($""Processing {filePath}"");
                " }
            }
        }
    },
    Connections = new List<NodeConnection>
    {
        new NodeConnection
        {
            SourceNodeId = "foreach-files",
            TargetNodeId = "parse-file",
            SourcePort = "LoopBody",
            TriggerMessageType = "Next"
        }
    }
};
```

**Error Handling:**

The node handles several error cases:
- **Null/Empty Expression**: Throws `InvalidOperationException` if CollectionExpression is not defined
- **Compilation Errors**: Returns detailed compilation diagnostics if the C# expression has syntax errors
- **Runtime Errors**: Catches exceptions during evaluation and sets node status to Failed
- **Null Result**: Throws `InvalidOperationException` if expression evaluates to null
- **Non-IEnumerable Result**: Throws `InvalidOperationException` if expression doesn't return an IEnumerable

**Key Features:**

1. **Full C# Expression Support**: Uses Roslyn scripting engine for powerful collection expressions
2. **LINQ Support**: Can use LINQ expressions for filtering, sorting, and transforming collections
3. **Type Safety**: Supports explicit type casting and dynamic access
4. **Progress Tracking**: Emits progress events with percentage completion
5. **Iteration Isolation**: Each iteration has its own `NodeExecutionContext`
6. **Parallel Processing**: Child nodes can execute in parallel for different iterations
7. **Debugging Support**: Stores items processed and collection details in output data

**Implementation File:** `ExecutionEngine/Nodes/ForEachNode.cs`

---

##### While Node

The `WhileNode` implements condition-based looping in workflows using a **feedback loop architecture**. It evaluates a C# boolean expression before each iteration and continues executing while the condition remains true.

**Design Philosophy: Feedback Loop Architecture**

Unlike ForEachNode which emits all iteration messages upfront, WhileNode uses a **feedback loop pattern**:

1. **Condition Re-Evaluation**: The condition is evaluated **before each iteration**, allowing child nodes to modify variables that affect the loop condition
2. **Feedback Connection**: Child nodes send `NodeCompleteMessage` back to the WhileNode to trigger the next iteration
3. **Stateful Iteration**: Uses workflow variables to track iteration count across multiple executions
4. **Safety Limits**: Configurable max iterations prevent infinite loops

**Architecture Pattern:**

```
┌─────────────┐
│ While Node  │────┐
│ Check Cond. │    │ Condition True: Emit Next
└─────────────┘    │
       ▲           ▼
       │      ┌──────────────┐
       │      │  Child Node  │
       │      │  (Loop Body) │
       │      └──────────────┘
       │           │
       └───────────┘
    Complete → Trigger Next Iteration
```

**Implementation:**

The WhileNode evaluates a condition and sends iteration messages while the condition is true:

**Configuration Properties:**

- **Condition** (string, required): C# boolean expression evaluated before each iteration
- **MaxIterations** (int, optional): Maximum number of iterations allowed (default: 1000)

**Condition Evaluation Context:**

The condition expression has access to:
- **Global Variables**: `GetGlobal("variableName")` - workflow-level variables modified by child nodes
- **Local Variables**: `Local["variableName"]` - node-scoped variables
- **Input Data**: `GetInput("key")` - data from upstream nodes
- **Output Data**: `Output` - current node's output dictionary
- **.NET Libraries**: Full C# syntax with System, System.Linq, System.Collections.Generic

**Iteration Behavior:**

On each execution, the WhileNode:
1. Checks if max iterations limit has been reached (fails if exceeded)
2. Evaluates the condition using current workflow variables
3. **If condition is true**:
   - Creates `NodeExecutionContext` with iteration index
   - Emits `NodeNextMessage` with iteration context
   - Routes message to child nodes connected via `LoopBody` port
   - Increments iteration counter
   - Completes with `SourcePort = "IterationCheck"` (special port to prevent exit)
4. **If condition is false**:
   - Outputs iteration statistics
   - Cleans up iteration counter
   - Completes with `SourcePort = LoopBody` (signals loop exit)

**Critical Design Decisions:**

- **Why Re-Evaluate?**: Child nodes modify workflow variables (e.g., increment counter). The condition MUST check the updated values.
- **Why Feedback Loop?**: Allows child nodes to complete before next iteration begins, ensuring proper sequencing.
- **Why Iteration Counter?**: Tracks state across multiple WhileNode executions, enabling the feedback pattern.

**Output Data (Available to Downstream Nodes):**

After loop completion, the WhileNode sets the following in its output data:
- `IterationCount`: Total number of iterations executed
- `Condition`: The condition expression used
- `MaxIterations`: The maximum iteration limit

**YAML Workflow Example:**

```yaml
workflowId: counter-loop
workflowName: Counter Loop Example
description: Demonstrates while loop with counter increment

nodes:
  - nodeId: initialize-counter
    nodeName: Initialize Counter
    runtimeType: CSharpScript
    configuration:
      script: |
        SetGlobal("counter", 0);
        SetOutput("initialized", true);
    description: Initializes counter to 0

  - nodeId: while-counter
    nodeName: While Counter < 5
    runtimeType: While
    configuration:
      Condition: "GetGlobal(\"counter\") < 5"
      MaxIterations: 10
    description: Loops while counter is less than 5

  - nodeId: increment-counter
    nodeName: Increment Counter
    runtimeType: CSharpScript
    configuration:
      script: |
        var counter = (int)GetGlobal("counter");
        var iteration = (int)GetInput("iterationIndex");

        Console.WriteLine($"Iteration {iteration}: Counter = {counter}");

        // Increment counter (this affects next condition evaluation)
        SetGlobal("counter", counter + 1);

        SetOutput("newCounter", counter + 1);
    description: Increments counter and logs progress

  - nodeId: log-completion
    nodeName: Log Completion
    runtimeType: CSharpScript
    configuration:
      script: |
        var iterationCount = (int)GetInput("IterationCount");
        var finalCounter = (int)GetGlobal("counter");
        Console.WriteLine($"Loop completed after {iterationCount} iterations. Final counter: {finalCounter}");
    description: Logs completion after loop exits

connections:
  # Initialize counter before loop
  - sourceNodeId: initialize-counter
    targetNodeId: while-counter
    triggerMessageType: Complete

  # Connect While to child node using LoopBody port and Next message
  - sourceNodeId: while-counter
    targetNodeId: increment-counter
    sourcePort: LoopBody
    triggerMessageType: Next  # ← Key: While emits Next messages for iterations

  # Feedback: Child completes → WhileNode re-evaluates condition
  - sourceNodeId: increment-counter
    targetNodeId: while-counter
    triggerMessageType: Complete  # ← Feedback loop: triggers next iteration check

  # Loop exit: While completes → Log node
  - sourceNodeId: while-counter
    targetNodeId: log-completion
    triggerMessageType: Complete
```

**Advanced Example: Polling with Timeout:**

```yaml
nodes:
  - nodeId: initialize-polling
    nodeName: Initialize Polling
    runtimeType: CSharpScript
    configuration:
      script: |
        SetGlobal("fileReady", false);
        SetGlobal("pollCount", 0);
        SetGlobal("maxPolls", 30);  // 5 minutes with 10s intervals

  - nodeId: poll-while-not-ready
    nodeName: Poll While File Not Ready
    runtimeType: While
    configuration:
      Condition: "!GetGlobal(\"fileReady\") && GetGlobal(\"pollCount\") < GetGlobal(\"maxPolls\")"
      MaxIterations: 100
    description: Polls until file is ready or timeout

  - nodeId: check-file
    nodeName: Check File Status
    runtimeType: CSharpScript
    configuration:
      script: |
        var pollCount = (int)GetGlobal("pollCount");
        SetGlobal("pollCount", pollCount + 1);

        // Check if file exists and is ready
        var filePath = (string)GetGlobal("watchFile");
        var fileReady = File.Exists(filePath) && !IsFileLocked(filePath);
        SetGlobal("fileReady", fileReady);

        if (!fileReady) {
            Console.WriteLine($"Poll {pollCount + 1}: File not ready. Waiting...");
            await Task.Delay(10000);  // Wait 10 seconds
        } else {
            Console.WriteLine($"File ready after {pollCount + 1} polls!");
        }

connections:
  - sourceNodeId: initialize-polling
    targetNodeId: poll-while-not-ready
    triggerMessageType: Complete

  - sourceNodeId: poll-while-not-ready
    targetNodeId: check-file
    sourcePort: LoopBody
    triggerMessageType: Next

  - sourceNodeId: check-file
    targetNodeId: poll-while-not-ready
    triggerMessageType: Complete  # Feedback loop
```

**C# Code Example (Programmatic Definition):**

```csharp
var workflow = new WorkflowDefinition
{
    WorkflowId = "while-loop-example",
    Name = "While Loop Example",
    Nodes = new List<NodeDefinition>
    {
        new NodeDefinition
        {
            NodeId = "while-node",
            NodeName = "While Counter < 10",
            RuntimeType = RuntimeType.While,
            Configuration = new Dictionary<string, object>
            {
                { "Condition", "GetGlobal(\"counter\") < 10" },
                { "MaxIterations", 50 }
            }
        },
        new NodeDefinition
        {
            NodeId = "loop-body",
            NodeName = "Increment Counter",
            RuntimeType = RuntimeType.CSharpScript,
            Configuration = new Dictionary<string, object>
            {
                { "Script", @"
                    var counter = (int)GetGlobal(""counter"");
                    SetGlobal(""counter"", counter + 1);
                " }
            }
        }
    },
    Connections = new List<NodeConnection>
    {
        new NodeConnection
        {
            SourceNodeId = "while-node",
            TargetNodeId = "loop-body",
            SourcePort = "LoopBody",
            TriggerMessageType = "Next"
        },
        new NodeConnection
        {
            SourceNodeId = "loop-body",
            TargetNodeId = "while-node",
            TriggerMessageType = "Complete"  // Feedback connection
        }
    }
};
```

**Error Handling:**

The node handles several error cases:
- **Null/Empty Condition**: Throws `InvalidOperationException` if Condition is not defined
- **Compilation Errors**: Returns detailed compilation diagnostics if the C# expression has syntax errors
- **Runtime Errors**: Catches exceptions during evaluation and sets node status to Failed
- **Max Iterations Exceeded**: Throws `InvalidOperationException` with infinite loop warning
- **Non-Boolean Result**: Throws `InvalidOperationException` if condition doesn't return boolean

**Key Features:**

1. **Dynamic Condition Re-Evaluation**: Condition checked before each iteration with updated variable values
2. **Feedback Loop Architecture**: Child nodes trigger next iteration via Complete message
3. **Infinite Loop Protection**: Configurable max iterations with safety limits
4. **Stateful Execution**: Tracks iteration count across multiple node executions
5. **Progress Tracking**: Emits progress events with iteration counts
6. **Automatic Cleanup**: Clears iteration counters on completion or failure
7. **Debugging Support**: Stores iteration statistics in output data

**Implementation File:** `ExecutionEngine/Nodes/WhileNode.cs`

---

##### Switch Node

The `SwitchNode` implements multi-way branching in workflows using **SourcePort-based routing**. It evaluates a C# expression and routes execution to different output ports based on matching case values, similar to a C# switch statement.

**Design Philosophy:**

The SwitchNode provides **value-based routing** where:

1. **Expression Evaluation**: Evaluates a C# expression once to get a value
2. **Case Matching**: Compares the result against defined case values
3. **Port Selection**: Routes to the matching case's port or the default port
4. **String-Based Matching**: All case values are compared as strings for consistency

**Implementation:**

The SwitchNode evaluates an expression and routes based on the result:

**Configuration Properties:**

- **Expression** (string, required): C# expression to evaluate
- **Cases** (Dictionary<string, string>, required): Map of case values to port names
  - **Key**: The value to match (e.g., "success", "failure", "pending")
  - **Value**: The port name to route to (if empty, uses key as port name)

**Expression Evaluation Context:**

The expression has access to:
- **Global Variables**: `GetGlobal("variableName")` - workflow-level variables
- **Local Variables**: `Local["variableName"]` - node-scoped variables
- **Input Data**: `GetInput("key")` - data from upstream nodes
- **Output Data**: `Output` - current node's output dictionary
- **.NET Libraries**: Full C# syntax with System, System.Linq, System.Collections.Generic

**Routing Behavior:**

When executed, the SwitchNode:
1. Evaluates the expression to get a result value
2. Converts the result to a string
3. Compares the string against each case key (case-sensitive, ordinal comparison)
4. If a match is found, sets `SourcePort` to the corresponding case port name
5. If no match is found, sets `SourcePort` to "Default"
6. Stores the result and matched case in output data

**Output Data (Available to Downstream Nodes):**

After execution, the SwitchNode sets the following in its output data:
- `ExpressionResult`: The string value of the expression result
- `MatchedCase`: The case that was matched (or "Default")
- `PortSelected`: The port name selected for routing

**YAML Workflow Example:**

```yaml
workflowId: status-based-routing
workflowName: Status-Based Routing
description: Demonstrates switch routing based on task status

nodes:
  - nodeId: check-status
    nodeName: Check Task Status
    runtimeType: CSharpScript
    configuration:
      script: |
        // Simulate checking task status
        var status = GetGlobal("taskStatus") ?? "pending";
        SetOutput("status", status);
        return status;
    description: Checks current task status

  - nodeId: route-by-status
    nodeName: Route By Status
    runtimeType: Switch
    configuration:
      Expression: "(string)GetInput(\"status\")"
      Cases:
        success: SuccessPort
        failure: FailurePort
        pending: PendingPort
        timeout: TimeoutPort
    description: Routes based on status value

  - nodeId: handle-success
    nodeName: Handle Success
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Task completed successfully!");
        SetOutput("handled", true);

  - nodeId: handle-failure
    nodeName: Handle Failure
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Task failed. Initiating retry...");
        SetOutput("retry", true);

  - nodeId: handle-pending
    nodeName: Handle Pending
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Task still pending. Waiting...");

  - nodeId: handle-timeout
    nodeName: Handle Timeout
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Task timed out. Escalating...");

  - nodeId: handle-default
    nodeName: Handle Unknown Status
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Unknown status. Manual intervention required.");

connections:
  - sourceNodeId: check-status
    targetNodeId: route-by-status
    triggerMessageType: Complete

  # Connect each case port to its handler
  - sourceNodeId: route-by-status
    targetNodeId: handle-success
    sourcePort: SuccessPort
    triggerMessageType: Complete

  - sourceNodeId: route-by-status
    targetNodeId: handle-failure
    sourcePort: FailurePort
    triggerMessageType: Complete

  - sourceNodeId: route-by-status
    targetNodeId: handle-pending
    sourcePort: PendingPort
    triggerMessageType: Complete

  - sourceNodeId: route-by-status
    targetNodeId: handle-timeout
    sourcePort: TimeoutPort
    triggerMessageType: Complete

  # Default case for unmatched values
  - sourceNodeId: route-by-status
    targetNodeId: handle-default
    sourcePort: Default
    triggerMessageType: Complete
```

**Advanced Example: Numeric Range Routing:**

```yaml
nodes:
  - nodeId: categorize-value
    nodeName: Categorize Numeric Value
    runtimeType: Switch
    configuration:
      # Use expression to convert numeric value to category string
      Expression: |
        var value = (int)GetInput("score");
        if (value >= 90) return "excellent";
        else if (value >= 70) return "good";
        else if (value >= 50) return "average";
        else return "poor";
      Cases:
        excellent: ExcellentPort
        good: GoodPort
        average: AveragePort
        poor: PoorPort
    description: Categorizes numeric score into ranges

  - nodeId: excellent-handler
    nodeName: Excellent Score Handler
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Excellent performance!");
        SetOutput("bonus", 1000);

  - nodeId: good-handler
    nodeName: Good Score Handler
    runtimeType: CSharpScript
    configuration:
      script: |
        Console.WriteLine("Good job!");
        SetOutput("bonus", 500);
```

**Advanced Example: Type-Based Routing:**

```yaml
nodes:
  - nodeId: route-by-type
    nodeName: Route By File Type
    runtimeType: Switch
    configuration:
      Expression: "System.IO.Path.GetExtension((string)GetInput(\"filePath\")).ToLower()"
      Cases:
        .etl: EtlPort
        .evtx: EvtxPort
        .csv: CsvPort
        .json: JsonPort
    description: Routes based on file extension

  - nodeId: process-etl
    nodeName: Process ETL File
    runtimeType: CSharpScript
    # ... ETL processing logic

  - nodeId: process-evtx
    nodeName: Process EVTX File
    runtimeType: CSharpScript
    # ... EVTX processing logic

connections:
  - sourceNodeId: route-by-type
    targetNodeId: process-etl
    sourcePort: EtlPort
    triggerMessageType: Complete

  - sourceNodeId: route-by-type
    targetNodeId: process-evtx
    sourcePort: EvtxPort
    triggerMessageType: Complete
```

**C# Code Example (Programmatic Definition):**

```csharp
var workflow = new WorkflowDefinition
{
    WorkflowId = "switch-routing",
    Name = "Switch Routing Example",
    Nodes = new List<NodeDefinition>
    {
        new NodeDefinition
        {
            NodeId = "switch-node",
            NodeName = "Route By Status",
            RuntimeType = RuntimeType.Switch,
            Configuration = new Dictionary<string, object>
            {
                { "Expression", "(string)GetInput(\"status\")" },
                { "Cases", new Dictionary<string, string>
                    {
                        { "success", "SuccessPort" },
                        { "failure", "FailurePort" },
                        { "pending", "PendingPort" }
                    }
                }
            }
        },
        new NodeDefinition
        {
            NodeId = "success-handler",
            NodeName = "Handle Success",
            RuntimeType = RuntimeType.CSharpScript,
            Configuration = new Dictionary<string, object>
            {
                { "Script", "Console.WriteLine(\"Success!\");" }
            }
        }
    },
    Connections = new List<NodeConnection>
    {
        new NodeConnection
        {
            SourceNodeId = "switch-node",
            TargetNodeId = "success-handler",
            SourcePort = "SuccessPort",
            TriggerMessageType = "Complete"
        }
    }
};
```

**Error Handling:**

The node handles several error cases:
- **Null/Empty Expression**: Throws `InvalidOperationException` if Expression is not defined
- **Compilation Errors**: Returns detailed compilation diagnostics if the C# expression has syntax errors
- **Runtime Errors**: Catches exceptions during evaluation and sets node status to Failed
- **Null Result**: Converts null results to empty string for comparison

**Key Features:**

1. **Full C# Expression Support**: Uses Roslyn scripting engine for powerful expression evaluation
2. **Multi-Way Branching**: Supports unlimited number of case branches
3. **Default Port**: Always provides a default route for unmatched values
4. **Type Flexibility**: Converts any expression result to string for matching
5. **Case-Sensitive Matching**: Uses ordinal string comparison for precise matching
6. **Dynamic Port Names**: Port names can be customized via case values
7. **Debugging Support**: Stores expression result and matched case in output data

**Comparison with If-Else:**

| Feature | If-Else Node | Switch Node |
|---------|--------------|-------------|
| Branches | 2 (True/False) | Unlimited (Cases + Default) |
| Condition | Boolean expression | Value expression |
| Matching | True/False | String equality |
| Use Case | Binary decisions | Multi-way routing |
| Performance | Single evaluation | Single evaluation + linear search |

**Implementation File:** `ExecutionEngine/Nodes/SwitchNode.cs`

---

#### 3.4.5 Timer Node (Scheduled Trigger)

The `TimerNode` is a specialized trigger node that initiates workflow execution based on cron schedule expressions. It acts as an entry point for time-based automated workflows.

**Design Philosophy:**

Timer nodes differ from task nodes in several key ways:

1. **Trigger vs. Task**: Timer nodes initiate workflows rather than process data
2. **State Preservation**: Maintains last trigger time across executions to prevent duplicate triggers
3. **Passive Scheduling**: Evaluates schedule on each execution rather than active background timers
4. **Conditional Completion**: Completes successfully whether it triggers or not, outputting trigger status

**Implementation:**

The TimerNode uses the NCrontab library for cron expression parsing and supports standard cron syntax:

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
│ │ │ │ │
* * * * *
```

**Configuration:**

- **Schedule** (string, required): Cron expression defining trigger schedule
- **TriggerOnStart** (boolean, optional): If true, triggers immediately on first execution. Default: false

**Execution Behavior:**

On each execution, the TimerNode:
1. Checks if this is the first execution and `TriggerOnStart` is enabled
2. Calculates the next occurrence based on the cron schedule
3. Compares next occurrence to current time
4. If time has passed, updates `lastTrigger` and outputs `Triggered=true`
5. If time hasn't passed, outputs `Triggered=false` and next trigger time

**Output Data:**

When triggered (`Triggered=true`):
- `TriggerTime`: DateTime of the trigger
- `Schedule`: The cron expression used
- `Triggered`: true

When not triggered (`Triggered=false`):
- `NextTriggerTime`: DateTime of next scheduled occurrence
- `Triggered`: false

**Example Usage:**

```yaml
nodes:
  - nodeId: daily-trigger
    nodeName: Daily ETL Trigger
    runtimeType: Timer
    configuration:
      Schedule: "0 2 * * *"  # 2 AM daily
      TriggerOnStart: false
    description: Triggers ETL workflow at 2 AM every day

  - nodeId: hourly-check
    nodeName: Hourly Health Check
    runtimeType: Timer
    configuration:
      Schedule: "0 * * * *"  # Every hour
      TriggerOnStart: true   # Run immediately on startup
    description: Triggers health check every hour

  - nodeId: weekday-morning
    nodeName: Weekday Morning Report
    runtimeType: Timer
    configuration:
      Schedule: "0 8 * * 1-5"  # 8 AM Monday-Friday
      TriggerOnStart: false
    description: Triggers report generation on weekday mornings

  - nodeId: process-files
    nodeName: Process Files
    runtimeType: CSharpScript
    scriptPath: scripts/process-files.csx
    description: Processes files when triggered

connections:
  - sourceNodeId: daily-trigger
    targetNodeId: process-files
    triggerMessageType: Complete
    # Only process if timer actually triggered
    condition: "GetInput(\"Triggered\") == true"
    isEnabled: true
```

**Common Cron Schedules:**

| Schedule | Description |
|----------|-------------|
| `* * * * *` | Every minute |
| `0 * * * *` | Every hour |
| `0 0 * * *` | Daily at midnight |
| `0 2 * * *` | Daily at 2 AM |
| `0 0 * * 0` | Weekly on Sunday at midnight |
| `0 0 1 * *` | Monthly on the 1st at midnight |
| `0 9 * * 1-5` | Weekdays at 9 AM |
| `*/15 * * * *` | Every 15 minutes |
| `0 */6 * * *` | Every 6 hours |

**Integration with Workflow Engine:**

Timer nodes are typically used in one of two patterns:

**Pattern 1: Polling Loop (Recommended)**
```
┌─────────────────────────────────────────┐
│ Workflow Engine Main Loop (while true) │
└──────────────┬──────────────────────────┘
               │
               ▼
        ┌─────────────┐
        │ Timer Node  │
        │  Evaluate   │
        └──────┬──────┘
               │
    ┌──────────┴──────────┐
    │                     │
    ▼                     ▼
Triggered=true      Triggered=false
    │                     │
    ▼                     │
┌─────────┐               │
│Execute  │               │
│Workflow │               │
└─────────┘               │
    │                     │
    └──────────┬──────────┘
               │
               ▼
         Sleep briefly
               │
               └──> Loop back to Timer Node
```

In this pattern, the workflow engine continuously evaluates timer nodes, and they complete quickly with `Triggered=true/false`. Downstream nodes use a condition to only execute when `Triggered=true`.

**Pattern 2: Event-Driven (Future Enhancement)**
```
External scheduler daemon → Sends StartMessage to Timer Node → Workflow executes
```

**Error Handling:**

- **Invalid Schedule**: Throws `InvalidOperationException` during `Initialize()` if cron expression is malformed
- **Null Schedule**: Throws `InvalidOperationException` during `ExecuteAsync()` if Schedule is not configured
- **Uninitialized Schedule**: Throws `InvalidOperationException` if crontabSchedule failed to parse

**State Management:**

The TimerNode maintains:
- `lastTrigger`: DateTime of last successful trigger (in-memory, not persisted)
- `crontabSchedule`: Parsed CrontabSchedule instance (initialized once)

**Limitations:**

- **No Persistence**: `lastTrigger` is not persisted across workflow engine restarts
- **No Time Zones**: Uses local system time (DateTime.Now), not UTC or timezone-aware
- **No Overlapping Prevention**: Does not prevent workflows from overlapping if execution takes longer than schedule interval

**Future Enhancements:**

1. Persist `lastTrigger` to prevent duplicate triggers after restarts
2. Add timezone support for distributed deployments
3. Add `AllowOverlap` configuration to prevent concurrent workflow executions
4. Add `MaxMissedTriggers` to handle catch-up scenarios after downtime

**Implementation File:** `ExecutionEngine/Nodes/TimerNode.cs`

**Dependencies:**
- NCrontab (NuGet package): Cron expression parsing and scheduling

---

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
