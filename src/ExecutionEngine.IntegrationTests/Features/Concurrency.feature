Feature: Concurrency
  As a workflow engine
  I want to manage parallel execution and resource limits
  So that workflows can run efficiently without overloading the system

  @integration @smoke @parallel
  Scenario: Concurrent node execution
    Given I have a workflow with nodes "Start", "P1", "P2", "P3", "P4"
    And "Start" connects to "P1" on Complete
    And "Start" connects to "P2" on Complete
    And "Start" connects to "P3" on Complete
    And "Start" connects to "P4" on Complete
    And each parallel node has a 200ms delay
    When I start the workflow
    Then "P1", "P2", "P3", "P4" should execute in parallel
    And their execution should overlap in time
    And there should be no deadlocks or race conditions
    And all nodes should complete successfully
    And total execution time should be less than 500ms

  @integration @parallel
  Scenario: Workflow concurrency limit
    Given I have a workflow with 20 parallel nodes
    And the workflow has a concurrency limit of 5
    When I start the workflow
    Then no more than 5 nodes should execute concurrently
    And additional nodes should be queued
    And nodes should execute as slots become available
    And all 20 nodes should eventually complete
    And the concurrency limiter should release slots correctly

  @integration @parallel
  Scenario: Node throttling
    Given I have a workflow with 10 instances of "ThrottledTask"
    And "ThrottledTask" has a throttle limit of 3 concurrent instances
    When I start the workflow
    Then no more than 3 instances of "ThrottledTask" should run simultaneously
    And queued instances should wait for throttle slots
    And all 10 instances should eventually complete
    And throttle limits should be respected throughout execution

  @integration @parallel
  Scenario: Priority based execution
    Given I have a workflow with nodes having different priorities:
      | NodeId    | Priority |
      | HighPri1  | 100      |
      | HighPri2  | 100      |
      | MedPri1   | 50       |
      | MedPri2   | 50       |
      | LowPri1   | 10       |
      | LowPri2   | 10       |
    And the workflow has a concurrency limit of 2
    When I start the workflow
    Then nodes should execute in priority order
    And "HighPri1" and "HighPri2" should execute first
    And "MedPri1" and "MedPri2" should execute before low priority
    And all nodes should eventually complete
    And priority ordering should be maintained

  @integration @parallel
  Scenario: Thread-safe state access
    Given I have 20 parallel nodes
    And each node increments a shared workflow variable "counter"
    And each node performs 10 increments
    When I start the workflow
    Then the final value of "counter" should be 200
    And no race conditions should occur
    And all increments should be recorded
    And state access should be thread-safe

  @integration @parallel
  Scenario: Parallel branches with synchronization point
    Given I have a workflow: Start → (Branch1, Branch2, Branch3) → Sync → End
    And each branch has 3 sequential nodes with delays
    And "Sync" node waits for all branches to complete
    When I start the workflow
    Then all 3 branches should execute in parallel
    And "Sync" should wait for all branches to complete
    And "Sync" should execute after all branches are done
    And "End" should execute after "Sync"
    And the workflow should complete successfully

  @integration @parallel @slow
  Scenario: High concurrent load stress test
    Given I have a workflow with 100 parallel nodes
    And each node has a random delay between 10ms and 100ms
    And the workflow has a concurrency limit of 20
    When I start the workflow
    Then all 100 nodes should complete successfully
    And no more than 20 nodes should run concurrently
    And there should be no deadlocks or resource exhaustion
    And the workflow should complete within 2 seconds

  @integration @parallel
  Scenario: Concurrent ForEach iterations
    Given I have a ForEach node iterating over 50 items
    And ForEach allows parallel iteration
    And the loop body has a 50ms delay
    When I start the workflow
    Then iterations should execute in parallel
    And multiple iterations should run simultaneously
    And all 50 items should be processed
    And execution time should benefit from parallelization

  @integration @parallel
  Scenario: Deadlock prevention with circular dependencies
    Given I have a workflow that could cause circular waiting
    And nodes have shared resource locks
    When I start the workflow
    Then the engine should detect potential deadlocks
    And appropriate error handling should occur
    And the workflow should not hang indefinitely
    And resources should be released properly

  @integration @parallel
  Scenario: Graceful degradation under resource pressure
    Given I have a workflow with high concurrency requirements
    And system resources are constrained
    When I start the workflow
    Then the engine should throttle execution appropriately
    And nodes should queue when resources unavailable
    And execution should continue as resources free up
    And no nodes should fail due to resource exhaustion
    And the workflow should complete successfully
