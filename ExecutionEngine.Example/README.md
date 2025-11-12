# ExecutionEngine.Example

This console application demonstrates the WorkflowEngine with different node types and workflow patterns.

## Project Structure

```
ExecutionEngine.Example/
├── Nodes/
│   ├── LogNode.cs              # Simple logging node
│   ├── DataProcessorNode.cs    # Data transformation node
│   └── AggregatorNode.cs       # Results aggregation node
├── Workflows/
│   ├── SimpleSequentialWorkflow.cs  # Sequential execution pattern
│   ├── ParallelWorkflow.cs          # Parallel execution pattern
│   └── FanOutWorkflow.cs            # Fan-out/fan-in pattern (has known issues)
├── Program.cs                   # Main entry point
└── README.md                    # This file
```

## Running the Examples

```bash
cd ExecutionEngine.Example
dotnet build
dotnet run
```

## Example Workflows

### 1. Simple Sequential Workflow

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

### 2. Parallel Processing Workflow

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
