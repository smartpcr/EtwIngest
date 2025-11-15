Feature: Advanced Features
  As a workflow designer
  I want to use advanced node types and capabilities
  So that I can build complex workflow patterns

  @integration @smoke
  Scenario: Container node execution
    Given I have a Container node "MainContainer" with child nodes "Child1", "Child2", "Child3"
    And "Child1" connects to "Child2" on Complete
    And "Child2" connects to "Child3" on Complete
    When I start the workflow
    Then the container should start all entry point children
    And "Child1", "Child2", "Child3" should execute in order
    And the container should complete when all children are done
    And container output should contain ChildResults with all child data
    And the workflow should complete successfully

  @integration
  Scenario: Container completes when all children complete
    Given I have a Container with 5 parallel child nodes
    When I start the workflow
    Then the container should wait for all children to complete
    And the container status should be "Running" while children execute
    And the container should complete after the last child completes
    And ChildResults should contain results from all 5 children

  @integration @errorhandling
  Scenario: Container fails if any child fails
    Given I have a Container with child nodes "A", "B", "C"
    And "B" is configured to fail
    When I start the workflow
    Then "B" should fail
    And the container should fail when "B" fails
    And the container error should indicate child failure
    And remaining children may or may not complete based on error strategy

  @integration
  Scenario: Subflow workflow execution
    Given I have a Subflow node "CallExternal" referencing "ExternalWorkflow.yaml"
    And the subflow has input mappings:
      | ParentVariable | SubflowInput |
      | userName       | user         |
      | itemId         | id           |
    And the subflow has output mappings:
      | SubflowOutput | ParentVariable |
      | result        | subflowResult  |
      | status        | subflowStatus  |
    When I start the workflow
    Then the external workflow should load from "ExternalWorkflow.yaml"
    And subflow should receive mapped inputs
    And the subflow should execute to completion
    And subflow outputs should be mapped back to parent variables
    And parent workflow should continue after subflow completes

  @integration
  Scenario: Nested subflow execution
    Given I have workflow A calling subflow B
    And subflow B calls subflow C
    And subflow C calls subflow D
    When I start workflow A
    Then all subflows should execute in order: A → B → C → D
    And each subflow should complete successfully
    And control should return through the chain: D → C → B → A
    And the final workflow should complete successfully

  @integration
  Scenario: Subflow execution with failure handling
    Given I have a Subflow node "Subflow1" that references a failing workflow
    And "Subflow1" connects to "HandleError" on Fail
    When I start the workflow
    Then the subflow should fail
    And the Subflow node should capture the failure
    And "HandleError" should execute
    And the parent workflow should handle the subflow failure gracefully

  @integration
  Scenario: Timer scheduled trigger
    Given I have a Timer node "ScheduledTask" with cron expression "0 0 * * *"
    And TriggerOnStart is set to true
    When I start the workflow
    Then the timer should trigger immediately (because of TriggerOnStart)
    And the output should contain Triggered = true
    And downstream nodes should receive the trigger
    And the workflow should complete successfully

  @integration
  Scenario: Timer with condition-based routing
    Given I have a Timer node "CheckSchedule" with cron expression "0 9 * * 1-5"
    And "CheckSchedule" connects to "WeekdayTask" with condition "GetInput('Triggered') == true"
    And "CheckSchedule" connects to "SkipTask" with condition "GetInput('Triggered') == false"
    And current time matches the schedule
    When I start the workflow
    Then "CheckSchedule" should evaluate the schedule
    And Triggered output should be <triggered>
    And <executedNode> should execute

    Examples:
      | triggered | executedNode |
      | true      | WeekdayTask  |
      | false     | SkipTask     |

  @integration
  Scenario: Nested container execution
    Given I have an outer Container "Outer" with child Container "Inner"
    And "Inner" has child nodes "A", "B", "C"
    And "Outer" has additional child node "D"
    When I start the workflow
    Then "Inner" container should execute as a child of "Outer"
    And "A", "B", "C" should execute within "Inner"
    And "Inner" should complete when all its children complete
    And "D" should execute in parallel with "Inner"
    And "Outer" should complete when both "Inner" and "D" complete
    And ChildResults should be aggregated at each container level

  @integration
  Scenario: Multi-level nested containers
    Given I have containers nested 4 levels deep:
      | Level | Container | Children              |
      | 1     | Root      | Level2A, Level2B      |
      | 2     | Level2A   | Level3A               |
      | 3     | Level3A   | Level4A, Level4B      |
      | 4     | Level4A   | TaskA, TaskB, TaskC   |
    When I start the workflow
    Then all levels should execute correctly
    And completion should propagate from innermost to outermost
    And Root ChildResults should contain aggregated data from all levels
    And the workflow should complete successfully

  @integration
  Scenario: Dynamic workflow modification - add node at runtime
    Given I have a running workflow
    And the workflow is currently executing node "NodeB"
    When I dynamically add a new node "NodeX" with connections
    And I connect "NodeB" to "NodeX" on Complete
    Then "NodeX" should be added to the workflow graph
    And when "NodeB" completes, "NodeX" should execute
    And the workflow should complete successfully with the new node

  @integration
  Scenario: Dynamic workflow modification - disable connection
    Given I have a workflow with nodes "Source", "Target1", "Target2"
    And "Source" connects to both "Target1" and "Target2"
    And the workflow is running
    When I disable the connection from "Source" to "Target1"
    And "Source" completes
    Then only "Target2" should execute
    And "Target1" should not execute
    And message should not be routed to "Target1"

  @integration
  Scenario: Dynamic workflow modification - enable disabled connection
    Given I have a workflow with a disabled connection "A" → "B"
    And the connection is initially IsEnabled = false
    When I enable the connection at runtime
    And node "A" completes
    Then the message should be routed to "B"
    And "B" should execute
    And the workflow should complete successfully

  @integration
  Scenario: Checkpoint workflow state
    Given I have a long-running workflow with 10 nodes
    And checkpointing is enabled
    When the workflow executes through 5 nodes
    And I create a checkpoint
    Then the checkpoint should capture current workflow state
    And the checkpoint should include completed node states
    And the checkpoint should include workflow variables
    And the checkpoint should include pending message queues

  @integration
  Scenario: Resume workflow from checkpoint
    Given I have a checkpointed workflow that failed at node "Step5"
    And nodes "Step1" through "Step4" completed successfully
    When I resume the workflow from the checkpoint
    Then nodes "Step1" through "Step4" should not re-execute
    And execution should resume at "Step5"
    And workflow variables should be restored from checkpoint
    And the workflow should complete if "Step5" succeeds

  @integration
  Scenario: Checkpoint and resume with modified workflow
    Given I have a checkpointed workflow
    And I modify the workflow definition after checkpoint
    When I resume from the checkpoint
    Then the engine should detect the modification
    And appropriate validation should occur
    And the engine should handle compatibility issues
    And resume should succeed if modifications are compatible

  @integration @errorhandling
  Scenario: Resume failed workflow with error correction
    Given I have a workflow that failed at node "FailingNode"
    And I create a checkpoint before the failure
    And I fix the issue causing the failure
    When I resume from checkpoint
    Then "FailingNode" should execute again
    And this time it should succeed
    And the workflow should continue from that point
    And the workflow should complete successfully

  @integration
  Scenario: Automatic checkpointing at intervals
    Given I have a long-running workflow with auto-checkpoint enabled
    And checkpoint interval is set to every 5 nodes
    When I start the workflow
    Then checkpoints should be created after nodes 5, 10, 15, etc.
    And each checkpoint should contain current state
    And checkpoints should not significantly impact performance
    And the workflow should complete successfully

  @integration
  Scenario: Subflow with container children
    Given I have a Subflow node that loads a workflow containing Containers
    And the subflow's workflow has nested containers
    When I execute the subflow
    Then the external workflow should load correctly
    And containers within the subflow should execute
    And container children should execute correctly
    And the subflow should aggregate all results
    And control should return to parent workflow

  @integration @slow
  Scenario: Complex workflow with all advanced features
    Given I have a workflow combining:
      | Feature           | Description                           |
      | Containers        | 3 container nodes with children       |
      | Subflows          | 2 subflow calls to external workflows |
      | Timers            | 1 timer trigger node                  |
      | Control Flow      | IfElse, ForEach, Switch nodes         |
      | Error Handling    | Retry policies and fallback nodes     |
      | Concurrency       | Parallel execution with limits        |
    When I start the workflow
    Then all features should work together correctly
    And the workflow should execute to completion
    And all nodes should behave as expected
    And final state should reflect all operations
    And the workflow should complete within 5 seconds
