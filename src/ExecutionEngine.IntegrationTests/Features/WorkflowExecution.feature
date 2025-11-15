Feature: Workflow Execution
  As a workflow engine user
  I want to execute workflows with different topologies
  So that I can orchestrate complex task sequences

  @integration @smoke
  Scenario: Simple sequential workflow execution
    Given I have a workflow with nodes "NodeA", "NodeB", "NodeC"
    And "NodeA" connects to "NodeB" on Complete
    And "NodeB" connects to "NodeC" on Complete
    When I start the workflow
    Then the workflow should complete successfully
    And nodes should execute in order "NodeA", "NodeB", "NodeC"
    And all nodes should have status "Completed"
    And the workflow duration should be greater than zero

  @integration @parallel
  Scenario: Parallel workflow execution
    Given I have a workflow with nodes "Start", "ParallelA", "ParallelB", "ParallelC", "Join"
    And "Start" connects to "ParallelA" on Complete
    And "Start" connects to "ParallelB" on Complete
    And "Start" connects to "ParallelC" on Complete
    And "ParallelA" connects to "Join" on Complete
    And "ParallelB" connects to "Join" on Complete
    And "ParallelC" connects to "Join" on Complete
    When I start the workflow
    Then the workflow should complete successfully
    And "ParallelA", "ParallelB", "ParallelC" should execute in parallel
    And "Join" should execute after all parallel nodes complete

  @integration @parallel
  Scenario: Mixed parallel and sequential execution
    Given I have a workflow with nodes "Start", "P1", "P2", "Join", "Sequential", "End"
    And "Start" connects to "P1" on Complete
    And "Start" connects to "P2" on Complete
    And "P1" connects to "Join" on Complete
    And "P2" connects to "Join" on Complete
    And "Join" connects to "Sequential" on Complete
    And "Sequential" connects to "End" on Complete
    When I start the workflow
    Then the workflow should complete successfully
    And "P1", "P2" should execute in parallel
    And "Join" should wait for both "P1" and "P2"
    And "Sequential" should execute after "Join"
    And "End" should execute after "Sequential"

  @integration @smoke
  Scenario: Entry point node discovery
    Given I have a workflow with nodes "Entry1", "Entry2", "NodeA", "NodeB"
    And "Entry1" connects to "NodeA" on Complete
    And "Entry2" connects to "NodeB" on Complete
    When I start the workflow
    Then "Entry1" and "Entry2" should be identified as entry points
    And both entry points should receive initial trigger
    And the workflow should complete successfully

  @integration
  Scenario: Fan-out to multiple downstream nodes
    Given I have a workflow with nodes "Source", "Target1", "Target2", "Target3", "Target4"
    And "Source" connects to "Target1" on Complete
    And "Source" connects to "Target2" on Complete
    And "Source" connects to "Target3" on Complete
    And "Source" connects to "Target4" on Complete
    When I start the workflow
    Then all target nodes should receive the completion message
    And all targets should execute in parallel
    And the workflow should complete successfully

  @integration
  Scenario: Workflow with no cycles completes
    Given I have a valid acyclic workflow
    When I start the workflow
    Then the workflow should complete successfully
    And no infinite loops should occur
    And all reachable nodes should execute

  @integration
  Scenario: Workflow cancellation mid-execution
    Given I have a long-running workflow
    When I start the workflow
    And I cancel the workflow after 500 milliseconds
    Then the workflow status should be "Cancelled"
    And some nodes may not have executed
    And executed nodes should have their state preserved
