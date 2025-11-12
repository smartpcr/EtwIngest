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

**Core Deliverables**:
1. **Conditional Connections**: Route messages based on node output
   - Add `NodeConnection.Condition` property (string expression)
   - Implement expression evaluator (simple property access: `output.status == "success"`)
   - Update `MessageRouter.RouteMessageAsync()` to evaluate conditions
   - Add `NodeConnection.IsConditionMet()` method

2. **Message Type Filtering**: Route specific message types
   - Add `NodeConnection.TriggerMessageType` enum property
   - Default: `MessageType.Complete | MessageType.Fail` (all)
   - Filter messages in `MessageRouter` before routing
   - Support bitwise flags for multiple types

3. **Output Port Routing**: Named outputs from nodes
   - Add `NodeConnection.SourcePort` and `TargetPort` properties (optional)
   - Update `INode` interface with `GetAvailablePorts()` method
   - Support default port (null = primary output)
   - Add port metadata to `NodeCompleteMessage`

**Key Files**:
- `Workflow/NodeConnection.cs` - Add Condition, TriggerMessageType, SourcePort, TargetPort
- `Routing/MessageRouter.cs` - Add condition evaluation and filtering
- `Core/INode.cs` - Add port discovery
- `Routing/ConditionEvaluator.cs` - NEW: Simple expression evaluator

**Tests Required**: 15-20 tests covering:
- Conditional routing (true/false conditions)
- Message type filtering (Complete only, Fail only, both)
- Multi-port routing
- Default behavior (no condition/filter)
- Invalid conditions/ports (error handling)

**Dependencies**: Phase 2.2 (MessageRouter infrastructure)

**Success Criteria**:
- All tests passing
- Can route based on node output values
- Can filter by message type
- Can route to named ports
- Backward compatible (existing workflows work)

### Phase 2.4: Reactive State Management 🔄 **PLANNED**
**Focus**: Observable state changes and reactive updates

**Core Deliverables**:
1. **State Change Events**: Publish state transitions
   - Add `WorkflowExecutionContext.Events` property (`IObservable<WorkflowEvent>`)
   - Event types: `WorkflowStarted`, `WorkflowCompleted`, `WorkflowFailed`, `WorkflowCancelled`
   - Event types: `NodeStarted`, `NodeCompleted`, `NodeFailed`, `NodeCancelled`
   - Use `System.Reactive` (Rx.NET) for observable pattern
   - Thread-safe event publishing

2. **Progress Tracking**: Real-time execution progress
   - Add `WorkflowExecutionContext.Progress` property (`IObservable<ProgressUpdate>`)
   - Calculate: `CompletedNodes / TotalNodes * 100`
   - Track: `PendingCount`, `RunningCount`, `CompletedCount`, `FailedCount`, `CancelledCount`
   - Estimate time remaining based on average node execution time
   - Publish progress updates after each node state change

3. **Live Query**: Query runtime state
   - Add `IWorkflowEngine.GetWorkflowStatusAsync()` - get snapshot
   - Add `WorkflowExecutionContext.GetNodeStatus(nodeId)` - get node state
   - Add `WorkflowExecutionContext.GetExecutionGraph()` - visual representation
   - Add metrics: `TotalExecutionTime`, `AverageNodeTime`, `QueueDepth`

**Key Files**:
- `Events/WorkflowEvent.cs` - NEW: Base event class
- `Events/NodeEvent.cs` - NEW: Node-specific events
- `Events/ProgressUpdate.cs` - NEW: Progress snapshot
- `Contexts/WorkflowExecutionContext.cs` - Add Events and Progress observables
- `Engine/IWorkflowEngine.cs` - Add GetWorkflowStatusAsync
- `Engine/WorkflowEngine.cs` - Publish events during execution

**Tests Required**: 20-25 tests covering:
- Event publishing (all event types)
- Event subscription (multiple subscribers)
- Progress calculation accuracy
- Live query during execution
- Performance metrics tracking
- Thread safety of event publishing

**Dependencies**: Phase 2.2 (WorkflowEngine state tracking)

**NuGet Packages**: `System.Reactive` (6.0.0+)

**Success Criteria**:
- All events published correctly
- Progress percentage accurate
- Live queries return current state
- No memory leaks from subscriptions
- Thread-safe event handling
- Performance overhead <5%

### Phase 2.5: State Persistence & Recovery 🔄 **PLANNED**
**Focus**: Checkpoint, pause, resume, and failure recovery

**Core Deliverables**:
1. **Checkpointing**: Save workflow state at key points
   - Add `ICheckpointStorage` interface (Save, Load, Delete, List)
   - Implement `InMemoryCheckpointStorage` (for testing)
   - Implement `FileCheckpointStorage` (JSON files)
   - Serialize: `WorkflowExecutionContext`, node states, message queues
   - Checkpoint frequency: `AfterEachNode`, `AfterNNodes`, `TimeInterval`, `Manual`
   - Add `WorkflowEngine.CreateCheckpointAsync()`

2. **Pause/Resume**: Stop and restart workflows
   - Implement `IWorkflowEngine.PauseAsync()` - stop accepting new messages
   - Implement `IWorkflowEngine.ResumeAsync()` - restore from checkpoint
   - Add `WorkflowExecutionStatus.Paused` state
   - Preserve CircularBuffer state (serialize Ready + InFlight messages)
   - Support timeout on pause (auto-resume after N seconds)
   - Graceful node completion before pause (wait for running nodes)

3. **Failure Recovery**: Recover from crashes
   - Add `IWorkflowEngine.RecoverIncompleteWorkflowsAsync()` at startup
   - Detect workflows with status `Running` or `Paused` in checkpoint store
   - Reconstruct `WorkflowExecutionContext` from last checkpoint
   - Restore CircularBuffer and message queues
   - Replay from last completed node
   - Handle: node was InFlight → requeue with retry++

**Key Files**:
- `Persistence/ICheckpointStorage.cs` - NEW: Storage abstraction
- `Persistence/InMemoryCheckpointStorage.cs` - NEW: In-memory implementation
- `Persistence/FileCheckpointStorage.cs` - NEW: File-based implementation
- `Persistence/WorkflowCheckpoint.cs` - NEW: Serializable snapshot
- `Engine/IWorkflowEngine.cs` - Add PauseAsync, ResumeAsync, RecoverAsync
- `Engine/WorkflowEngine.cs` - Implement checkpoint logic
- `Enums/CheckpointFrequency.cs` - NEW: Checkpoint trigger options

**Tests Required**: 25-30 tests covering:
- Checkpoint creation and restoration
- Pause/Resume workflow
- Crash recovery (simulate engine restart)
- Message queue preservation
- Partial node execution handling
- Checkpoint storage implementations
- Concurrent checkpoint access

**Dependencies**:
- Phase 2.2 (WorkflowEngine)
- Phase 2.4 (State tracking for serialization)

**NuGet Packages**: `System.Text.Json` (for serialization)

**Success Criteria**:
- Can checkpoint at any point
- Can pause and resume without data loss
- Can recover from crashes
- Checkpoint storage is pluggable
- Serialization is efficient (<100ms for typical workflow)
- No message loss during pause/resume
- Backward compatible checkpoints (version tolerance)

### Phase 2.6: MaxConcurrency & Resource Management 🔄 **PLANNED**
**Focus**: Control parallel execution and resource limits

**Core Deliverables**:
1. **Workflow-Level Concurrency**: Limit concurrent nodes
   - Add `WorkflowDefinition.MaxConcurrency` property (default: unlimited)
   - Implement concurrency semaphore in `WorkflowEngine`
   - Queue nodes when concurrency limit reached
   - Add `NodeDefinition.Priority` (High/Normal/Low) for scheduling
   - Track: `ActiveNodeCount`, `QueuedNodeCount`
   - Fair scheduling: round-robin across priority levels

2. **Node-Level Throttling**: Rate limit specific nodes
   - Add `NodeDefinition.MaxConcurrentExecutions` property
   - Per-node-type semaphore (e.g., max 10 "api-call" nodes running)
   - Implement backpressure: pause upstream nodes when downstream throttled
   - Add `NodeDefinition.RateLimit` (requests per second)
   - Token bucket algorithm for rate limiting
   - Dynamic throttling based on error rate

3. **Resource Quotas**: CPU, memory, I/O limits (OPTIONAL - Advanced)
   - Add `NodeDefinition.ResourceRequirements` (CPU cores, memory MB, I/O ops)
   - Track total resource usage across active nodes
   - Reject node start if resources unavailable
   - Resource starvation detection (queue timeout)
   - Fair resource allocation (prevent hogging)
   - Monitor actual usage vs requested (if possible)

**Key Files**:
- `Workflow/WorkflowDefinition.cs` - Add MaxConcurrency
- `Workflow/NodeDefinition.cs` - Add Priority, MaxConcurrentExecutions, RateLimit, ResourceRequirements
- `Engine/WorkflowEngine.cs` - Implement concurrency control
- `Scheduling/NodeScheduler.cs` - NEW: Priority-based scheduling
- `Scheduling/ResourceManager.cs` - NEW: Track and enforce resource limits
- `Scheduling/RateLimiter.cs` - NEW: Token bucket rate limiting
- `Enums/NodePriority.cs` - NEW: High, Normal, Low

**Tests Required**: 20-25 tests covering:
- Workflow MaxConcurrency enforcement
- Node-level throttling
- Priority scheduling (high runs before low)
- Rate limiting accuracy
- Resource quota enforcement
- Backpressure propagation
- Starvation detection

**Dependencies**: Phase 2.2 (WorkflowEngine node execution)

**Success Criteria**:
- MaxConcurrency enforced accurately
- Node throttling prevents overload
- Priority scheduling works correctly
- Rate limiting prevents burst
- No deadlocks or starvation
- Performance overhead <10%
- Graceful degradation when limits hit

### Phase 2.7: Advanced Error Handling 🔄 **PLANNED**
**Focus**: Retry policies, compensation, and resilience patterns

**Core Deliverables**:
1. **Retry Policies**: Configurable retry strategies
   - Add `NodeDefinition.RetryPolicy` property
   - Strategies: `None`, `Fixed`, `Exponential`, `Linear`
   - Properties: `MaxAttempts`, `InitialDelay`, `MaxDelay`, `Multiplier`
   - Add jitter: randomize delay ±25% to prevent thundering herd
   - Conditional retry: `RetryOn` exception types (e.g., only retry TimeoutException)
   - Retry budget: global limit on retry attempts per workflow
   - Integrate with CircularBuffer retry mechanism (already exists!)

2. **Compensation Logic**: Undo operations on failure (Saga pattern)
   - Add `NodeDefinition.CompensationNodeId` property
   - Trigger compensation nodes in reverse order on workflow failure
   - Add `CompensationContext` with failure details
   - Support partial compensation (compensate only completed nodes)
   - Add `NodeConnection.IsCompensation` flag (don't trigger on normal flow)
   - Compensation scope: per-branch or entire workflow
   - Idempotent compensation (safe to run multiple times)

3. **Circuit Breaker**: Prevent cascading failures
   - Add `NodeDefinition.CircuitBreakerPolicy` property
   - States: `Closed` (normal), `Open` (failing), `HalfOpen` (testing)
   - Track failure rate per node type (not per instance)
   - Properties: `FailureThreshold` (%), `OpenDuration`, `HalfOpenSuccesses`
   - Fallback behavior: route to fallback node when circuit open
   - Add `NodeDefinition.FallbackNodeId` property
   - Reset circuit breaker after `OpenDuration` expires
   - Metrics: track circuit breaker state transitions

**Key Files**:
- `Policies/RetryPolicy.cs` - NEW: Retry configuration
- `Policies/CircuitBreakerPolicy.cs` - NEW: Circuit breaker configuration
- `Policies/CircuitBreakerState.cs` - NEW: Track circuit state
- `Workflow/NodeDefinition.cs` - Add RetryPolicy, CompensationNodeId, CircuitBreakerPolicy, FallbackNodeId
- `Contexts/CompensationContext.cs` - NEW: Failure context for compensation
- `Engine/WorkflowEngine.cs` - Implement compensation logic
- `Resilience/CircuitBreakerManager.cs` - NEW: Track and manage circuit breakers
- `Enums/RetryStrategy.cs` - NEW: None, Fixed, Exponential, Linear

**Tests Required**: 25-30 tests covering:
- Retry policies (all strategies)
- Exponential backoff calculation
- Jitter randomization
- Conditional retry (exception types)
- Compensation execution (reverse order)
- Partial compensation
- Circuit breaker state transitions
- Circuit breaker per node type
- Fallback routing
- Idempotent compensation

**Dependencies**:
- Phase 2.2 (WorkflowEngine)
- Phase 2.3 (Conditional routing for fallback)

**Success Criteria**:
- Retry policies work correctly
- Compensation runs in reverse order
- Circuit breaker prevents cascading failures
- Fallback routing works when circuit open
- Retry budget prevents infinite retries
- Performance overhead <5%
- Thread-safe circuit breaker state
- Metrics accurately track failures


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