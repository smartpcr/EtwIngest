# ExecutionEngine Implementation Plan

Based on the design document in `readme.md`, this document tracks the implementation phases.

## Phase 1.1: Core Models ✓ COMPLETE
- [x] NodeInstance - execution result tracking
- [x] NodeExecutionContext - node-level state
- [x] WorkflowExecutionContext - workflow-level state
- [x] ExecutionState - helper for script access
- [x] NodeExecutionStatus enum
- [x] WorkflowExecutionStatus enum
- [x] Unit tests with 100% coverage

## Phase 1.2: Message Infrastructure ✓ COMPLETE
- [x] MessageType enum (Complete, Fail, Progress)
- [x] INodeMessage interface
- [x] NodeCompleteMessage
- [x] NodeFailMessage
- [x] ProgressMessage
- [x] Unit tests with 100% coverage

## Phase 1.3: Message Queue and Routing ✓ COMPLETE
- [x] CircularBuffer - lock-free ring buffer
- [x] MessageEnvelope - message wrapper with lease management
- [x] NodeMessageQueue - channel-based queue
- [x] DeadLetterQueue - failed message handling
- [x] MessageRouter - message routing infrastructure
- [x] Unit tests with 100% coverage

## Phase 1.4: Node Factory and Execution ✓ COMPLETE
- [x] NodeDefinition - node configuration model
- [x] NodeFactory - dynamic node creation
- [x] INode interface
- [x] ExecutableNodeBase - base class for nodes
- [x] CSharpScriptNode - Roslyn-based C# script execution
- [x] PowerShellScriptNode - PowerShell script execution
- [x] Assembly loading with type caching
- [x] Unit tests with 100% coverage
- [x] Platform-specific test handling

## Phase 1.5: Workflow Definition and Graph Model ⚠️ PARTIAL
### Completed ✓
- [x] WorkflowDefinition - graph structure model
- [x] NodeConnection - edge/dependency model
- [x] WorkflowValidator - graph validation
- [x] Cycle detection (DFS algorithm)
- [x] Entry point validation
- [x] Connection validation
- [x] Unit tests with 100% coverage

### Missing ✗
- [ ] **JSON Serialization** - Load/save workflows from/to JSON
- [ ] **YAML Serialization** - Load/save workflows from/to YAML
- [ ] **WorkflowLoader** - File I/O helper for loading workflow definitions
- [ ] **WorkflowSerializer** - Serialize/deserialize workflows
- [ ] Unit tests for serialization/deserialization
- [ ] Sample workflow files (JSON and YAML)

## Phase 1.6: Workflow Execution Engine (PLANNED)
- [ ] WorkflowEngine - orchestrates workflow execution
- [ ] WorkflowInstance - runtime state tracking
- [ ] Node execution orchestration
- [ ] Dependency resolution
- [ ] Parallel execution support
- [ ] MaxConcurrency enforcement
- [ ] Unit tests

## Phase 1.7: Control Flow Nodes (PLANNED)
- [ ] IfElseNode - conditional branching
- [ ] SwitchNode - multi-way branching
- [ ] WhileLoopNode - conditional loops
- [ ] ForEachLoopNode - collection iteration
- [ ] SubflowNode - nested workflow execution
- [ ] Unit tests

## Phase 1.8: State Persistence (PLANNED)
- [ ] WorkflowInstanceSerializer - serialize runtime state
- [ ] Pause/Resume support
- [ ] Checkpoint creation
- [ ] State recovery
- [ ] Unit tests

## Phase 1.9: Timer and Trigger Nodes (PLANNED)
- [ ] TimerNode - schedule-based execution
- [ ] ManualTriggerNode - on-demand execution
- [ ] EventTriggerNode - event-based triggers
- [ ] Unit tests

## Phase 2.0: Advanced Features (PLANNED)
- [ ] Workflow versioning
- [ ] Workflow migration
- [ ] Execution history
- [ ] Performance monitoring
- [ ] Distributed execution support

## Design Requirements Reference

From `readme.md`:
1. ✓ Execution plan is a graph containing nodes and connectors
2. ✓ Graph execution creates workflow instance, triggered by special node
3. ✓ ExecutionContext with global variables, message queue via channels
4. ✓ Nodes execute with `Task ExecuteAsync(CancellationToken)`
5. ✓ Nodes produce events: OnStart, OnProgress, messages: OnComplete, OnFail
6. ✓ Messages connected via channel subscriptions (edges in graph)
7. ⚠️ Special node: subflow (workflow) - PLANNED for Phase 1.7
8. ⚠️ Control flow nodes: if-else, switch, while/foreach - PLANNED for Phase 1.7
9. ✓ Graph validation (no infinite loops) - DONE
10. ⚠️ **Graph definition in JSON and YAML** - MISSING in Phase 1.5
11. ✓ Node execution in C# code and PowerShell script - DONE
12. ⚠️ **Graph persisted to file, execution state persisted** - PARTIALLY DONE (need serialization)

## Critical Gaps

### Phase 1.5 Gaps (MUST FIX NOW):
1. **JSON Serialization** - Cannot load/save workflow definitions from JSON files
2. **YAML Serialization** - Cannot load/save workflow definitions from YAML files
3. **WorkflowLoader** - No helper for file I/O operations
4. **Sample Files** - No example workflow definitions to validate serialization

These must be implemented to complete Phase 1.5 per design requirements #9 and #10.
