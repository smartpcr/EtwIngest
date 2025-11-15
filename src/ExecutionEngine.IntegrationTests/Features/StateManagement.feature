Feature: State Management
  As a workflow engine
  I want to manage workflow and node state correctly
  So that data flows properly through the workflow

  @integration @smoke
  Scenario: Workflow variable sharing across nodes
    Given I have a workflow with nodes "SetVar", "ReadVar"
    And "SetVar" sets workflow variable "sharedData" to "test value"
    And "SetVar" connects to "ReadVar" on Complete
    When I start the workflow
    Then "ReadVar" should be able to read workflow variable "sharedData"
    And the value should be "test value"
    And the workflow should complete successfully

  @integration @smoke
  Scenario: Node output to input data flow
    Given I have a workflow with nodes "Producer", "Consumer"
    And "Producer" sets output data "result" to 42
    And "Producer" connects to "Consumer" on Complete
    When I start the workflow
    Then "Consumer" should receive input data "result" with value 42
    And the data should flow from Producer output to Consumer input
    And the workflow should complete successfully

  @integration
  Scenario: Data transformation pipeline
    Given I have a workflow with nodes "StepA", "StepB", "StepC"
    And "StepA" sets output "value" to 10
    And "StepB" doubles the input "value" and outputs it
    And "StepC" adds 5 to input "value" and outputs it
    And nodes are connected StepA → StepB → StepC
    When I start the workflow
    Then "StepB" should receive input "value" = 10
    And "StepB" should output "value" = 20
    And "StepC" should receive input "value" = 20
    And "StepC" should output "value" = 25

  @integration
  Scenario: Node scoped local variables
    Given I have a ForEach node iterating 3 times
    And each iteration sets a local variable "temp"
    When I start the workflow
    Then each iteration should have its own "temp" variable
    And local variables should not interfere between iterations
    And local variables should not be visible to other nodes

  @integration
  Scenario: Execution context propagation
    Given I have a sequential workflow A → B → C
    And "A" sets output data "step" to "A completed"
    And "B" sets output data "step" to "B completed"
    When I start the workflow
    Then "B" context should contain input from "A"
    And "C" context should contain input from "B"
    And context chain should be preserved throughout

  @integration @parallel
  Scenario: Thread-safe concurrent variable access
    Given I have 10 parallel nodes
    And each node increments a workflow variable "counter"
    When I start the workflow
    Then the final value of "counter" should be 10
    And no race conditions should occur
    And all increments should be recorded

  @integration
  Scenario: ForEach iteration metadata in context
    Given I have a ForEach node with ItemVariableName "item"
    And the collection contains ["A", "B", "C"]
    When I start the workflow
    Then iteration 0 should have input "item" = "A" and "itemIndex" = 0
    And iteration 1 should have input "item" = "B" and "itemIndex" = 1
    And iteration 2 should have input "item" = "C" and "itemIndex" = 2

  @integration
  Scenario: While loop iteration context
    Given I have a While loop that runs 5 times
    When I start the workflow
    Then each iteration should receive "iterationIndex" in context
    And iteration indices should be 0, 1, 2, 3, 4

  @integration
  Scenario: Workflow variables persist across node executions
    Given I have a workflow that sets variable "persistent" in node A
    And later accesses it in node Z (after many nodes)
    When I start the workflow
    Then node Z should successfully read "persistent"
    And the value should be unchanged
    And variables should persist for workflow lifetime

  @integration
  Scenario: Output data preserved in NodeCompleteMessage
    Given I have nodes "A" and "B" connected
    And "A" sets complex output data with nested objects
    When I start the workflow
    Then the NodeCompleteMessage should contain all output data
    And "B" should receive the complete data structure
    And no data should be lost in transmission

  @integration
  Scenario: Multiple outputs to multiple inputs
    Given I have node "Source" with outputs "out1", "out2", "out3"
    And "Source" connects to "Target1", "Target2", "Target3"
    When I start the workflow
    Then all targets should receive all output data
    And each target should have access to "out1", "out2", "out3"

  @integration
  Scenario: Context isolation between workflow instances
    Given I start workflow instance A
    And I start workflow instance B
    And both workflows have a variable "instanceData"
    When instance A sets "instanceData" to "A value"
    And instance B sets "instanceData" to "B value"
    Then instance A should see "A value"
    And instance B should see "B value"
    And contexts should not interfere with each other

  @integration
  Scenario: Node instance tracking for debugging
    Given I have a workflow that executes node "Task" 3 times
    When I start the workflow
    Then there should be 3 distinct NodeInstance records
    And each instance should have unique InstanceId
    And each instance should have complete execution history
    And instances should include start time, end time, duration

  @integration
  Scenario: Null and empty value handling
    Given I have a node that sets output "value" to null
    When the downstream node accesses the input
    Then it should handle null gracefully
    And no exceptions should be thrown
    And the workflow should continue normally
