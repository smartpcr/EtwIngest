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

### Phase 2: Workflow Execution Engine 🔄 **PLANNED**
- [ ] WorkflowEngine - orchestrates workflow execution
- [ ] WorkflowInstance - runtime state tracking
- [ ] Dependency resolution
- [ ] Parallel node execution with MaxConcurrency enforcement
- [ ] Message-based node triggering
- [ ] Error propagation and handling

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