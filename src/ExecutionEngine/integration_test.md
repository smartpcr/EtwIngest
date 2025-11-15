# ExecutionEngine Integration Tests

This document describes the comprehensive integration test scenarios for the ExecutionEngine workflow orchestration system. All scenarios are implemented using BDD (Behavior-Driven Development) with Reqnroll and MSTest.

## Test Organization

Integration tests are located in the `ExecutionEngine.IntegrationTests` project and organized by functional area:

- **[WorkflowExecution.feature](../ExecutionEngine.IntegrationTests/Features/WorkflowExecution.feature)**: Core workflow execution scenarios (7 scenarios)
- **[MessageRouting.feature](../ExecutionEngine.IntegrationTests/Features/MessageRouting.feature)**: Message routing and queue management (8 scenarios)
- **[ControlFlow.feature](../ExecutionEngine.IntegrationTests/Features/ControlFlow.feature)**: Conditional and loop control structures (12 scenarios)
- **[StateManagement.feature](../ExecutionEngine.IntegrationTests/Features/StateManagement.feature)**: Workflow and node context state handling (14 scenarios)
- **[ErrorHandling.feature](../ExecutionEngine.IntegrationTests/Features/ErrorHandling.feature)**: Failure scenarios, retry, and recovery (14 scenarios)
- **[Concurrency.feature](../ExecutionEngine.IntegrationTests/Features/Concurrency.feature)**: Parallel execution and resource management (10 scenarios)
- **[AdvancedFeatures.feature](../ExecutionEngine.IntegrationTests/Features/AdvancedFeatures.feature)**: Container nodes, subflows, and timers (20 scenarios)

**Total: 85 integration test scenarios**

## Test Categories

### 1. Workflow Execution ([WorkflowExecution.feature](../ExecutionEngine.IntegrationTests/Features/WorkflowExecution.feature))

Tests the core workflow execution engine capabilities.

#### 1.1 Simple Sequential Workflow
**Scenario**: Execute a workflow with nodes in sequence
**Feature File**: `WorkflowExecution.feature` - "Simple sequential workflow execution"
**Test Cases**:
- Node A → Node B → Node C execution order
- Each node completes before next starts
- Final workflow status is Completed
- Execution duration is recorded

**Validation**:
- All nodes execute in correct order
- NodeStarted, NodeCompleted events fire
- Workflow context contains all node results

#### 1.2 Parallel Workflow Execution
**Scenario**: Execute nodes in parallel when no dependencies exist
**Feature File**: `WorkflowExecution.feature` - "Parallel workflow execution"
**Test Cases**:
- Multiple nodes with no dependencies execute concurrently
- Fan-out: Node A → (Node B, Node C, Node D) parallel execution
- All parallel branches complete before downstream node

**Validation**:
- Multiple nodes have overlapping execution timestamps
- All parallel nodes complete successfully
- Downstream node waits for all parallel nodes

#### 1.3 Mixed Parallel and Sequential
**Scenario**: Complex workflow with both parallel and sequential segments
**Feature File**: `WorkflowExecution.feature` - "Mixed parallel and sequential execution"
**Test Cases**:
- Start → (ParallelA, ParallelB) → Join → Sequential → End
- Fan-in: Multiple nodes converge to single node

**Validation**:
- Correct execution topology
- Join node waits for all upstream completions
- Sequential portion executes after join

#### 1.4 Entry Point Discovery
**Scenario**: Workflow automatically identifies entry point nodes
**Feature File**: `WorkflowExecution.feature` - "Entry point node discovery"
**Test Cases**:
- Nodes with no incoming connections are entry points
- Multiple entry points execute in parallel
- Timer nodes are valid entry points

**Validation**:
- Correct entry points identified
- All entry points receive initial trigger message
- Workflow starts from correct nodes

### 2. Message Routing ([MessageRouting.feature](../ExecutionEngine.IntegrationTests/Features/MessageRouting.feature))

Tests the message routing and queue management system.

#### 2.1 Basic Message Routing
**Scenario**: Route completion messages to downstream nodes
**Feature File**: `MessageRouting.feature` - "Route completion message to downstream node"
**Test Cases**:
- NodeCompleteMessage routes to connected target
- Message contains correct source node ID and context
- Target node queue receives message

**Validation**:
- Message appears in target queue
- Message contains NodeExecutionContext from source
- Router reports successful delivery

#### 2.2 Conditional Routing
**Scenario**: Route messages based on conditions
**Feature File**: `MessageRouting.feature` - "Conditional message routing"
**Test Cases**:
- Connections with conditions evaluate correctly
- Conditions access input data and workflow variables
- Failed conditions skip routing

**Validation**:
- Only matching connections receive messages
- Non-matching connections are skipped
- Condition evaluation errors are handled

#### 2.3 Multi-Target Fan-Out
**Scenario**: Single source routes to multiple targets
**Feature File**: `MessageRouting.feature` - "Fan-out to multiple targets"
**Test Cases**:
- One source → three targets
- All targets receive same message
- Targets execute in parallel

**Validation**:
- All target queues contain the message
- Message routing count matches target count
- All targets execute successfully

#### 2.4 Source Port Routing
**Scenario**: Route based on source port (True/False branches)
**Feature File**: `MessageRouting.feature` - "Source port based routing"
**Test Cases**:
- IfElseNode routes via TrueBranch or FalseBranch ports
- Switch node routes via case-specific ports
- Default port routing when no match

**Validation**:
- Correct port selected based on condition
- Only one branch receives message
- SourcePort property set correctly on message

#### 2.5 Dead Letter Queue
**Scenario**: Failed messages go to dead letter queue
**Feature File**: `MessageRouting.feature` - "Dead letter queue for failed messages"
**Test Cases**:
- Message routing failure adds to DLQ
- Max retries exceeded adds to DLQ
- DLQ entry contains failure reason and exception

**Validation**:
- DLQ contains failed message
- Entry has correct timestamp and reason
- Original message preserved in envelope

### 3. Control Flow ([ControlFlow.feature](../ExecutionEngine.IntegrationTests/Features/ControlFlow.feature))

Tests conditional branching, loops, and switches.

#### 3.1 If-Else Conditional Branching
**Scenario**: Execute different paths based on condition
**Feature File**: `ControlFlow.feature` - "If-Else conditional execution"
**Test Cases**:
- Condition true → TrueBranch executes
- Condition false → FalseBranch executes
- Access to global, local, and input data in condition
- Boolean expression evaluation

**Validation**:
- Correct branch executes
- Other branch does not execute
- BranchTaken and ConditionResult in output
- Downstream nodes receive correct branch data

#### 3.2 ForEach Collection Iteration
**Scenario**: Iterate over collection items
**Feature File**: `ControlFlow.feature` - "ForEach collection iteration"
**Test Cases**:
- Iterate over array of items
- Each iteration receives item and index
- Child nodes execute once per item
- Collection from LINQ expression

**Validation**:
- Correct number of iterations (matches collection size)
- Each child receives unique item and index
- ItemsProcessed output matches collection size
- Child nodes can execute in parallel

#### 3.3 While Loop with Condition
**Scenario**: Loop while condition remains true
**Feature File**: `ControlFlow.feature` - "While loop with condition re-evaluation"
**Test Cases**:
- Loop executes while condition true
- Condition re-evaluated on each iteration
- Child modifies variables affecting condition
- Max iterations limit prevents infinite loop

**Validation**:
- Loop terminates when condition false
- IterationCount output is correct
- Variables updated by child affect next iteration
- Infinite loop protection triggers on max iterations

#### 3.4 Switch Multi-Way Branching
**Scenario**: Route to different handlers based on value
**Feature File**: `ControlFlow.feature` - "Switch multi-way branching"
**Test Cases**:
- Expression evaluates to string value
- Matching case port receives message
- Non-matching cases do not execute
- Default port for unmatched values

**Validation**:
- Correct case handler executes
- ExpressionResult and MatchedCase in output
- Only one branch executes
- Default case works for unknown values

#### 3.5 Nested Loops
**Scenario**: ForEach within ForEach (nested iteration)
**Feature File**: `ControlFlow.feature` - "Nested loop iteration"
**Test Cases**:
- Outer loop iterates over items
- Inner loop iterates within each outer iteration
- Correct total execution count (outer * inner)

**Validation**:
- All combinations execute
- Correct nesting of iteration indices
- Total executions = outer count × inner count

### 4. State Management ([StateManagement.feature](../ExecutionEngine.IntegrationTests/Features/StateManagement.feature))

Tests workflow and node execution context state handling.

#### 4.1 Workflow Variables
**Scenario**: Share data across nodes via workflow variables
**Feature File**: `StateManagement.feature` - "Workflow variable sharing"
**Test Cases**:
- Set workflow variable in Node A
- Read workflow variable in Node B
- Variable persists across node executions
- Thread-safe concurrent access

**Validation**:
- Variable accessible from all nodes
- Variable updates visible immediately
- No data loss with concurrent access
- Variables available in conditions

#### 4.2 Node Input/Output Data Flow
**Scenario**: Pass data from node output to downstream node input
**Feature File**: `StateManagement.feature` - "Node output to input data flow"
**Test Cases**:
- Node A sets OutputData
- Node B receives as InputData
- Data transformation pipeline (A→B→C)

**Validation**:
- OutputData correctly passed as InputData
- Data pipeline preserves values
- Multiple outputs to multiple inputs work

#### 4.3 Local Variables (Node Scoped)
**Scenario**: Node-scoped variables isolated from workflow
**Feature File**: `StateManagement.feature` - "Node scoped local variables"
**Test Cases**:
- Local variables scoped to node instance
- Different node instances have separate locals
- Locals not visible to other nodes

**Validation**:
- Local variables don't leak to other nodes
- Each iteration has fresh locals
- Locals persist within node execution

#### 4.4 Context Propagation
**Scenario**: NodeExecutionContext flows through workflow
**Feature File**: `StateManagement.feature` - "Execution context propagation"
**Test Cases**:
- Context contains input from previous node
- Context accumulates data through pipeline
- Context includes iteration metadata (ForEach, While)

**Validation**:
- Context chain is preserved
- Iteration index and item available in context
- Historical data accessible

### 5. Error Handling ([ErrorHandling.feature](../ExecutionEngine.IntegrationTests/Features/ErrorHandling.feature))

Tests failure scenarios, retry logic, and recovery.

#### 5.1 Node Failure Handling
**Scenario**: Handle node execution failures
**Feature File**: `ErrorHandling.feature` - "Node execution failure"
**Test Cases**:
- Node throws exception → Failed status
- OnFail connections triggered
- Error message captured
- Workflow fails if no error handler

**Validation**:
- NodeFailed event fires
- Error details in NodeInstance
- OnFail edges route correctly
- Workflow status reflects failure

#### 5.2 Retry Policy
**Scenario**: Retry failed nodes with backoff
**Feature File**: `ErrorHandling.feature` - "Retry policy execution"
**Test Cases**:
- Node fails → retries configured times
- Backoff delay between retries
- Success after retry continues workflow
- Final failure after max retries

**Validation**:
- Retry attempts match policy
- Delays observed between retries
- Success on retry clears failure
- Max retries exceeded triggers failure

#### 5.3 Circuit Breaker
**Scenario**: Circuit breaker prevents repeated failures
**Feature File**: `ErrorHandling.feature` - "Circuit breaker pattern"
**Test Cases**:
- Multiple failures open circuit
- Circuit open prevents execution
- Fallback node executes when open
- Circuit resets after timeout

**Validation**:
- Circuit state transitions correctly
- Fallback routing works
- Circuit reset allows retry
- Success closes circuit

#### 5.4 Compensation (Saga Pattern)
**Scenario**: Compensating transactions on workflow failure
**Feature File**: `ErrorHandling.feature` - "Compensation transaction execution"
**Test Cases**:
- Workflow fails mid-execution
- Completed nodes trigger compensation
- Compensation nodes execute in reverse order
- Compensation failures logged but don't fail workflow

**Validation**:
- Compensation nodes identified correctly
- Reverse execution order
- Compensation results captured
- Original failure preserved

#### 5.5 Timeout Handling
**Scenario**: Workflow times out after configured duration
**Feature File**: `ErrorHandling.feature` - "Workflow timeout"
**Test Cases**:
- Long-running workflow exceeds timeout
- Timeout cancellation token triggered
- Workflow status set to Cancelled
- Partial results captured

**Validation**:
- TimeoutException thrown
- Cancellation propagates to nodes
- Workflow status is Cancelled
- Timeout duration respected

### 6. Concurrency ([Concurrency.feature](../ExecutionEngine.IntegrationTests/Features/Concurrency.feature))

Tests parallel execution, throttling, and resource management.

#### 6.1 Concurrent Node Execution
**Scenario**: Multiple nodes execute concurrently
**Feature File**: `Concurrency.feature` - "Concurrent node execution"
**Test Cases**:
- Parallel nodes execute simultaneously
- No blocking between independent nodes
- Thread-safe state access

**Validation**:
- Overlapping execution timestamps
- No deadlocks or race conditions
- All nodes complete successfully
- Correct final state

#### 6.2 Workflow Concurrency Limit
**Scenario**: Limit concurrent nodes in workflow
**Feature File**: `Concurrency.feature` - "Workflow concurrency limit"
**Test Cases**:
- Concurrency limit set to N
- Only N nodes execute concurrently
- Additional nodes queued
- Nodes execute as slots free

**Validation**:
- Max N nodes running at once
- Queued nodes execute when slots available
- Concurrency limiter releases slots
- All nodes eventually execute

#### 6.3 Node Throttling
**Scenario**: Throttle specific node execution rate
**Feature File**: `Concurrency.feature` - "Node throttling"
**Test Cases**:
- Node throttled to max concurrent instances
- Multiple instances queued
- Throttle limits honored

**Validation**:
- Throttle limits respected
- Instances execute as throttle permits
- No over-execution
- All instances complete

#### 6.4 Priority Queue Execution
**Scenario**: High-priority nodes execute first
**Feature File**: `Concurrency.feature` - "Priority based execution"
**Test Cases**:
- Nodes with different priorities
- High priority executes before low
- Priority affects queue ordering

**Validation**:
- High priority nodes execute first
- Priority ordering maintained
- All priorities eventually execute

### 7. Advanced Features ([AdvancedFeatures.feature](../ExecutionEngine.IntegrationTests/Features/AdvancedFeatures.feature))

Tests container nodes, subflows, timers, and other advanced capabilities.

#### 7.1 Container Node Execution
**Scenario**: Container with child nodes executes as unit
**Feature File**: `AdvancedFeatures.feature` - "Container node execution"
**Test Cases**:
- Container starts child nodes
- Child nodes execute internally
- Container completes when all children done
- Container fails if any child fails
- Child results aggregated in container output

**Validation**:
- All children execute
- Container completion waits for children
- ChildResults output contains all child data
- Container failure on child failure

#### 7.2 Subflow Execution
**Scenario**: Subflow node executes external workflow
**Feature File**: `AdvancedFeatures.feature` - "Subflow workflow execution"
**Test Cases**:
- Subflow loads external workflow file
- Subflow executes with input mappings
- Subflow outputs mapped back to parent
- Nested subflows supported

**Validation**:
- External workflow loaded correctly
- Input/output mappings work
- Subflow completes successfully
- Parent workflow continues after subflow

#### 7.3 Timer Trigger
**Scenario**: Timer node triggers workflow on schedule
**Feature File**: `AdvancedFeatures.feature` - "Timer scheduled trigger"
**Test Cases**:
- Timer evaluates cron schedule
- Trigger occurs at scheduled time
- TriggerOnStart option works
- Triggered output indicates trigger status

**Validation**:
- Schedule parsed correctly
- Trigger occurs at expected time
- Triggered=true/false output correct
- Downstream nodes use condition on Triggered

#### 7.4 Nested Container Nodes
**Scenario**: Container within container
**Feature File**: `AdvancedFeatures.feature` - "Nested container execution"
**Test Cases**:
- Outer container has inner container as child
- Inner container children execute
- Outer container waits for inner completion
- Multi-level nesting works

**Validation**:
- All levels execute correctly
- Completion propagates upward
- Results aggregated at each level

#### 7.5 Dynamic Workflow Modification
**Scenario**: Workflow structure changes during execution
**Feature File**: `AdvancedFeatures.feature` - "Dynamic workflow modification"
**Test Cases**:
- Add nodes during execution
- Modify connections dynamically
- Disable/enable connections at runtime

**Validation**:
- New nodes execute
- Modified routing works
- Disabled connections skip
- Workflow consistency maintained

### 8. State Persistence and Recovery ([AdvancedFeatures.feature](../ExecutionEngine.IntegrationTests/Features/AdvancedFeatures.feature))

#### 8.1 Checkpoint and Resume
**Scenario**: Checkpoint workflow state and resume later
**Feature File**: `AdvancedFeatures.feature` - "Checkpoint and resume"
**Note**: State persistence scenarios are included in the AdvancedFeatures.feature file.
**Test Cases**:
- Checkpoint saves workflow state
- Resume restores state correctly
- Nodes continue from checkpoint
- Failed workflows can resume

**Validation**:
- State persisted correctly
- Resume loads correct state
- Execution continues from checkpoint
- No duplicate executions

## Test Data and Fixtures

### Test Workflow Definitions

All test scenarios use predefined workflow YAML files located in `ExecutionEngine.IntegrationTests/TestWorkflows/`:

- `SimpleSequential.yaml`: A → B → C sequential workflow
- `ParallelFanOut.yaml`: A → (B, C, D) parallel execution
- `ConditionalBranching.yaml`: IfElse with True/False paths
- `ForEachIteration.yaml`: ForEach loop over collection
- `WhileLoop.yaml`: While loop with condition
- `SwitchRouting.yaml`: Switch with multiple cases
- `ContainerWorkflow.yaml`: Container with child nodes
- `SubflowExample.yaml`: Workflow calling subflow
- `ErrorHandling.yaml`: Workflow with retry and fallback
- `ConcurrencyTest.yaml`: Parallel execution with limits

### Test Node Implementations

Custom test nodes located in `ExecutionEngine.IntegrationTests/TestNodes/`:

- **DelayNode**: Simulates long-running operations
- **CounterNode**: Increments workflow counter (for loops)
- **FailureNode**: Intentionally fails (for error testing)
- **RandomDelayNode**: Random execution time (for concurrency testing)
- **DataGeneratorNode**: Generates test data collections

## Assertions and Validation

### Common Assertions

All tests use FluentAssertions for readable test validation:

```csharp
// Workflow completion
context.Status.Should().Be(WorkflowExecutionStatus.Completed);

// Node execution order
nodeStartEvents.Should().ContainInOrder("node-a", "node-b", "node-c");

// Data flow validation
nodeContext.InputData["key"].Should().Be(expectedValue);

// Error handling
dlq.Count.Should().BeGreaterThan(0);
dlq.GetAllEntriesAsync().Result.Should().Contain(e => e.Reason.Contains("expected error"));

// Timing validation
context.Duration.Should().BeGreaterThan(TimeSpan.Zero);
context.Duration.Should().BeLessThan(expectedMaxDuration);
```

### Integration Test Helpers

Helper classes in `ExecutionEngine.IntegrationTests/Helpers/`:

- **WorkflowTestFixture**: Sets up test workflows
- **EventCapture**: Captures workflow and node events
- **StateInspector**: Inspects workflow state during execution
- **TimingValidator**: Validates execution timing and ordering

## Running Integration Tests

### Command Line

```bash
# Run all integration tests
dotnet test ExecutionEngine.IntegrationTests/ExecutionEngine.IntegrationTests.csproj

# Run specific feature
dotnet test --filter "FullyQualifiedName~WorkflowExecution"

# Run with category
dotnet test --filter "Category=Integration"

# Run specific scenario
dotnet test --filter "DisplayName~Simple sequential workflow"
```

### Test Tags

- `@integration`: All integration tests
- `@smoke`: Critical path tests
- `@slow`: Tests with longer execution time
- `@parallel`: Concurrency related tests
- `@errorhandling`: Error and failure scenarios

## Performance Benchmarks

Integration tests include performance validation:

- Simple sequential workflow: < 100ms
- Parallel fan-out (10 nodes): < 200ms
- ForEach iteration (100 items): < 1s
- Complex workflow (50 nodes): < 2s
- Message routing overhead: < 5ms per message

## Test Coverage Goals

- **Line Coverage**: > 80%
- **Branch Coverage**: > 75%
- **Scenario Coverage**: All documented workflows
- **Error Path Coverage**: All failure modes tested
