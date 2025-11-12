# Workflow Execution Engine - Design & Implementation Plan

## Overview

This document describes the design and implementation plan for the Workflow Execution Engine (Phase 2), which builds upon the foundational components established in Phase 1 (CircularBuffer, NodeMessageQueue, MessageRouter).

The execution engine provides message-driven workflow orchestration with support for parallel execution, conditional routing, error handling, and state management.

---

## Architecture Principles

1. **Message-Driven Execution**: Nodes are triggered by messages from upstream nodes
2. **Single Execution Per Trigger**: Each node executes exactly once per incoming message
3. **Lock-Free Concurrency**: CircularBuffer enables thread-safe parallel execution
4. **Self-Healing**: Automatic retry and requeue for failed/expired messages
5. **Observable State**: Real-time progress tracking and event streams
6. **Resilience**: Circuit breakers, retry policies, and compensation logic

---

## Phase 2.2: Basic Workflow Orchestration ✅ **COMPLETE**

**Status**: ✅ Complete
**Focus**: Core orchestration and message-driven execution

### What Was Delivered

- **WorkflowEngine**: Core orchestration and lifecycle management
- **Message-Driven Execution**: Nodes triggered by messages from upstream nodes
- **Entry Point Detection**: Automatic detection of nodes with no incoming connections
- **NodeInstance Tracking**: State transitions (Pending → Running → Completed/Failed/Cancelled)
- **Message Routing**: Basic routing via MessageRouter based on workflow connections
- **Parallel Execution**: Task-based concurrency for independent nodes
- **Completion Detection**: Workflow completes when all reachable nodes finish
- **Timeout Handling**: 10s node timeout, 30s workflow timeout
- **Error Collection**: Aggregated error reporting from failed nodes

### Key Components

```
WorkflowEngine
├── InitializeNodesAsync() - Create node instances via NodeFactory
├── SetupMessageQueues() - Create NodeMessageQueue per node
├── SetupMessageRouter() - Configure routing based on connections
├── FindEntryPointNodes() - Detect workflow entry points
├── StartNodeExecutionTasks() - Launch parallel execution tasks
├── NodeExecutionLoopAsync() - Single message → single execution
├── TriggerEntryPointNodesAsync() - Send initial trigger messages
├── WaitForCompletionAsync() - Wait for all nodes with timeout
└── DetermineWorkflowStatus() - Aggregate final status
```

### Self-Healing Lease Management

- **Automatic Requeue**: CircularBuffer detects expired leases during CheckoutAsync
- **Exponential Backoff**: NotBefore = now + retryCount * 2 seconds
- **Retry Budget**: MaxRetries per message (default: 3)
- **Superseded Messages**: Messages exceeding maxRetries are marked as Superseded
- **No Background Monitoring**: Just-in-time cleanup during message checkout

### Test Metrics

- **Tests**: 10 WorkflowEngine tests
- **Coverage**: 91.4% overall | WorkflowEngine: 100%
- **Total Suite**: 288 tests (279 passing, 7 skipped, 2 known issues)

### Key Files

- `Engine/WorkflowEngine.cs` - Core orchestration engine
- `Engine/IWorkflowEngine.cs` - Lifecycle management interface
- `Contexts/WorkflowExecutionContext.cs` - Execution state
- `Contexts/NodeInstance.cs` - Per-node execution tracking
- `Routing/MessageRouter.cs` - Message routing logic
- `Messages/NodeCompleteMessage.cs` - Success message
- `Messages/NodeFailMessage.cs` - Failure message

---

## Phase 2.3: Conditional Routing & Message Filtering 🔄 **NEXT UP**

**Status**: 🔄 Planned
**Priority**: HIGH - Foundation for complex workflows
**Estimated Effort**: 2-3 weeks

### Core Deliverables

#### 1. Conditional Connections

Route messages based on output values from upstream nodes.

**Implementation**:
- Add `NodeConnection.Condition` property (string expression)
- Simple expression evaluator supporting:
  - Comparisons: `==`, `!=`, `>`, `<`, `>=`, `<=`
  - Logical operators: `&&`, `||`, `!`
  - Property access: `output.status`, `output.count`
- Evaluate condition against NodeContext.OutputData
- Route message only if condition evaluates to `true`

**Example**:
```csharp
new NodeConnection
{
    SourceNodeId = "payment-processor",
    TargetNodeId = "send-confirmation",
    Condition = "output.status == \"success\" && output.amount > 100"
}
```

#### 2. Message Type Filtering

Route based on message type (Complete vs. Fail).

**Implementation**:
- Add `NodeConnection.TriggerMessageType` enum (bitwise flags)
- Values: `Complete`, `Fail`, `All`
- Default: `Complete` (only route success messages)
- Filter in MessageRouter before condition evaluation

**Example**:
```csharp
new NodeConnection
{
    SourceNodeId = "api-call",
    TargetNodeId = "error-handler",
    TriggerMessageType = TriggerMessageType.Fail // Only route failures
}
```

#### 3. Output Port Routing

Support nodes with multiple named outputs (e.g., "success", "error", "timeout").

**Implementation**:
- Add `NodeConnection.SourcePort` and `TargetPort` properties
- Add `INode.GetAvailablePorts()` method (optional, returns default ports if not overridden)
- Add `NodeCompleteMessage.SourcePort` metadata
- Route based on source port match
- Default port: null (primary output)

**Example**:
```csharp
// Validation node with two output ports
new NodeConnection
{
    SourceNodeId = "validate-input",
    SourcePort = "valid",
    TargetNodeId = "process-data"
},
new NodeConnection
{
    SourceNodeId = "validate-input",
    SourcePort = "invalid",
    TargetNodeId = "send-error-response"
}
```

### Implementation Plan

**Week 1**: Conditional Connections
- Create `Routing/ConditionEvaluator.cs`
- Add expression parser (simple recursive descent parser)
- Add `NodeConnection.Condition` property
- Update `MessageRouter.RouteMessageAsync()` to evaluate conditions
- Unit tests: 8-10 tests (valid expressions, syntax errors, null handling)

**Week 2**: Message Type Filtering + Output Ports
- Add `Enums/TriggerMessageType.cs` enum
- Add `NodeConnection.TriggerMessageType` property
- Add `NodeConnection.SourcePort`, `TargetPort` properties
- Add `NodeCompleteMessage.SourcePort` metadata
- Add `INode.GetAvailablePorts()` method
- Update MessageRouter filtering logic
- Unit tests: 7-10 tests (filtering, port routing, defaults)

**Week 3**: Integration + Testing
- Integration tests with WorkflowEngine
- Complex workflow scenarios (branching, error paths)
- Performance testing (overhead measurement)
- Documentation updates

### Key Files to Create/Modify

**New Files**:
- `Routing/ConditionEvaluator.cs` - Expression parser and evaluator
- `Enums/TriggerMessageType.cs` - Message type enum

**Modified Files**:
- `Workflow/NodeConnection.cs` - Add Condition, TriggerMessageType, SourcePort, TargetPort
- `Routing/MessageRouter.cs` - Add condition evaluation and filtering
- `Core/INode.cs` - Add GetAvailablePorts() method
- `Messages/NodeCompleteMessage.cs` - Add SourcePort metadata

### Test Requirements (15-20 tests)

1. **Conditional Routing** (8 tests):
   - Simple comparison operators (`==`, `!=`, `>`, `<`)
   - Logical operators (`&&`, `||`, `!`)
   - Property access (`output.status`, `output.count`)
   - Null handling (missing properties)
   - Syntax errors (invalid expressions)
   - Type coercion (string to int comparisons)
   - Complex expressions
   - Backward compatibility (null condition = always route)

2. **Message Type Filtering** (4 tests):
   - Complete messages only (default)
   - Fail messages only
   - Both message types
   - Correct filtering in workflow execution

3. **Output Port Routing** (5 tests):
   - Named port routing
   - Default port (null)
   - Multiple ports from single node
   - Invalid port names (unhandled)
   - Port + condition combination

4. **Integration** (3 tests):
   - Full workflow with conditional branching
   - Error path routing
   - Multi-port node execution

### Dependencies

- Phase 2.2 (MessageRouter infrastructure) ✅

### Success Criteria

- ✅ Can route based on output values
- ✅ Can filter by message type
- ✅ Can route to named ports
- ✅ 100% backward compatible (null condition/port = default behavior)
- ✅ Performance overhead <2%
- ✅ Expression evaluator handles common scenarios
- ✅ Clear error messages for invalid expressions

---

## Phase 2.4: Reactive State Management 🔄 **HIGH PRIORITY**

**Status**: 🔄 Planned
**Priority**: HIGH - Essential for monitoring and UX
**Estimated Effort**: 3-4 weeks

### Core Deliverables

#### 1. State Change Events (IObservable)

Publish workflow and node state changes as observable event streams.

**Implementation**:
- Use `System.Reactive` (Rx.NET) for observable pattern
- Event types:
  - `WorkflowStartedEvent`
  - `WorkflowCompletedEvent`
  - `WorkflowFailedEvent`
  - `WorkflowCancelledEvent`
  - `NodeStartedEvent`
  - `NodeCompletedEvent`
  - `NodeFailedEvent`
  - `NodeCancelledEvent`
  - `NodeProgressEvent`
- Add `WorkflowExecutionContext.Events` property: `IObservable<WorkflowEvent>`
- Thread-safe event publishing using `Subject<T>`

**Example**:
```csharp
// Subscribe to node completion events
context.Events
    .OfType<NodeCompletedEvent>()
    .Subscribe(evt =>
    {
        Console.WriteLine($"Node {evt.NodeId} completed in {evt.Duration}");
    });
```

#### 2. Progress Tracking

Real-time progress calculation based on node completion.

**Implementation**:
- Add `ProgressUpdate` class with:
  - `PercentComplete` (0-100)
  - `NodesCompleted`, `NodesRunning`, `NodesPending`, `NodesFailed`
  - `EstimatedTimeRemaining` (based on average node execution time)
  - `Timestamp`
- Add `WorkflowExecutionContext.Progress` property: `IObservable<ProgressUpdate>`
- Update progress after each node state change
- Calculate percentage: (completed + failed) / (total reachable nodes) * 100

**Example**:
```csharp
// Display real-time progress
context.Progress.Subscribe(progress =>
{
    Console.WriteLine($"Progress: {progress.PercentComplete}% " +
                      $"({progress.NodesCompleted}/{progress.NodesCompleted + progress.NodesPending})");
});
```

#### 3. Live Query API

Query current workflow state at any time.

**Implementation**:
- Add `IWorkflowEngine.GetWorkflowStatusAsync(workflowInstanceId)`
- Add `WorkflowExecutionContext.GetNodeStatus(nodeId)` method
- Add `WorkflowExecutionContext.GetExecutionGraph()` method
- Return snapshot of current state (thread-safe copy)
- Include performance metrics:
  - Total execution time
  - Average node execution time
  - Queue depth per node

**Example**:
```csharp
var status = await engine.GetWorkflowStatusAsync(workflowInstanceId);
Console.WriteLine($"Status: {status.Status}");
Console.WriteLine($"Nodes: {status.NodeInstances.Count}");
Console.WriteLine($"Errors: {status.FailedNodes.Count}");
```

### Implementation Plan

**Week 1**: Event Infrastructure
- Add `System.Reactive` NuGet package
- Create event classes (`Events/WorkflowEvent.cs`, `Events/NodeEvent.cs`)
- Add `Subject<WorkflowEvent>` to WorkflowExecutionContext
- Publish basic events (Started, Completed, Failed) from WorkflowEngine
- Unit tests: Event publishing and subscription

**Week 2**: Progress Tracking
- Create `Events/ProgressUpdate.cs` class
- Add progress calculation logic
- Add `Subject<ProgressUpdate>` to WorkflowExecutionContext
- Calculate and publish progress after each node state change
- Unit tests: Progress calculation accuracy

**Week 3**: Live Query API
- Add `GetWorkflowStatusAsync()` to IWorkflowEngine
- Add `GetNodeStatus()`, `GetExecutionGraph()` to WorkflowExecutionContext
- Implement thread-safe state snapshotting
- Add performance metrics collection
- Unit tests: Live query during execution

**Week 4**: Testing + Optimization
- Integration tests with WorkflowEngine
- Memory leak testing (subscription cleanup)
- Performance overhead measurement
- Thread-safety stress testing
- Documentation updates

### Key Files to Create/Modify

**New Files**:
- `Events/WorkflowEvent.cs` - Base event class with timestamp, workflowInstanceId
- `Events/NodeEvent.cs` - Node-specific events with nodeId, status
- `Events/ProgressUpdate.cs` - Progress snapshot
- `Events/WorkflowStartedEvent.cs`, `WorkflowCompletedEvent.cs`, etc.
- `Events/NodeStartedEvent.cs`, `NodeCompletedEvent.cs`, etc.

**Modified Files**:
- `Contexts/WorkflowExecutionContext.cs` - Add Events, Progress observables
- `Engine/IWorkflowEngine.cs` - Add GetWorkflowStatusAsync
- `Engine/WorkflowEngine.cs` - Publish events during execution
- `ExecutionEngine.csproj` - Add System.Reactive NuGet package

### Test Requirements (20-25 tests)

1. **Event Publishing** (10 tests):
   - All event types published correctly
   - Event ordering (Started before Completed)
   - Event data accuracy (timestamps, nodeIds, etc.)
   - Multiple subscribers (no interference)
   - Subscription cleanup (no memory leaks)
   - Error events (exception details included)
   - Cancellation events
   - Thread-safety (concurrent subscriptions)

2. **Progress Tracking** (8 tests):
   - Progress calculation accuracy (various workflows)
   - Progress updates after each node completion
   - Percentage calculation (0% → 100%)
   - Node count accuracy
   - Estimated time remaining calculation
   - Progress with failed nodes
   - Progress with cancelled nodes
   - Thread-safety

3. **Live Query** (7 tests):
   - GetWorkflowStatusAsync during execution
   - GetNodeStatus for individual nodes
   - GetExecutionGraph structure
   - Snapshot consistency (no partial updates)
   - Performance metrics accuracy
   - Query non-existent workflow (null return)
   - Thread-safety

### Dependencies

- Phase 2.2 (State tracking) ✅

### NuGet Packages

- `System.Reactive` (6.0.0+)

### Success Criteria

- ✅ All events published correctly with accurate data
- ✅ Progress percentage accurate (±1%)
- ✅ Live queries return current state without blocking execution
- ✅ No memory leaks (subscriptions properly disposed)
- ✅ Thread-safe event publishing and querying
- ✅ Performance overhead <5%
- ✅ Subscription cleanup automatic (via IDisposable)

---

## Phase 2.5: State Persistence & Recovery 🔄

**Status**: 🔄 Planned
**Priority**: MEDIUM - Needed for long-running workflows
**Estimated Effort**: 4-5 weeks

### Core Deliverables

#### 1. Checkpointing

Save workflow state to persistent storage for recovery.

**Implementation**:
- Create `ICheckpointStorage` interface (pluggable storage)
- Implementations:
  - `InMemoryCheckpointStorage` (testing)
  - `FileCheckpointStorage` (JSON files)
  - Future: SQL, Redis, Blob storage
- Serialize:
  - WorkflowExecutionContext (status, variables)
  - NodeInstance states
  - Message queues (CircularBuffer state)
  - MessageRouter configuration
- Checkpoint frequency options:
  - `AfterEachNode` - After every node completion
  - `AfterNNodes` - After N node completions
  - `TimeInterval` - Every N seconds
  - `Manual` - Explicit checkpoint calls

**Example**:
```csharp
var storage = new FileCheckpointStorage("checkpoints/");
var checkpoint = new WorkflowCheckpoint
{
    WorkflowInstanceId = context.InstanceId,
    Status = context.Status,
    NodeStates = context.NodeInstances.ToList(),
    MessageQueues = SerializeQueues(context.NodeQueues),
    Timestamp = DateTime.UtcNow
};
await storage.SaveCheckpointAsync(checkpoint);
```

#### 2. Pause/Resume

Gracefully pause and resume workflow execution.

**Implementation**:
- Add `IWorkflowEngine.PauseAsync(workflowInstanceId)`
  - Stop accepting new messages
  - Wait for currently running nodes to complete
  - Create checkpoint
  - Set WorkflowExecutionStatus.Paused
- Add `IWorkflowEngine.ResumeAsync(workflowInstanceId)`
  - Load checkpoint
  - Reconstruct WorkflowExecutionContext
  - Restore message queues
  - Resume node execution tasks
  - Set WorkflowExecutionStatus.Running
- Preserve CircularBuffer state (in-flight messages)

**Example**:
```csharp
// Pause for maintenance
await engine.PauseAsync(workflowInstanceId);

// Later, resume execution
await engine.ResumeAsync(workflowInstanceId);
```

#### 3. Failure Recovery

Automatically recover incomplete workflows after restart.

**Implementation**:
- Add `IWorkflowEngine.RecoverIncompleteWorkflowsAsync()`
- At engine startup:
  - Query checkpoint storage for Running/Paused workflows
  - Load last checkpoint for each incomplete workflow
  - Reconstruct WorkflowExecutionContext
  - Handle in-flight nodes:
    - If InFlight status → requeue message with retry++
    - If Running → mark as Pending and requeue
  - Resume execution from last completed node
- Idempotent recovery (safe to run multiple times)

**Example**:
```csharp
// At application startup
var engine = new WorkflowEngine(checkpointStorage);
await engine.RecoverIncompleteWorkflowsAsync();
```

### Implementation Plan

**Week 1-2**: Checkpoint Storage
- Create `Persistence/ICheckpointStorage.cs` interface
- Implement `InMemoryCheckpointStorage`
- Implement `FileCheckpointStorage` with JSON serialization
- Create `Persistence/WorkflowCheckpoint.cs` serializable class
- Serialize/deserialize CircularBuffer state
- Unit tests: Storage implementations

**Week 3**: Pause/Resume
- Add `PauseAsync()`, `ResumeAsync()` to IWorkflowEngine
- Implement graceful pause (wait for running nodes)
- Implement resume (restore queues and state)
- Add `WorkflowExecutionStatus.Paused` state
- Unit tests: Pause/Resume without data loss

**Week 4**: Failure Recovery
- Add `RecoverIncompleteWorkflowsAsync()` to IWorkflowEngine
- Implement recovery logic (requeue in-flight messages)
- Handle partial node execution
- Add recovery logging and metrics
- Unit tests: Crash recovery scenarios

**Week 5**: Testing + Edge Cases
- Integration tests (full pause/resume/recovery)
- Concurrent checkpoint access
- Serialization performance benchmarks
- Large workflow checkpointing
- Documentation updates

### Key Files to Create/Modify

**New Files**:
- `Persistence/ICheckpointStorage.cs` - Storage interface
- `Persistence/InMemoryCheckpointStorage.cs` - In-memory implementation
- `Persistence/FileCheckpointStorage.cs` - File-based implementation
- `Persistence/WorkflowCheckpoint.cs` - Serializable snapshot
- `Enums/CheckpointFrequency.cs` - Checkpoint frequency options

**Modified Files**:
- `Engine/IWorkflowEngine.cs` - Add PauseAsync, ResumeAsync, RecoverAsync
- `Engine/WorkflowEngine.cs` - Implement checkpointing logic
- `Contexts/WorkflowExecutionContext.cs` - Add serialization support
- `Queue/CircularBuffer.cs` - Add state serialization methods

### Test Requirements (25-30 tests)

1. **Checkpointing** (10 tests):
   - Checkpoint creation
   - Checkpoint restoration
   - Serialization/deserialization accuracy
   - Storage implementations (InMemory, File)
   - Checkpoint frequency options
   - CircularBuffer state preservation
   - Large workflow checkpointing
   - Concurrent checkpoint writes
   - Serialization performance (<100ms)

2. **Pause/Resume** (8 tests):
   - Pause running workflow
   - Resume paused workflow
   - No data loss after pause/resume
   - Message queue preservation
   - In-flight message handling
   - Multiple pause/resume cycles
   - Pause during node execution
   - Resume after long pause

3. **Failure Recovery** (10 tests):
   - Recover running workflow after crash
   - Recover paused workflow
   - Requeue in-flight messages
   - Handle partial node execution
   - Multiple incomplete workflows
   - Recovery idempotency
   - Recovery after corrupted checkpoint
   - Recovery performance
   - Recovery logging

### Dependencies

- Phase 2.2 (WorkflowEngine) ✅
- Phase 2.4 (State tracking for serialization)

### NuGet Packages

- `System.Text.Json` (for serialization)

### Success Criteria

- ✅ Can checkpoint at any point during execution
- ✅ Pause/Resume without data loss
- ✅ Crash recovery works (automatic at startup)
- ✅ Pluggable storage (easy to add new implementations)
- ✅ Serialization <100ms for typical workflow (50 nodes)
- ✅ No message loss during checkpoint/recovery
- ✅ Backward compatible checkpoints (version handling)
- ✅ Thread-safe checkpoint operations

---

## Phase 2.6: MaxConcurrency & Resource Management 🔄

**Status**: 🔄 Planned
**Priority**: MEDIUM - Important for scalability
**Estimated Effort**: 3-4 weeks

### Core Deliverables

#### 1. Workflow-Level Concurrency

Limit concurrent node executions across entire workflow.

**Implementation**:
- Add `WorkflowDefinition.MaxConcurrency` property (default: unlimited)
- Add concurrency semaphore in WorkflowEngine
- Queue nodes when concurrency limit reached
- Add `NodeDefinition.Priority` enum (High, Normal, Low)
- Fair scheduling: round-robin across priorities
- Track active execution count

**Example**:
```csharp
new WorkflowDefinition
{
    WorkflowId = "data-processing",
    MaxConcurrency = 5, // Max 5 nodes executing concurrently
    Nodes = new List<NodeDefinition>
    {
        new NodeDefinition { NodeId = "fetch-data", Priority = NodePriority.High },
        new NodeDefinition { NodeId = "process-data", Priority = NodePriority.Normal }
    }
}
```

#### 2. Node-Level Throttling

Limit concurrent executions of specific node types.

**Implementation**:
- Add `NodeDefinition.MaxConcurrentExecutions` property
- Per-node-type semaphore (not per-instance)
- Example: API call node with rate limit
- Add `NodeDefinition.RateLimit` property (requests/second)
- Token bucket rate limiting implementation
- Backpressure: pause upstream when downstream throttled

**Example**:
```csharp
new NodeDefinition
{
    NodeId = "api-call",
    NodeType = "HttpClient",
    MaxConcurrentExecutions = 10, // Max 10 concurrent API calls
    RateLimit = 100 // Max 100 requests/second
}
```

#### 3. Resource Quotas (OPTIONAL)

Track and enforce resource usage limits.

**Implementation**:
- Add `NodeDefinition.ResourceRequirements` property
  - `CpuUnits` (estimated CPU cores)
  - `MemoryMB` (estimated memory)
  - `IoUnits` (estimated I/O)
- Add `WorkflowDefinition.ResourceQuotas` property
- Track total resource usage
- Reject node start if resources unavailable
- Resource starvation detection (warn if node can't get resources)
- Fair allocation (prevent single node monopolizing resources)

**Example**:
```csharp
new NodeDefinition
{
    NodeId = "video-encode",
    ResourceRequirements = new ResourceRequirements
    {
        CpuUnits = 2.0, // Needs 2 CPU cores
        MemoryMB = 4096 // Needs 4GB RAM
    }
}

new WorkflowDefinition
{
    WorkflowId = "video-pipeline",
    ResourceQuotas = new ResourceQuotas
    {
        MaxCpuUnits = 8.0,
        MaxMemoryMB = 16384
    }
}
```

### Implementation Plan

**Week 1**: Workflow-Level Concurrency
- Add `WorkflowDefinition.MaxConcurrency` property
- Add `Enums/NodePriority.cs` enum
- Add `NodeDefinition.Priority` property
- Implement concurrency semaphore in WorkflowEngine
- Implement priority-based scheduling
- Unit tests: Concurrency enforcement, priority scheduling

**Week 2**: Node-Level Throttling + Rate Limiting
- Add `NodeDefinition.MaxConcurrentExecutions` property
- Implement per-node-type semaphores
- Add `NodeDefinition.RateLimit` property
- Implement `Scheduling/RateLimiter.cs` (token bucket)
- Implement backpressure mechanism
- Unit tests: Throttling, rate limiting

**Week 3**: Resource Quotas (Optional)
- Add `Workflow/ResourceRequirements.cs` class
- Add `Workflow/ResourceQuotas.cs` class
- Implement `Scheduling/ResourceManager.cs`
- Track resource usage
- Starvation detection
- Unit tests: Resource allocation, quotas

**Week 4**: Testing + Optimization
- Integration tests (full workflow with limits)
- Performance benchmarks (overhead measurement)
- Stress testing (high concurrency)
- Deadlock detection tests
- Documentation updates

### Key Files to Create/Modify

**New Files**:
- `Scheduling/NodeScheduler.cs` - Priority-based scheduling
- `Scheduling/RateLimiter.cs` - Token bucket rate limiting
- `Scheduling/ResourceManager.cs` - Resource tracking and allocation
- `Workflow/ResourceRequirements.cs` - Resource requirements class
- `Workflow/ResourceQuotas.cs` - Resource quotas class
- `Enums/NodePriority.cs` - High, Normal, Low

**Modified Files**:
- `Workflow/WorkflowDefinition.cs` - Add MaxConcurrency, ResourceQuotas
- `Workflow/NodeDefinition.cs` - Add Priority, MaxConcurrentExecutions, RateLimit, ResourceRequirements
- `Engine/WorkflowEngine.cs` - Integrate scheduling and resource management

### Test Requirements (20-25 tests)

1. **Workflow-Level Concurrency** (8 tests):
   - MaxConcurrency enforcement
   - Concurrent execution count accuracy
   - Node queuing when limit reached
   - Priority scheduling (High > Normal > Low)
   - Fair scheduling (round-robin)
   - Unlimited concurrency (default)
   - Concurrency with failures
   - Thread-safety

2. **Node-Level Throttling** (7 tests):
   - MaxConcurrentExecutions per node type
   - Per-node-type semaphore isolation
   - Rate limiting accuracy (±5%)
   - Token bucket algorithm
   - Backpressure mechanism
   - Burst traffic handling
   - Thread-safety

3. **Resource Quotas** (6 tests):
   - Resource allocation tracking
   - Resource quota enforcement
   - Resource starvation detection
   - Fair allocation
   - Resource release after node completion
   - Thread-safety

4. **Integration** (4 tests):
   - Full workflow with all limits
   - High load stress test
   - Deadlock detection
   - Performance overhead measurement

### Dependencies

- Phase 2.2 (WorkflowEngine) ✅

### Success Criteria

- ✅ MaxConcurrency enforced accurately (±1)
- ✅ Node throttling prevents overload
- ✅ Priority scheduling works correctly
- ✅ Rate limiting accurate (±5%)
- ✅ No deadlocks or starvation
- ✅ Performance overhead <10%
- ✅ Thread-safe resource tracking
- ✅ Clear error messages when limits exceeded

---

## Phase 2.7: Advanced Error Handling 🔄

**Status**: 🔄 Planned
**Priority**: HIGH - Critical for production
**Estimated Effort**: 4-5 weeks

### Core Deliverables

#### 1. Retry Policies

Configurable retry strategies for failed nodes.

**Implementation**:
- Add `NodeDefinition.RetryPolicy` property
- Retry strategies:
  - `None` - No retry (fail immediately)
  - `Fixed` - Fixed delay between retries
  - `Exponential` - Exponential backoff (delay * 2^retryCount)
  - `Linear` - Linear backoff (delay * retryCount)
- Properties:
  - `MaxAttempts` (1-10, default: 3)
  - `InitialDelay` (TimeSpan, default: 1s)
  - `MaxDelay` (TimeSpan, default: 60s)
  - `Multiplier` (double, default: 2.0 for exponential)
- Add jitter: randomize delay ±25% (prevent thundering herd)
- Conditional retry:
  - `RetryOn` exception types (e.g., only retry TimeoutException)
  - `DoNotRetryOn` exception types (e.g., never retry ArgumentException)
- Retry budget: global limit on retry attempts per workflow
- Integrate with CircularBuffer retry mechanism (already exists!)

**Example**:
```csharp
new NodeDefinition
{
    NodeId = "api-call",
    RetryPolicy = new RetryPolicy
    {
        Strategy = RetryStrategy.Exponential,
        MaxAttempts = 5,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        Multiplier = 2.0,
        RetryOn = new[] { typeof(TimeoutException), typeof(HttpRequestException) }
    }
}
```

#### 2. Compensation Logic (Saga Pattern)

Undo operations on failure (distributed transactions).

**Implementation**:
- Add `NodeDefinition.CompensationNodeId` property
- Trigger compensation nodes in reverse order on workflow failure
- Add `CompensationContext` with:
  - Original failure reason
  - Failed node ID
  - Failed node output data
  - List of nodes to compensate
- Support partial compensation (compensate only completed nodes)
- Add `NodeConnection.IsCompensation` flag (don't trigger on normal flow)
- Compensation scope options:
  - `PerBranch` - Compensate only nodes in failed branch
  - `EntireWorkflow` - Compensate all completed nodes
- Idempotent compensation (safe to run multiple times)

**Example**:
```csharp
new NodeDefinition
{
    NodeId = "charge-payment",
    CompensationNodeId = "refund-payment" // Undo on failure
},
new NodeDefinition
{
    NodeId = "refund-payment",
    NodeType = "PaymentRefund",
    // This node receives CompensationContext with failure details
}
```

**Compensation execution order** (reverse of completion):
```
Normal flow: A → B → C → D (D fails)
Compensation: D_compensate ← C_compensate ← B_compensate ← A_compensate
```

#### 3. Circuit Breaker

Prevent cascading failures by temporarily blocking failing nodes.

**Implementation**:
- Add `NodeDefinition.CircuitBreakerPolicy` property
- Circuit breaker states:
  - `Closed` - Normal operation, allow all requests
  - `Open` - Too many failures, block all requests
  - `HalfOpen` - Testing recovery, allow limited requests
- Track failure rate per node type (not per instance)
- Properties:
  - `FailureThreshold` (percentage, default: 50%)
  - `MinimumThroughput` (min requests before opening, default: 10)
  - `OpenDuration` (TimeSpan, default: 30s)
  - `HalfOpenSuccesses` (successes needed to close, default: 3)
- Fallback behavior:
  - Add `NodeDefinition.FallbackNodeId` property
  - Route to fallback node when circuit open
  - Fallback node receives `CircuitBreakerContext` with failure stats
- Reset circuit breaker after `OpenDuration` expires (automatic)
- Metrics: track circuit breaker state transitions

**Example**:
```csharp
new NodeDefinition
{
    NodeId = "external-api",
    CircuitBreakerPolicy = new CircuitBreakerPolicy
    {
        FailureThreshold = 50, // Open after 50% failures
        MinimumThroughput = 10, // Need at least 10 requests
        OpenDuration = TimeSpan.FromSeconds(30),
        HalfOpenSuccesses = 3 // 3 successes to close
    },
    FallbackNodeId = "use-cached-data" // Fallback when circuit open
}
```

**State transitions**:
```
Closed (normal) → Open (too many failures) → HalfOpen (testing) → Closed (recovered)
                ↑                                              ↓
                └──────────────────────────────────────────────┘
                          (if HalfOpen tests fail)
```

### Implementation Plan

**Week 1-2**: Retry Policies
- Add `Policies/RetryPolicy.cs` class
- Add `Enums/RetryStrategy.cs` enum
- Implement retry strategy calculators (Fixed, Exponential, Linear)
- Add jitter calculation
- Implement conditional retry (exception type matching)
- Add retry budget tracking to WorkflowExecutionContext
- Integrate with CircularBuffer retry mechanism
- Unit tests: All strategies, jitter, conditional retry

**Week 2-3**: Compensation Logic
- Add `Contexts/CompensationContext.cs` class
- Add `NodeDefinition.CompensationNodeId` property
- Add `NodeConnection.IsCompensation` flag
- Implement reverse-order compensation execution in WorkflowEngine
- Track compensation scope (per-branch vs entire workflow)
- Implement idempotency checks
- Unit tests: Compensation execution, partial compensation, idempotency

**Week 3-4**: Circuit Breaker
- Add `Policies/CircuitBreakerPolicy.cs` class
- Add `Policies/CircuitBreakerState.cs` class
- Implement `Resilience/CircuitBreakerManager.cs`
- Track failure rates per node type
- Implement state machine (Closed → Open → HalfOpen → Closed)
- Add fallback routing
- Add metrics tracking
- Unit tests: State transitions, fallback, metrics

**Week 5**: Testing + Integration
- Integration tests (full workflow with all resilience features)
- Retry + circuit breaker interaction
- Compensation + retry interaction
- Performance overhead measurement
- Stress testing
- Documentation updates

### Key Files to Create/Modify

**New Files**:
- `Policies/RetryPolicy.cs` - Retry configuration
- `Policies/CircuitBreakerPolicy.cs` - Circuit breaker configuration
- `Policies/CircuitBreakerState.cs` - Track circuit state
- `Contexts/CompensationContext.cs` - Failure context for compensation
- `Resilience/CircuitBreakerManager.cs` - Manage circuit breakers
- `Enums/RetryStrategy.cs` - None, Fixed, Exponential, Linear

**Modified Files**:
- `Workflow/NodeDefinition.cs` - Add RetryPolicy, CompensationNodeId, CircuitBreakerPolicy, FallbackNodeId
- `Workflow/NodeConnection.cs` - Add IsCompensation flag
- `Engine/WorkflowEngine.cs` - Implement compensation logic
- `Contexts/WorkflowExecutionContext.cs` - Add retry budget tracking

### Test Requirements (25-30 tests)

1. **Retry Policies** (10 tests):
   - Fixed strategy
   - Exponential strategy
   - Linear strategy
   - None strategy (no retry)
   - Exponential backoff calculation accuracy
   - Jitter randomization (±25%)
   - Conditional retry (exception types)
   - DoNotRetryOn exceptions
   - Retry budget enforcement
   - MaxDelay cap

2. **Compensation Logic** (10 tests):
   - Compensation execution (reverse order)
   - Partial compensation (only completed nodes)
   - CompensationContext data accuracy
   - IsCompensation flag (no normal flow)
   - Per-branch compensation scope
   - Entire workflow compensation scope
   - Idempotent compensation
   - Compensation failure handling
   - Multiple compensation nodes
   - Compensation with retry

3. **Circuit Breaker** (8 tests):
   - Closed → Open transition (failure threshold)
   - Open → HalfOpen transition (after duration)
   - HalfOpen → Closed transition (success threshold)
   - HalfOpen → Open transition (test failure)
   - Per-node-type tracking
   - Fallback routing
   - MinimumThroughput requirement
   - Metrics tracking

4. **Integration** (7 tests):
   - Retry + circuit breaker interaction
   - Compensation + retry interaction
   - All resilience features together
   - Performance overhead measurement (<5%)
   - Thread-safety
   - High failure rate stress test
   - Complex workflow with multiple failure paths

### Dependencies

- Phase 2.2 (WorkflowEngine) ✅
- Phase 2.3 (Conditional routing for fallback)

### Success Criteria

- ✅ Retry policies work correctly for all strategies
- ✅ Exponential backoff calculation accurate
- ✅ Compensation runs in reverse order
- ✅ Circuit breaker prevents cascading failures
- ✅ Fallback routing works when circuit open
- ✅ Retry budget prevents infinite retries
- ✅ Performance overhead <5%
- ✅ Thread-safe circuit breaker state management
- ✅ Metrics accurately track failures and retries
- ✅ Idempotent compensation (safe to retry)

---

## Implementation Priority & Roadmap

### Recommended Order

1. **Phase 2.3** (Conditional Routing) - 2-3 weeks
   - Foundation for complex workflows
   - Enables error handling patterns
   - Required for Phase 2.7 (fallback routing)

2. **Phase 2.4** (Reactive State) - 3-4 weeks
   - Essential for monitoring and UX
   - Enables real-time debugging
   - Required for Phase 2.5 (state tracking)

3. **Phase 2.7** (Error Handling) - 4-5 weeks
   - Critical for production reliability
   - Includes retry, compensation, circuit breaker
   - Depends on Phase 2.3 (conditional routing)

4. **Phase 2.6** (Resource Management) - 3-4 weeks
   - Important for scalability
   - Can be implemented independently
   - Lower priority than error handling

5. **Phase 2.5** (Persistence) - 4-5 weeks
   - Needed for long-running workflows
   - Requires Phase 2.4 (state tracking)
   - Can be deferred if not needed immediately

### Total Estimated Timeline

- **16-21 weeks** for all phases
- **~4-5 months** calendar time (with iterations and testing)
- **Minimum viable product**: Phases 2.3 + 2.4 + 2.7 (9-12 weeks)

### Dependency Graph

```
Phase 2.2 (COMPLETE) ✅
    ├── Phase 2.3 (Conditional Routing) [2-3 weeks]
    │       └── Phase 2.7 (Error Handling) [4-5 weeks]
    ├── Phase 2.4 (Reactive State) [3-4 weeks]
    │       └── Phase 2.5 (Persistence) [4-5 weeks]
    └── Phase 2.6 (Resource Management) [3-4 weeks] (independent)
```

### Milestones

**Milestone 1**: Complex Workflows (Phases 2.3) - Week 3
- Conditional routing
- Multi-path workflows
- Error path handling

**Milestone 2**: Observable Workflows (Phases 2.3 + 2.4) - Week 7
- Real-time monitoring
- Progress tracking
- Live debugging

**Milestone 3**: Production-Ready (Phases 2.3 + 2.4 + 2.7) - Week 14
- Retry policies
- Compensation logic
- Circuit breakers
- Ready for production use

**Milestone 4**: Scalable (Phases 2.3 + 2.4 + 2.6 + 2.7) - Week 18
- Concurrency control
- Resource management
- Rate limiting

**Milestone 5**: Long-Running (All Phases) - Week 23
- State persistence
- Pause/Resume
- Crash recovery

---

## Testing Strategy

### Unit Tests (Per Phase)

- 15-30 tests per phase
- Cover all features and edge cases
- Isolated component testing
- Mock dependencies
- Fast execution (<1s per test)

### Integration Tests

- End-to-end workflow scenarios
- Multi-phase feature interaction
- Real components (no mocks)
- Performance benchmarks
- Error scenarios

### Performance Tests

- Throughput: workflows/second
- Latency: p50, p95, p99
- Memory usage
- CPU usage
- Overhead measurement (each phase <5%)

### Code Coverage

- Maintain >90% coverage
- 100% for critical paths (WorkflowEngine, MessageRouter, CircularBuffer)
- Document uncovered code
- No coverage regressions

---

## Success Criteria (Overall)

Each phase is complete when:

1. ✅ All features implemented according to spec
2. ✅ All tests passing (unit + integration)
3. ✅ Code coverage ≥90%
4. ✅ Documentation updated (this file + code comments)
5. ✅ No known critical bugs
6. ✅ Performance benchmarks met
7. ✅ Code reviewed and approved
8. ✅ Backward compatible (no breaking changes to public API)

---

## Next Steps

1. ✅ Review and approve this implementation plan
2. 🔄 Complete IWorkflowEngine interface implementation
3. 🔄 Begin Phase 2.3 implementation (Conditional Routing)
4. Set up project tracking (Kanban board)
5. Schedule weekly reviews
6. Begin architecture diagrams (workflow execution flow)

---

## Change Log

- **2025-01-12**: Initial design document created
- **Phase 2.2**: Completed basic workflow orchestration (10 tests, 100% coverage)
- **Phase 2.2**: Removed LeaseMonitor, simplified to self-healing CircularBuffer architecture
- **Phase 2.2**: Created IWorkflowEngine interface (implementation pending)
