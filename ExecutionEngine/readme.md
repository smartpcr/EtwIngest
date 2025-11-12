# ExecutionEngine Design Document

## Overview

The ExecutionEngine is a workflow orchestration system that executes directed acyclic graphs (DAGs) of nodes connected by message-based dependencies.

## Design Requirements

1. ✅ **Graph Structure**: Execution plan is a graph containing nodes and connectors
2. ⚠️ **Workflow Instances**: Execution creates workflow instance, triggered by special nodes (timer/manual) - *Trigger nodes planned for Phase 1.9*
3. ✅ **Execution Context**: Graph execution has ExecutionContext with global variables and message queue via channels
4. ✅ **Strong-Typed Messages**: Messages are strong-typed and produced by each node
5. ✅ **Node Interface**: Each node implements `Task ExecuteAsync(CancellationToken)`
6. ✅ **Node Events**: Nodes produce OnStart, OnProgress events
7. ✅ **Node Messages**: Nodes produce OnComplete, OnFail messages enqueued to queue
8. ✅ **Graph Edges**: OnComplete/OnFail connect to next nodes via channel subscriptions (edges define dependencies)
9. ⚠️ **Subflow Nodes**: Special node for nested workflow execution - *Planned for Phase 1.7*
10. ⚠️ **Control Flow Nodes**: if-else, switch, while/foreach loop nodes - *Planned for Phase 1.7*
11. ✅ **Graph Validation**: Graph traversal validates no infinite loops (cycle detection)
12. ✅ **Serialization**: Graph definition in JSON and YAML files
13. ✅ **Script Execution**: Node execution supports C# code and PowerShell scripts
14. ⚠️ **State Persistence**: Execution state persisted to file - *Planned for Phase 1.8*
15. ⚠️ **Execution Control**: Pause, resume, cancel support - *Planned for Phase 1.8*

## Implementation Status

### Phase 1: Core Infrastructure ✅ **COMPLETE**

#### Phase 1.1: Core Models ✅
- ✅ NodeInstance - tracks execution results
- ✅ NodeExecutionContext - node-level state (input/output data)
- ✅ WorkflowExecutionContext - workflow-level state (global variables)
- ✅ ExecutionState - helper for script access to context
- ✅ NodeExecutionStatus enum (Pending, Running, Completed, Failed, Cancelled)
- ✅ WorkflowExecutionStatus enum
- ✅ NodeStartEventArgs, ProgressEventArgs
- ✅ 100% test coverage

#### Phase 1.2: Message Infrastructure ✅
- ✅ MessageType enum (Complete, Fail, Progress)
- ✅ INodeMessage interface
- ✅ NodeCompleteMessage
- ✅ NodeFailMessage
- ✅ ProgressMessage
- ✅ 100% test coverage

#### Phase 1.3: Message Queue and Routing ✅
- ✅ CircularBuffer - lock-free ring buffer for message storage
- ✅ MessageEnvelope - message wrapper with lease management
- ✅ NodeMessageQueue - channel-based message queue
- ✅ DeadLetterQueue - failed message handling
- ✅ MessageRouter - message routing infrastructure
- ✅ 100% test coverage

#### Phase 1.4: Node Factory and Execution ✅
- ✅ NodeDefinition - node configuration model
- ✅ NodeFactory - dynamic node creation with runtime type routing
- ✅ INode interface
- ✅ ExecutableNodeBase - base class with event raising
- ✅ CSharpScriptNode - Roslyn-based C# script execution
- ✅ PowerShellScriptNode - PowerShell script execution
- ✅ Assembly loading with type caching
- ✅ 100% test coverage (platform-specific handling for PowerShell)

#### Phase 1.5: Workflow Definition and Serialization ✅
- ✅ WorkflowDefinition - graph structure model
- ✅ NodeConnection - edge/dependency model with message triggers
- ✅ WorkflowValidator - graph validation (cycle detection, entry points)
- ✅ ValidationResult - errors and warnings
- ✅ WorkflowSerializer - JSON and YAML serialization
- ✅ WorkflowLoader - load/save with integrated validation
- ✅ Sample workflows (JSON and YAML)
- ✅ 100% test coverage

**Total Phase 1 Tests**: 269 (262 passing, 7 skipped platform-specific)
**Phase 1 Coverage**: 91.9% overall

### Phase 2.1: NodeMessageQueue and Lease Management ✅ **COMPLETE**
- ✅ MessageLease - message lease wrapper with expiration tracking
- ✅ NodeMessageQueue - lease-based queue with visibility timeout pattern
- ✅ CircularBuffer - automatic expired lease reaping during checkout (self-healing)
- ✅ Visibility timeout pattern (messages invisible while leased)
- ✅ Max retries with exponential backoff
- ✅ Dead letter queue integration for failed messages
- ✅ **Lease expiration model**: expired = handler crashed/hung → automatic requeue

**Phase 2.1 Tests**: 18 tests for NodeMessageQueue (289 total, 282 passing, 7 skipped)
**Phase 2.1 Coverage**: 91.4% overall | MessageLease: 100% | NodeMessageQueue: 100% | CircularBuffer: 100%

### Phase 2.2: Basic Workflow Orchestration ✅ **COMPLETE**
- ✅ WorkflowEngine - core orchestration and lifecycle management
- ✅ Message-driven node execution (each node executes once per trigger)
- ✅ Entry point detection (nodes with no incoming connections)
- ✅ NodeInstance tracking (Pending → Running → Completed/Failed/Cancelled)
- ✅ Basic message routing via MessageRouter
- ✅ Parallel node execution with Task-based concurrency
- ✅ Workflow completion detection and status aggregation
- ✅ Timeout handling (10s node timeout, 30s workflow timeout)
- ✅ Error collection and reporting (__node_errors in context)

**Phase 2.2 Tests**: 10 WorkflowEngine tests (288 total, 279 passing, 7 skipped, 2 known issues)
**Phase 2.2 Coverage**: 91.4% overall | WorkflowEngine: 100%

### Phase 2.3: Conditional Routing & Message Filtering 🔄 **PLANNED**
**Focus**: Smart message routing based on node outcomes and conditions

- [ ] **Conditional Connections**: Route messages based on node output
  - [ ] `NodeConnection.Condition` property (expression-based)
  - [ ] Route only if condition evaluates to true
  - [ ] Support for output data inspection (e.g., `output.status == "success"`)

- [ ] **Message Type Filtering**: Route specific message types
  - [ ] `NodeConnection.TriggerMessageType` filtering (Complete vs Fail)
  - [ ] Subscribe to specific message types only
  - [ ] Default to Complete messages if not specified

- [ ] **Output Port Routing**: Named outputs from nodes
  - [ ] Multiple output ports per node (e.g., "success", "error", "timeout")
  - [ ] `NodeConnection.SourcePort` and `TargetPort` properties
  - [ ] Default port for simple scenarios

**Key Use Cases**: Error handling branches, conditional workflows, multi-outcome nodes

### Phase 2.4: Reactive State Management 🔄 **PLANNED**
**Focus**: Observable state changes and reactive updates

- [ ] **State Change Events**: Publish state transitions
  - [ ] `IObservable<StateChangeEvent>` pattern
  - [ ] Subscribe to workflow state changes
  - [ ] Subscribe to node state changes
  - [ ] Granular event types (NodeStarted, NodeCompleted, WorkflowFailed, etc.)

- [ ] **Progress Tracking**: Real-time execution progress
  - [ ] Calculate completion percentage
  - [ ] Track active/completed/failed node counts
  - [ ] Estimated time remaining
  - [ ] Progress events stream

- [ ] **Live Query**: Query runtime state
  - [ ] Get current node statuses
  - [ ] Get workflow execution graph state
  - [ ] Get node input/output data
  - [ ] Performance metrics (execution time, queue depth)

**Key Use Cases**: UI updates, monitoring dashboards, debugging, logging

### Phase 2.5: State Persistence & Recovery 🔄 **PLANNED**
**Focus**: Checkpoint, pause, resume, and failure recovery

- [ ] **Checkpointing**: Save workflow state at key points
  - [ ] Automatic checkpoint after each node completion
  - [ ] Manual checkpoint trigger
  - [ ] Configurable checkpoint frequency
  - [ ] Checkpoint storage abstraction (memory/file/database)

- [ ] **Pause/Resume**: Stop and restart workflows
  - [ ] Pause workflow execution
  - [ ] Resume from paused state
  - [ ] Preserve message queues across pause/resume
  - [ ] Time-limited pause with auto-resume

- [ ] **Failure Recovery**: Recover from crashes
  - [ ] Detect incomplete workflows on startup
  - [ ] Reconstruct workflow state from checkpoints
  - [ ] Resume from last successful checkpoint
  - [ ] Handle partial node execution

**Key Use Cases**: Long-running workflows, crash recovery, maintenance windows

### Phase 2.6: MaxConcurrency & Resource Management 🔄 **PLANNED**
**Focus**: Control parallel execution and resource limits

- [ ] **Workflow-Level Concurrency**: Limit concurrent nodes
  - [ ] `WorkflowDefinition.MaxConcurrency` enforcement
  - [ ] Queue nodes when limit reached
  - [ ] Priority-based scheduling
  - [ ] Resource pool management

- [ ] **Node-Level Throttling**: Rate limit specific nodes
  - [ ] `NodeDefinition.MaxConcurrentExecutions`
  - [ ] Per-node execution slots
  - [ ] Backpressure handling
  - [ ] Dynamic throttling based on load

- [ ] **Resource Quotas**: CPU, memory, I/O limits
  - [ ] Resource allocation per node
  - [ ] Resource tracking and enforcement
  - [ ] Resource starvation detection
  - [ ] Fair scheduling algorithms

**Key Use Cases**: Resource-constrained environments, cost control, preventing overload

### Phase 2.7: Advanced Error Handling 🔄 **PLANNED**
**Focus**: Retry policies, compensation, and resilience patterns

- [ ] **Retry Policies**: Configurable retry strategies
  - [ ] `NodeDefinition.RetryPolicy` configuration
  - [ ] Exponential backoff
  - [ ] Jitter for retry timing
  - [ ] Max retry attempts per node
  - [ ] Conditional retry (retry only on specific errors)

- [ ] **Compensation Logic**: Undo operations on failure
  - [ ] Compensation node definition
  - [ ] Automatic rollback on workflow failure
  - [ ] Partial compensation for failed branches
  - [ ] Compensation execution order (reverse of success path)

- [ ] **Circuit Breaker**: Prevent cascading failures
  - [ ] Circuit breaker per node type
  - [ ] Open/half-open/closed states
  - [ ] Failure threshold configuration
  - [ ] Reset timeout
  - [ ] Fallback behavior when circuit open

**Key Use Cases**: Transient failure handling, distributed system resilience, saga patterns

### Phase 3: Control Flow and Advanced Nodes 🔄 **PLANNED**

#### Phase 1.7: Control Flow Nodes
- [ ] IfElseNode - conditional branching
- [ ] SwitchNode - multi-way branching
- [ ] WhileLoopNode - conditional loops
- [ ] ForEachLoopNode - collection iteration
- [ ] SubflowNode - nested workflow execution

#### Phase 1.8: State Persistence
- [ ] WorkflowInstanceSerializer - serialize runtime state
- [ ] Pause/Resume support
- [ ] Checkpoint creation
- [ ] State recovery from checkpoints
- [ ] Cancellation support

#### Phase 1.9: Trigger Nodes
- [ ] TimerNode - schedule-based execution
- [ ] ManualTriggerNode - on-demand execution
- [ ] EventTriggerNode - event-based triggers

### Phase 4: Advanced Features 🔄 **PLANNED**
- [ ] Workflow versioning
- [ ] Workflow migration
- [ ] Execution history and audit trail
- [ ] Performance monitoring and metrics
- [ ] Distributed execution support

## Architecture

### Component Hierarchy

```
ExecutionEngine
├── Core/              # Base models and interfaces
│   ├── INode
│   ├── NodeInstance
│   ├── ExecutionState
│   └── Event Args
├── Contexts/          # Execution contexts
│   ├── WorkflowExecutionContext
│   └── NodeExecutionContext
├── Factory/           # Node creation
│   ├── NodeDefinition
│   └── NodeFactory
├── Nodes/             # Node implementations
│   ├── ExecutableNodeBase
│   ├── CSharpScriptNode
│   └── PowerShellScriptNode
├── Messages/          # Message types
│   ├── INodeMessage
│   └── Message implementations
├── Queue/             # Message queuing
│   ├── CircularBuffer
│   ├── NodeMessageQueue
│   └── DeadLetterQueue
├── Routing/           # Message routing
│   └── MessageRouter
└── Workflow/          # Workflow definition
    ├── WorkflowDefinition
    ├── NodeConnection
    ├── WorkflowValidator
    ├── WorkflowSerializer
    └── WorkflowLoader
```

### Key Design Patterns

- **Factory Pattern**: NodeFactory creates nodes from definitions
- **Strategy Pattern**: Different node types (CSharp, PowerShell, Script)
- **Observer Pattern**: Event-based node lifecycle (OnStart, OnProgress)
- **Message Queue Pattern**: Async message passing between nodes
- **Directed Graph**: Workflow as DAG with nodes and edges

## Sample Workflow

```yaml
workflowId: data-pipeline
workflowName: Data Processing Pipeline
nodes:
  - nodeId: fetch
    runtimeType: PowerShell
    scriptPath: scripts/fetch.ps1
  - nodeId: process
    runtimeType: CSharpScript
    scriptPath: scripts/process.csx
  - nodeId: save
    runtimeType: CSharpScript
    scriptPath: scripts/save.csx
connections:
  - sourceNodeId: fetch
    targetNodeId: process
    triggerMessageType: Complete
  - sourceNodeId: process
    targetNodeId: save
    triggerMessageType: Complete
```

## Next Steps

1. **Phase 2**: Implement WorkflowEngine for actual execution orchestration
2. **Phase 1.7**: Add control flow nodes (if-else, switch, loops)
3. **Phase 1.8**: Add state persistence and pause/resume
4. **Phase 1.9**: Add trigger nodes (timer, manual, event)

See `IMPLEMENTATION_PLAN.md` for detailed phase breakdown and progress tracking.